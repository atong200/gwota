using Gwota.Net.Transport;

namespace Gwota.Net.Lan;

/// <summary>
/// A running LAN match. The host decides the order of commands: it applies each one, then relays it with its state
/// checksum. The guest sends its commands to the host and applies only what the host relays, checking the checksum.
/// </summary>
public sealed class LanMatch : IMatchLink
{
    public const string OpponentLeftReason = "Your opponent left the match.";
    public const string ConnectionLostReason = "The connection to your opponent was lost.";
    public const string DesyncReason = "The two games went out of sync, so the match was stopped.";
    public const string YouLeftReason = "You left the match.";

    private readonly IConnection _connection;
    private readonly GameData _data;
    private readonly Func<ulong> _seeds;
    private readonly Queue<Pending> _pending = new();
    private int _sequence;
    private bool _awaitingOwn;
    private bool _localRematch;
    private bool _remoteRematch;

    private LanMatch(IConnection connection, GameData data, MatchSetup setup, bool isHost, Func<ulong> seeds)
    {
        _connection = connection;
        _data = data;
        _seeds = seeds;
        IsHost = isHost;
        Setup = setup;
        Session = MatchFactory.VersusPlayer(data, setup);
    }

    public bool IsHost { get; }

    public Seat LocalSeat => IsHost ? Seat.One : Seat.Two;

    public Seat OpponentSeat => LocalSeat.Opponent();

    public MatchSetup Setup { get; private set; }

    public MatchSession Session { get; private set; }

    /// <summary>1 for the first match, increased by every rematch.</summary>
    public int MatchNumber { get; private set; } = 1;

    public string? CloseReason { get; private set; }

    public bool OpponentLeft { get; private set; }

    public bool HasPending => _pending.Count > 0;

    public bool CanSend => CloseReason is null && !HasPending && !_awaitingOwn && !Session.IsFinished;

    public RematchStatus Rematch =>
        _localRematch ? RematchStatus.RequestedByYou
        : _remoteRematch ? RematchStatus.RequestedByOpponent
        : RematchStatus.None;

    internal static LanMatch Host(IConnection connection, GameData data, MatchSetup setup, Func<ulong> seeds) =>
        new(connection, data, setup, isHost: true, seeds);

    internal static LanMatch Guest(IConnection connection, GameData data, MatchSetup setup) =>
        new(connection, data, setup, isHost: false, () => 0);

    internal static StartMessage StartFor(MatchSetup setup) =>
        new(setup.Seed, setup.PlayerName, DeckDto.From(setup.PlayerDeck), setup.OpponentName, DeckDto.From(setup.OpponentDeck));

    /// <summary>The setup a start message describes, or the reason it cannot be played with <paramref name="data"/>.</summary>
    internal static (MatchSetup? Setup, string? Error) SetupFrom(StartMessage start, GameData data)
    {
        var host = start.HostDeck?.ToDefinition("lan_host");
        var guest = start.GuestDeck?.ToDefinition("lan_guest");
        if (host is null || guest is null
            || DeckValidator.Validate(host, data.Cards, data.Leaders).Count > 0
            || DeckValidator.Validate(guest, data.Cards, data.Leaders).Count > 0)
        {
            return (null, "The host sent decks this game cannot play.");
        }

        return (new MatchSetup(host, guest, start.Seed, start.HostName ?? "Host", start.GuestName ?? "Guest"), null);
    }

    public void Poll()
    {
        Drain();
        if (CloseReason is null && _connection.IsClosed)
        {
            // Lines read just before the close are still waiting.
            Drain();
            if (CloseReason is null)
            {
                OpponentGone(ConnectionLostReason);
            }
        }
    }

    public void Send(GameCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.Seat != LocalSeat)
        {
            throw new ArgumentException($"This game plays seat {LocalSeat}, not {command.Seat}.", nameof(command));
        }

        if (CloseReason is not null || Session.IsFinished)
        {
            return;
        }

