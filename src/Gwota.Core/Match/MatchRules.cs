namespace Gwota.Core.Match;

public enum NextRoundStarter
{
    /// <summary>The winner of the previous round plays first.</summary>
    RoundWinner,

    /// <summary>The loser of the previous round plays first.</summary>
    RoundLoser,
}

/// <summary>Tunable rule constants.</summary>
public sealed record MatchRules
{
    public static MatchRules Classic { get; } = new();

    public int StartingHandSize { get; init; } = 10;

    /// <summary>Most cards a player may redraw during the draft phase.</summary>
    public int MaxRedraws { get; init; } = 2;

    public int StartingGems { get; init; } = 2;

    /// <summary>Cards drawn by the player of a spy.</summary>
    public int SpyDrawCount { get; init; } = 2;

    /// <summary>Minimum row total for row-scorch abilities (unit and leader) to trigger.</summary>
    public int RowScorchThreshold { get; init; } = 10;

    /// <summary>Who starts a round after a decisive round. After a tie, the starter alternates.</summary>
    public NextRoundStarter NextRoundStarter { get; init; } = NextRoundStarter.RoundWinner;
}

/// <summary>Everything needed to seat a player: identity, faction, leader and the deck's cards.</summary>
public sealed record PlayerSetup(string Name, Faction Faction, LeaderDefinition Leader, IReadOnlyList<CardDefinition> Deck);
