using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Gwota.Net.Transport;

/// <summary>
/// Newline-framed UTF-8 over TCP. An empty line is a heartbeat, sent when nothing else went out for a while; the
/// connection closes when nothing at all arrives within the timeout.
/// </summary>
public sealed class TcpConnection : IConnection
{
    public const int MaxLineLength = 64 * 1024;

    private static readonly UTF8Encoding Utf8 = new(false);

    private readonly TcpClient _client;
    private readonly NetworkStream _stream;
    private readonly ConcurrentQueue<string> _inbox = new();
    private readonly Lock _writeLock = new();
    private readonly CancellationTokenSource _stop = new();
    private readonly TimeSpan _heartbeat;
    private readonly TimeSpan _timeout;
    private readonly TimeProvider _time;
    private long _lastReceived;
    private long _lastSent;
    private volatile string? _closeReason;

    private TcpConnection(TcpClient client, TimeSpan heartbeat, TimeSpan timeout, TimeProvider time)
    {
        _client = client;
        _client.NoDelay = true;
        _stream = client.GetStream();
        _heartbeat = heartbeat;
        _timeout = timeout;
        _time = time;
        _lastReceived = _lastSent = time.GetTimestamp();
        _ = Task.Run(ReadLoopAsync);
        _ = Task.Run(HeartbeatLoopAsync);
    }

    public static TimeSpan DefaultHeartbeat { get; } = TimeSpan.FromSeconds(2);

    public static TimeSpan DefaultTimeout { get; } = TimeSpan.FromSeconds(10);

    public bool IsClosed
    {
        get
        {
            if (_closeReason is null && _time.GetElapsedTime(Interlocked.Read(ref _lastReceived)) > _timeout)
            {
                Close("The connection timed out.");
            }

            return _closeReason is not null;
        }
    }

    public string? CloseReason => IsClosed ? _closeReason : null;

    public EndPoint? RemoteEndPoint => _client.Client.RemoteEndPoint;

    public static TcpConnection Wrap(TcpClient client, TimeSpan? heartbeat = null, TimeSpan? timeout = null, TimeProvider? time = null)
    {
        ArgumentNullException.ThrowIfNull(client);
        return new(client, heartbeat ?? DefaultHeartbeat, timeout ?? DefaultTimeout, time ?? TimeProvider.System);
    }

    /// <summary>Connects to a host, failing after <paramref name="connectTimeout"/>.</summary>
    public static async Task<TcpConnection> ConnectAsync(string host, int port, TimeSpan connectTimeout, CancellationToken cancellation = default)
    {
        var client = new TcpClient();
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
            timeout.CancelAfter(connectTimeout);
            await client.ConnectAsync(host, port, timeout.Token).ConfigureAwait(false);
            return Wrap(client);
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

        Write(line);
    }

    public bool TryReceive(out string line) => _inbox.TryDequeue(out line!);

    public void Close(string reason)
    {
        if (Interlocked.CompareExchange(ref _closeReason, reason, null) is not null)
        {
            return;
        }

        _stop.Cancel();
        try
        {
            // A graceful shutdown lets lines already written (such as a goodbye) reach the other side.
            _client.Client.Shutdown(SocketShutdown.Send);
        }
        catch (Exception ex) when (ex is SocketException or ObjectDisposedException)
        {
        }

        _client.Dispose();
    }

    public void Dispose()
    {
        Close("Closed.");
        _stop.Dispose();
    }

    private void Write(string line)
    {
        if (_closeReason is not null)
        {
            return;
        }

        var bytes = Utf8.GetBytes(line + "\n");
        try
        {
            lock (_writeLock)
            {
                _stream.Write(bytes);
                Interlocked.Exchange(ref _lastSent, _time.GetTimestamp());
            }
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException or SocketException)
        {
            Close("The connection was lost.");
        }
    }

    private async Task ReadLoopAsync()
    {
        var buffer = new byte[4096];
        var line = new List<byte>();
        try
        {
            while (!_stop.IsCancellationRequested)
            {
                var read = await _stream.ReadAsync(buffer, _stop.Token).ConfigureAwait(false);
                if (read == 0)
                {
                    Close("The other side closed the connection.");
                    return;
                }

                Interlocked.Exchange(ref _lastReceived, _time.GetTimestamp());
                for (var i = 0; i < read; i++)
                {
                    if (buffer[i] != (byte)'\n')
                    {
                        line.Add(buffer[i]);
                        if (line.Count > MaxLineLength)
                        {
                            Close("The other side sent an oversized message.");
                            return;
                        }

                        continue;
                    }

                    if (line.Count > 0)
                    {
                        _inbox.Enqueue(Utf8.GetString([.. line]));
                        line.Clear();
                    }
                }
            }
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException or SocketException or OperationCanceledException)
        {
            Close("The connection was lost.");
        }
    }

    private async Task HeartbeatLoopAsync()
    {
        try
        {
            while (!_stop.IsCancellationRequested)
            {
                await Task.Delay(_heartbeat / 4, _time, _stop.Token).ConfigureAwait(false);
                if (_time.GetElapsedTime(Interlocked.Read(ref _lastSent)) >= _heartbeat)
                {
                    Write(string.Empty);
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

    public TcpConnectionListener(int port, IPAddress? address = null)
    {
        _listener = new TcpListener(address ?? IPAddress.Any, port);
        _listener.Start();
    }

    public int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;

    public IConnection? TryAccept() => _listener.Pending() ? TcpConnection.Wrap(_listener.AcceptTcpClient()) : null;

    public void Dispose() => _listener.Stop();
}