namespace Gwota.Net.Transport;

/// <summary>A two-way stream of text lines. Implementations are safe to poll from one thread while they receive on another.</summary>
public interface IConnection : IDisposable
{
    bool IsClosed { get; }

    /// <summary>Why the connection closed, or null while it is open.</summary>
    string? CloseReason { get; }

    void Send(string line);

    bool TryReceive(out string line);

    void Close(string reason);
}

/// <summary>Accepts incoming connections without blocking.</summary>
public interface IConnectionListener : IDisposable
{
    int Port { get; }

    IConnection? TryAccept();
}