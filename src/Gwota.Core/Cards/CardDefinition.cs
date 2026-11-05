namespace Gwota.Core.Cards;

/// <summary>
/// Immutable card data (one JSON file per card). <see cref="Rows"/> is where a unit may be placed (several = agile)
/// or where a special applies (weather) or may target (horn).
/// </summary>
public sealed record CardDefinition
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public required Faction Faction { get; init; }

    public required CardKind Kind { get; init; }

    public IReadOnlyList<RowType> Rows { get; init; } = [];

    public int Strength { get; init; }

    public bool IsHero { get; init; }

    public IReadOnlyList<UnitAbility> Abilities { get; init; } = [];

    public SpecialEffect? Effect { get; init; }

    public string? MusterGroup { get; init; }

    /// <summary>Cards each player draws when a unit with <see cref="UnitAbility.Concord"/> is played; 0 otherwise.</summary>
    public int ConcordDraw { get; init; }

    public string? Description { get; init; }

    public bool IsUnit => Kind == CardKind.Unit;

    public bool IsSpecial => Kind == CardKind.Special;

    public bool IsAgile => IsUnit && Rows.Count > 1;

    /// <summary>A unit that effects may target: decoys, medics, scorch and the return-from-board leader skip heroes.</summary>
    public bool IsNonHeroUnit => IsUnit && !IsHero;

    public bool Has(UnitAbility ability) => Abilities.Contains(ability);

    public bool Is(SpecialEffect effect) => Effect == effect;

    /// <summary>Side of the board the card lands on when <paramref name="player"/> plays it: spies go to the opponent.</summary>
    public Seat PlacementSide(Seat player) => Has(UnitAbility.Spy) ? player.Opponent() : player;
}
