using System.Diagnostics.CodeAnalysis;
using Gwota.Net.Transport;

namespace Gwota.Net.Lan;

/// <summary>
/// Waits for one guest, checks that both games can play together, then starts the match. Poll it every frame until
/// <see cref="Match"/> is set.
/// </summary>
public sealed class LanHost : IDisposable
{
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
            else if (connection.IsClosed || _time.GetElapsedTime(since) > NetProtocol.HelloTimeout)
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

    /// <summary>The guest's deck when it can join; otherwise why it cannot.</summary>
    private bool CanJoin(HelloMessage hello, [NotNullWhen(true)] out DeckDefinition? deck, [NotNullWhen(false)] out string? rejection)
    {
        deck = null;
        rejection = hello switch
        {
            { Protocol: var protocol } when protocol != NetProtocol.Version =>
                $"Version mismatch: the host uses network protocol {NetProtocol.Version}, you use {protocol}. Both players need the same build.",
            { GameVersion: var version } when version != _identity.GameVersion =>
                $"Version mismatch: the host runs {_identity.GameVersion}, you run {version}.",
            { Fingerprint: var fingerprint } when fingerprint != _identity.Fingerprint =>
                "Your card data differs from the host's. Both players need the same build.",
            _ => null,
        };
        if (rejection is not null || hello.Deck is not { } sent)
        {
            rejection ??= "No deck was sent.";
            return false;
        }

        deck = sent.ToDefinition(LanMatch.GuestDeckId);
        var problems = DeckValidator.Validate(deck, _data.Cards, _data.Leaders);
        if (problems.Count > 0)
        {
            rejection = $"Your deck cannot be played: {problems[0]}";
            return false;
        }

        return true;
    }

    private static HelloMessage? TryReadHello(IConnection connection)
    {
        while (connection.TryReceiveMessage(out var message))
        {
            if (message is HelloMessage hello)
            {
                return hello;
            }
        }

        return null;
    }

    private void Admit(IConnection connection, HelloMessage hello)
    {
        if (!CanJoin(hello, out var guestDeck, out var rejection))
        {
            LastRejection = rejection;
            connection.SendAndClose(new RejectMessage(rejection), rejection);
            return;
        }

        var setup = new MatchSetup(_host.Deck, guestDeck, _seeds(), _host.Name, ProfileSettings.NormalizeUsername(hello.Name));
        connection.Send(LanMatch.StartFor(setup));
        Match = LanMatch.Host(connection, _data, setup, _seeds);
    }

    private void StopListening()
    {
        _listener.Dispose();
        foreach (var (connection, _) in _joining)
        {
            connection.SendAndClose(new RejectMessage("The host is no longer waiting for players."), "Host stopped.");
        }

        _joining.Clear();
    }
}
