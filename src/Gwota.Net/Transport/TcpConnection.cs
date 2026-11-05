using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Channels;

namespace Gwota.Net.Transport;

/// <summary>
/// Newline-framed UTF-8 over TCP. Sent lines are queued and written by one background writer; closing writes what is
/// still queued (bounded by <see cref="NetProtocol.CloseFlushTimeout"/>) before shutting the socket down. An empty line
/// is a heartbeat, sent when nothing else went out for a while; the connection closes when nothing at all arrives
/// within the timeout.
/// </summary>
public sealed class TcpConnection : IConnection
{
    private const int InitialBufferSize = 4096;
    private const byte Newline = (byte)'\n';

    private static readonly UTF8Encoding Utf8 = new(false);

    private readonly TcpClient _client;
    private readonly NetworkStream _stream;
    private readonly ConcurrentQueue<string> _inbox = new();
    private readonly Channel<string> _outbox = Channel.CreateUnbounded<string>(new UnboundedChannelOptions { SingleReader = true });
    private readonly Lock _closeLock = new();
    private readonly CancellationTokenSource _stop = new();
    private readonly CancellationTokenSource _flushDeadline = new();
    private readonly TimeSpan _heartbeat;
    private readonly TimeSpan _timeout;
    private readonly TimeProvider _time;
    private long _lastReceived;
    private long _lastQueued;
    private volatile string? _closeReason;

    private TcpConnection(TcpClient client, TimeSpan heartbeat, TimeSpan timeout, TimeProvider time)
    {
        _client = client;
        _client.NoDelay = true;
        _stream = client.GetStream();
        _heartbeat = heartbeat;
        _timeout = timeout;
        _time = time;
        _lastReceived = _lastQueued = time.GetTimestamp();
        _ = Task.Run(ReadLoopAsync);
        _ = Task.Run(WriteLoopAsync);
        _ = Task.Run(() => WatchLoopAsync(_stop.Token));
    }

    public bool IsClosed => _closeReason is not null;

    public string? CloseReason => _closeReason;

    public EndPoint? RemoteEndPoint => _client.Client.RemoteEndPoint;

    public static TcpConnection Wrap(TcpClient client, TimeSpan? heartbeat = null, TimeSpan? timeout = null, TimeProvider? time = null)
    {
        ArgumentNullException.ThrowIfNull(client);
        return new(client, heartbeat ?? NetProtocol.Heartbeat, timeout ?? NetProtocol.ReceiveTimeout, time ?? TimeProvider.System);
    }

