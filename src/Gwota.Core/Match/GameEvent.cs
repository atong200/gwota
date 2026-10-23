namespace Gwota.Core.Match;

/// <summary>
/// Something that happened in the match. The UI renders from <see cref="MatchState"/> and uses
/// events for feedback (log, animations, sounds).
/// </summary>
public abstract record GameEvent;

public sealed record MatchStarted(Seat FirstPlayer) : GameEvent;

public sealed record CardsDrawn(Seat Seat, IReadOnlyList<int> CardIds) : GameEvent;

/// <summary>A seat ended its draft phase, sending <paramref name="ReturnedCardIds"/> back and drawing <paramref name="DrawnCardIds"/>.</summary>
public sealed record DraftFinished(Seat Seat, IReadOnlyList<int> ReturnedCardIds, IReadOnlyList<int> DrawnCardIds) : GameEvent;

public sealed record RoundStarted(int Round, Seat Starter) : GameEvent;

public sealed record TurnStarted(Seat Seat) : GameEvent;

/// <summary>A card entered play. <paramref name="Side"/> differs from <paramref name="Player"/> for spies.</summary>
public sealed record CardPlayed(Seat Player, CardInstance Card, Seat Side, RowType? Row) : GameEvent;

public sealed record CardsMustered(Seat Player, IReadOnlyList<CardInstance> Cards) : GameEvent;

public sealed record CardRevived(Seat Player, CardInstance Card) : GameEvent;

public sealed record CardsDestroyed(IReadOnlyList<CardInstance> Cards) : GameEvent;

public sealed record CardReturnedToHand(Seat Player, CardInstance Card) : GameEvent;

public sealed record WeatherCleared : GameEvent;

public sealed record LeaderUsed(Seat Player, LeaderDefinition Leader) : GameEvent;

public sealed record ChoiceRequested(PendingChoice Choice) : GameEvent;

public sealed record PlayerPassed(Seat Seat, bool Automatic) : GameEvent;

public sealed record RoundEnded(RoundResult Result) : GameEvent;

public sealed record PerkTriggered(Seat Seat, Faction Faction, string Description) : GameEvent;

public sealed record PlayerForfeited(Seat Seat) : GameEvent;

public sealed record MatchEnded(MatchOutcome Outcome) : GameEvent;
