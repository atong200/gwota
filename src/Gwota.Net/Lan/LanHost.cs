using Gwota.Net.Transport;

namespace Gwota.Net.Lan;

/// <summary>
/// Waits for one guest, checks that both games can play together, then starts the match. Poll it every frame until
/// <see cref="Match"/> is set.
/// </summary>
public sealed class LanHost : IDisposable
{
    public static TimeSpan HelloTimeout { get; } = TimeSpan.FromSeconds(5);

    private readonly IConnectionListener _listener;
    private readonly GameData _data;
    private readonly LanIdentity _identity;
    private readonly LanPlayer _host;
    private readonly Func<ulong> _seeds;
    private readonly TimeProvider _time;
    private readonly List<(IConnection Connection, long Since)> _joining = [];
    private bool _disposed;

    public LanHost(
        IConnectionListener listener,
        GameData data,
        LanIdentity identity,
        LanPlayer host,
        Func<ulong>? seeds = null,
        TimeProvider? time = null)
    {
        ArgumentNullException.ThrowIfNull(listener);
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(host);
        _listener = listener;
        _data = data;
        _identity = identity;
        _host = host.Normalized();
        _seeds = seeds ?? (() => (ulong)Random.Shared.NextInt64());
        _time = time ?? TimeProvider.System;
    }

    public int Port => _listener.Port;

    /// <summary>Set once a guest has joined; the host stops listening at that point.</summary>
    public LanMatch? Match { get; private set; }

    /// <summary>The last guest that was turned away, and why (for display).</summary>
    public string? LastRejection { get; private set; }

    public void Poll()
    {
        if (Match is not null || _disposed)
        {
            return;
        }

        while (_listener.TryAccept() is { } connection)
        {
            _joining.Add((connection, _time.GetTimestamp()));
        }

        for (var i = _joining.Count - 1; i >= 0 && Match is null; i--)
        {
            var (connection, since) = _joining[i];
            if (TryReadHello(connection) is { } hello)
            {
                _joining.RemoveAt(i);
                Admit(connection, hello);
            }
            else if (connection.IsClosed || _time.GetElapsedTime(since) > HelloTimeout)
            {
                _joining.RemoveAt(i);
                connection.Close("No hello received.");
            }
        }

        if (Match is not null)
        {
            StopListening();
        }
    }

    /// <summary>Stops waiting; a match already started keeps running.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        StopListening();
    }

    /// <summary>The reason a guest cannot join, or null when it can.</summary>
    internal string? Check(HelloMessage hello)
    {
        if (hello.Protocol != NetProtocol.Version || hello.GameVersion != _identity.GameVersion)
        {
            return $"Version mismatch: the host runs {_identity.GameVersion}, you run {hello.GameVersion}.";
        }

        if (hello.Fingerprint != _identity.Fingerprint)
        {
            return "Your card data differs from the host's. Both players need the same build.";
        }

        if (hello.Deck is null)
        {
            return "No deck was sent.";
        }

        var problems = DeckValidator.Validate(hello.Deck.ToDefinition("lan_guest"), _data.Cards, _data.Leaders);
        return problems.Count > 0 ? $"Your deck cannot be played: {problems[0]}" : null;
    }

    private static HelloMessage? TryReadHello(IConnection connection)
    {
        while (connection.TryReceive(out var line))
        {
            if (NetCodec.Decode(line) is HelloMessage hello)
            {
                return hello;
            }
        }

        return null;
    }

    private void Admit(IConnection connection, HelloMessage hello)
    {
        if (Check(hello) is { } reason)
        {
            LastRejection = reason;
            connection.Send(NetCodec.Encode(new RejectMessage(reason)));
            connection.Close(reason);
            return;
        }

        var setup = new MatchSetup(
            _host.Deck,
            hello.Deck.ToDefinition("lan_guest"),
            _seeds(),
            _host.Name,
            ProfileSettings.NormalizeUsername(hello.Name));
        connection.Send(NetCodec.Encode(LanMatch.StartFor(setup)));
        Match = LanMatch.Host(connection, _data, setup, _seeds);
    }

    private void StopListening()
    {
        _listener.Dispose();
        foreach (var (connection, _) in _joining)
        {
            connection.Send(NetCodec.Encode(new RejectMessage("The host is no longer waiting for players.")));
            connection.Close("Host stopped.");
        }

        _joining.Clear();
    }
}