    /// <summary>Connects to a host, failing after <paramref name="connectTimeout"/>.</summary>
    public static async Task<TcpConnection> ConnectAsync(
        string host,
        int port,
        TimeSpan connectTimeout,
        TimeProvider? time = null,
        CancellationToken cancellation = default)
    {
        time ??= TimeProvider.System;
        var client = new TcpClient();
        try
        {
            using var timeout = new CancellationTokenSource(connectTimeout, time);
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellation, timeout.Token);
            await client.ConnectAsync(host, port, linked.Token).ConfigureAwait(false);
            return Wrap(client, time: time);
        }
        catch
        {
            client.Dispose();
            throw;
        }
    }

    public void Send(string line)
    {
        ArgumentNullException.ThrowIfNull(line);
        if (line.Contains('\n', StringComparison.Ordinal))
        {
            throw new ArgumentException("A line cannot contain a newline.", nameof(line));
        }

        Enqueue(line);
    }

    public bool TryReceive(out string line) => _inbox.TryDequeue(out line!);

    public void Close(string reason)
    {
        lock (_closeLock)
        {
            if (_closeReason is not null)
            {
                return;
            }

            _closeReason = reason;
            _stop.Cancel();
            _flushDeadline.CancelAfter(NetProtocol.CloseFlushTimeout);
            _outbox.Writer.TryComplete();
        }
    }

    /// <summary>Closes; the socket is released once the queued lines are written or the flush times out.</summary>
    public void Dispose() => Close("Closed.");

    private void Enqueue(string line)
    {
        if (_closeReason is null && _outbox.Writer.TryWrite(line))
        {
            Interlocked.Exchange(ref _lastQueued, _time.GetTimestamp());
        }
    }

    /// <summary>The only writer to the socket. It ends once closing has written (or given up on) the queue.</summary>
    private async Task WriteLoopAsync()
    {
        var deadline = _flushDeadline.Token;
        var buffer = new byte[InitialBufferSize];
        try
        {
            await foreach (var line in _outbox.Reader.ReadAllAsync(deadline).ConfigureAwait(false))
            {
                var needed = Utf8.GetMaxByteCount(line.Length) + 1;
                if (buffer.Length < needed)
                {
                    buffer = new byte[Math.Max(needed, buffer.Length * 2)];
                }

                var length = Utf8.GetBytes(line, buffer);
                buffer[length] = Newline;
                await _stream.WriteAsync(buffer.AsMemory(0, length + 1), deadline).ConfigureAwait(false);
            }
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException or SocketException or OperationCanceledException)
        {
            Close("The connection was lost.");
        }
        finally
        {
            Release();
        }
    }

    private void Release()
    {
        Close("The connection was lost.");
        try
        {
            // A graceful shutdown lets the lines just written reach the other side before it sees the close.
            _client.Client.Shutdown(SocketShutdown.Send);
        }
        catch (Exception ex) when (ex is SocketException or ObjectDisposedException)
        {
        }

        _client.Dispose();
        lock (_closeLock)
        {
            _stop.Dispose();
            _flushDeadline.Dispose();
        }
    }

    /// <summary>Splits incoming bytes into lines; it ends when the socket is released or the other side closes.</summary>
    private async Task ReadLoopAsync()
    {
        var buffer = new byte[InitialBufferSize];
        var filled = 0;
        try
        {
            while (true)
            {
                if (filled == buffer.Length)
                {
                    Array.Resize(ref buffer, Math.Min(buffer.Length * 2, NetProtocol.MaxLineLength + 1));
                }

                var read = await _stream.ReadAsync(buffer.AsMemory(filled)).ConfigureAwait(false);
                if (read == 0)
                {
                    Close("The other side closed the connection.");
                    return;
                }

                Interlocked.Exchange(ref _lastReceived, _time.GetTimestamp());
                var scanned = filled;
                filled += read;
                var start = 0;
                int newline;
                while ((newline = buffer.AsSpan(scanned, filled - scanned).IndexOf(Newline)) >= 0)
                {
                    var end = scanned + newline;
                    if (end - start > NetProtocol.MaxLineLength)
                    {
                        break;
                    }

                    if (end > start)
                    {
                        _inbox.Enqueue(Utf8.GetString(buffer, start, end - start));
                    }

                    start = scanned = end + 1;
                }

                if (filled - start > NetProtocol.MaxLineLength)
                {
                    Close("The other side sent an oversized message.");
                    return;
                }

                buffer.AsSpan(start, filled - start).CopyTo(buffer);
                filled -= start;
            }
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException or SocketException or InvalidOperationException)
        {
            Close("The connection was lost.");
        }
    }

    /// <summary>Sends heartbeats and closes the connection when the other side went quiet.</summary>
    private async Task WatchLoopAsync(CancellationToken stop)
    {
        var tick = (_heartbeat < _timeout ? _heartbeat : _timeout) / 4;
        try
        {
            while (!stop.IsCancellationRequested)
            {
                await Task.Delay(tick, _time, stop).ConfigureAwait(false);
                if (_time.GetElapsedTime(Interlocked.Read(ref _lastReceived)) > _timeout)
                {
                    Close("The connection timed out.");
                    return;
                }

                if (_time.GetElapsedTime(Interlocked.Read(ref _lastQueued)) >= _heartbeat)
                {
                    Enqueue(string.Empty);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Closed.
        }
    }
}

/// <summary>Listens on all IPv4 interfaces unless an address is given; port 0 picks a free port.</summary>
public sealed class TcpConnectionListener : IConnectionListener
{
    private readonly TcpListener _listener;
    private readonly TimeProvider? _time;

    public TcpConnectionListener(int port, IPAddress? address = null, TimeProvider? time = null)
    {
        _listener = new TcpListener(address ?? IPAddress.Any, port);
        _time = time;
        _listener.Start();
    }

    public int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;

    public IConnection? TryAccept() => _listener.Pending() ? TcpConnection.Wrap(_listener.AcceptTcpClient(), time: _time) : null;

    public void Dispose() => _listener.Stop();
}
