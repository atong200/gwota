namespace Gwota.Core.Cards;

/// <summary>A faction leader: one ability usable once per match instead of playing a card.</summary>
public sealed record LeaderDefinition
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public required Faction Faction { get; init; }

    public required LeaderAbility Ability { get; init; }

    /// <summary>Row parameter for row-based abilities (horn, scorch, weather).</summary>
    public RowType? Row { get; init; }

    public string? Description { get; init; }
}
