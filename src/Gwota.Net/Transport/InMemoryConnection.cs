using System.Collections.Concurrent;

namespace Gwota.Net.Transport;

/// <summary>One end of an in-process connection pair.</summary>
public sealed class InMemoryConnection : IConnection
{
    private readonly ConcurrentQueue<string> _inbox = new();
    private InMemoryConnection _other = null!;
    private volatile string? _closeReason;

    private InMemoryConnection()
    {
    }

    public bool IsClosed => _closeReason is not null;

    public string? CloseReason => _closeReason;

    public static (InMemoryConnection First, InMemoryConnection Second) CreatePair()
    {
        var first = new InMemoryConnection();
        var second = new InMemoryConnection();
        first._other = second;
        second._other = first;
        return (first, second);
    }

    public void Send(string line)
    {
        ArgumentNullException.ThrowIfNull(line);
        if (!IsClosed && !_other.IsClosed)
        {
            _other._inbox.Enqueue(line);
        }
    }

    public bool TryReceive(out string line) => _inbox.TryDequeue(out line!);

    public void Close(string reason)
    {
        if (IsClosed)
        {
            return;
        }

        _closeReason = reason;
        _other._closeReason ??= "The other side closed the connection.";
    }

    public void Dispose() => Close("Closed.");
}

/// <summary>An in-process listener: <see cref="Connect"/> hands back the client end and queues the server end.</summary>
public sealed class InMemoryListener : IConnectionListener
{
    private readonly ConcurrentQueue<IConnection> _pending = new();

    public int Port => 0;

    public bool IsDisposed { get; private set; }

    public InMemoryConnection Connect()
    {
        var (client, server) = InMemoryConnection.CreatePair();
        if (IsDisposed)
        {
            client.Close("Connection refused.");
        }
        else
        {
            _pending.Enqueue(server);
        }

        return client;
    }

    public IConnection? TryAccept() => _pending.TryDequeue(out var connection) ? connection : null;

    public void Dispose() => IsDisposed = true;
}