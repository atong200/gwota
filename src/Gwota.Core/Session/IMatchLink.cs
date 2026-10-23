namespace Gwota.Core.Session;

public enum RematchStatus
{
    None,
    RequestedByYou,
    RequestedByOpponent,
}

/// <summary>
/// A match against a remote opponent. Commands go out through <see cref="Send"/> and are applied, in an order both
/// games agree on, by <see cref="ApplyNext"/>.
/// </summary>
public interface IMatchLink : IDisposable
{
    /// <summary>The current match; a new session replaces it when a rematch starts.</summary>
    MatchSession Session { get; }

    Seat LocalSeat { get; }

    /// <summary>False while one of your commands is on its way, commands wait to be applied or the link is closed.</summary>
    bool CanSend { get; }

    bool HasPending { get; }

    /// <summary>Why the link closed, or null while it is open.</summary>
    string? CloseReason { get; }

    /// <summary>True when the link closed because the opponent quit or the connection dropped.</summary>
    bool OpponentLeft { get; }

    RematchStatus Rematch { get; }

    /// <summary>Reads what arrived from the opponent. Call it every frame.</summary>
    void Poll();

    void Send(GameCommand command);

    /// <summary>Applies the next agreed command to <see cref="Session"/>. Returns null when none is waiting.</summary>
    CommandResult? ApplyNext();

    void RequestRematch();

    /// <summary>Tells the opponent you are leaving and closes the link.</summary>
    void Leave();
}