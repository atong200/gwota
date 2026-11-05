using Gwota.Net.Transport;

namespace Gwota.Net.Lan;

/// <summary>Joins a host over an open connection. Poll it every frame until <see cref="Match"/> or <see cref="Error"/> is set.</summary>
public sealed class LanGuest : IDisposable
{
    private readonly IConnection _connection;
    private readonly GameData _data;

    public LanGuest(IConnection connection, GameData data, LanIdentity identity, LanPlayer guest)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(guest);
        _connection = connection;
        _data = data;
        var player = guest.Normalized();
        _connection.Send(new HelloMessage(
            NetProtocol.Version, identity.GameVersion, identity.Fingerprint, player.Name, DeckDto.From(player.Deck)));
    }

    public LanMatch? Match { get; private set; }

    /// <summary>Why joining failed, or null while it is still in progress or has succeeded.</summary>
    public string? Error { get; private set; }

    public void Poll()
    {
        if (Match is not null || Error is not null)
        {
            return;
        }

        var closed = _connection.IsClosed;
        while (_connection.TryReceiveMessage(out var message))
        {
            switch (message)
            {
                case RejectMessage reject:
                    Fail(reject.Reason ?? "The host refused to start the match.");
                    return;
                case StartMessage start:
                    if (LanMatch.TryReadSetup(start, _data, out var setup, out var error))
                    {
                        Match = LanMatch.Guest(_connection, _data, setup);
                    }
                    else
                    {
                        _connection.Send(new LeaveMessage(LeaveReason.Quit));
                        Fail(error);
                    }

                    return;
            }
        }

        if (closed)
        {
            Fail("The host closed the connection.");
        }
    }

    /// <summary>Gives up joining; a match already started keeps running.</summary>
    public void Dispose()
    {
        if (Match is null)
        {
            Fail("Cancelled.");
        }
    }

    private void Fail(string reason)
    {
        Error ??= reason;
        _connection.Close(reason);
    }
}