        if (IsHost)
        {
            _pending.Enqueue(new Pending(command, PendingKind.Candidate));
        }
        else
        {
            _awaitingOwn = true;
            Write(new CommandMessage(CommandDto.From(command)));
        }
    }

    public CommandResult? ApplyNext()
    {
        if (!_pending.TryDequeue(out var next))
        {
            return null;
        }

        return next.Kind switch
        {
            PendingKind.LocalOnly => Session.Submit(next.Command),
            PendingKind.Candidate => ApplyAsHost(next),
            _ => ApplyAsGuest(next),
        };
    }

    public void RequestRematch()
    {
        if (CloseReason is not null || !Session.IsFinished || _localRematch)
        {
            return;
        }

        _localRematch = true;
        Write(new RematchMessage());
        StartRematchIfAgreed();
    }

    public void Leave()
    {
        if (CloseReason is not null)
        {
            return;
        }

        Write(new LeaveMessage(LeaveReason.Quit));
        Close(YouLeftReason);
    }

    public void Dispose() => Leave();

    private void Drain()
    {
        while (CloseReason is null && _connection.TryReceive(out var line))
        {
            if (NetCodec.Decode(line) is { } message)
            {
                Handle(message);
            }
        }
    }

    private CommandResult ApplyAsHost(Pending next)
    {
        var result = Session.Submit(next.Command);
        if (!result.IsSuccess)
        {
            if (next.Command.Seat == OpponentSeat)
            {
                Write(new RefusedMessage(result.Error ?? "Illegal command."));
            }

            return result;
        }

        Write(new AppliedMessage(++_sequence, CommandDto.From(next.Command), Session.Checksum()));
        return result;
    }

    private CommandResult ApplyAsGuest(Pending next)
    {
        if (next.Command.Seat == LocalSeat)
        {
            _awaitingOwn = false;
        }

        var result = Session.Submit(next.Command);
        if (!result.IsSuccess || Session.Checksum() != next.Checksum)
        {
            Desync();
        }

        return result;
    }

    private void Handle(NetMessage message)
    {
        switch (message)
        {
            case CommandMessage { Command: var dto } when IsHost:
                if (dto?.ToCommand() is { } command && command.Seat == OpponentSeat)
                {
                    _pending.Enqueue(new Pending(command, PendingKind.Candidate));
                }
                else
                {
                    Write(new RefusedMessage("Unreadable command."));
                }

                break;
            case AppliedMessage applied when !IsHost:
                if (applied.Sequence != ++_sequence || applied.Command?.ToCommand() is not { } relayed)
                {
                    Desync();
                    break;
                }

                _pending.Enqueue(new Pending(relayed, PendingKind.Sequenced, applied.Checksum));
                break;
            case RefusedMessage when !IsHost:
                _awaitingOwn = false;
                break;
            case RematchMessage:
                if (Session.IsFinished)
                {
                    _remoteRematch = true;
                    StartRematchIfAgreed();
                }

                break;
            case StartMessage start when !IsHost:
                StartGuestRematch(start);
                break;
            case LeaveMessage { Reason: LeaveReason.Desync }:
                Close(DesyncReason);
                break;
            case LeaveMessage:
                OpponentGone(OpponentLeftReason);
                break;
        }
    }

    private void StartRematchIfAgreed()
    {
        if (!IsHost || !_localRematch || !_remoteRematch)
        {
            return;
        }

        Setup = Setup with { Seed = _seeds() };
        Write(StartFor(Setup));
        Restart();
    }

    private void StartGuestRematch(StartMessage start)
    {
        if (!Session.IsFinished)
        {
            Desync();
            return;
        }

        var (setup, error) = SetupFrom(start, _data);
        if (setup is null)
        {
            Write(new LeaveMessage(LeaveReason.Quit));
            Close(error!);
            return;
        }

        Setup = setup;
        Restart();
    }

    private void Restart()
    {
        Session = MatchFactory.VersusPlayer(_data, Setup);
        MatchNumber++;
        _sequence = 0;
        _pending.Clear();
        _awaitingOwn = false;
        _localRematch = false;
        _remoteRematch = false;
    }

    /// <summary>The opponent is gone: an unfinished match is won by forfeit, applied locally.</summary>
    private void OpponentGone(string reason)
    {
        OpponentLeft = true;
        Close(reason);
        if (!Session.IsFinished)
        {
            if (!IsHost)
            {
                // Commands the host relayed are kept; the one still on its way is lost with the host.
                _awaitingOwn = false;
            }
            else
            {
                var own = _pending.Where(p => p.Command.Seat == LocalSeat).ToList();
                _pending.Clear();
                own.ForEach(_pending.Enqueue);
            }

            _pending.Enqueue(new Pending(new ForfeitCommand(OpponentSeat), PendingKind.LocalOnly));
        }
    }

    private void Desync()
    {
        Write(new LeaveMessage(LeaveReason.Desync));
        Close(DesyncReason);
        _pending.Clear();
    }

    private void Close(string reason)
    {
        CloseReason ??= reason;
        _connection.Close(reason);
    }

    private void Write(NetMessage message)
    {
        if (!_connection.IsClosed)
        {
            _connection.Send(NetCodec.Encode(message));
        }
    }

    private enum PendingKind
    {
        /// <summary>Host side: a command not yet validated or relayed.</summary>
        Candidate,

        /// <summary>Guest side: a command relayed by the host, with the host's checksum after it.</summary>
        Sequenced,

        /// <summary>Applied here only (the opponent is gone).</summary>
        LocalOnly,
    }

    private sealed record Pending(GameCommand Command, PendingKind Kind, ulong Checksum = 0);
}