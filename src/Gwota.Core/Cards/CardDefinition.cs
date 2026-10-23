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

    public string? Description { get; init; }

    public bool IsUnit => Kind == CardKind.Unit;

    public bool IsSpecial => Kind == CardKind.Special;

    public bool IsAgile => IsUnit && Rows.Count > 1;

    public bool Has(UnitAbility ability) => Abilities.Contains(ability);

    public bool Is(SpecialEffect effect) => Effect == effect;
}
