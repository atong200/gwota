namespace Gwota.Core.Abilities;

/// <summary>
/// Computes effective unit strength. Heroes are unaffected; other units apply, in order:
/// weather → tight bond → morale boost → horn. Row-wide facts are gathered once per row, not once per unit.
/// </summary>
public static class StrengthCalculator
{
    private static readonly IStrengthModifier[] Pipeline =
    [
        new WeatherModifier(),
        new TightBondModifier(),
        new MoraleBoostModifier(),
        new HornModifier(),
    ];

    public static int Calculate(MatchState state, BoardRow row, CardInstance card)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(row);
        ArgumentNullException.ThrowIfNull(card);

        var inRow = row.Cards.Any(c => c.Id == card.Id);
        return Calculate(new RowFacts(state, row), card, inRow);
    }

    public static int RowTotal(MatchState state, BoardRow row)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(row);

        var facts = new RowFacts(state, row);
        var total = 0;
        foreach (var card in row.Cards)
        {
            total += Calculate(facts, card, inRow: true);
        }

        return total;
    }

    public static int PlayerTotal(MatchState state, Seat seat)
    {
        ArgumentNullException.ThrowIfNull(state);
        return state.Player(seat).Rows.Sum(row => RowTotal(state, row));
    }

    private static int Calculate(in RowFacts row, CardInstance card, bool inRow)
    {
        var definition = card.Definition;
        if (!definition.IsUnit)
        {
            return 0;
        }

        if (definition.IsHero)
        {
            return definition.Strength;
        }

        var strength = definition.Strength;
        foreach (var modifier in Pipeline)
        {
            strength = modifier.Apply(strength, row, card, inRow);
        }

        return strength;
    }
}

/// <summary>What every unit of a row shares when its strength is computed.</summary>
internal readonly struct RowFacts
{
    public RowFacts(MatchState state, BoardRow row)
    {
        Row = row;
        IsWeatherActive = state.IsWeatherActive(row.Type);
        foreach (var card in row.Cards)
        {
            MoraleBoosters += card.Definition.Has(UnitAbility.MoraleBoost) ? 1 : 0;
            HornUnits += card.Definition.Has(UnitAbility.Horn) ? 1 : 0;
        }
    }

    public BoardRow Row { get; }

    public bool IsWeatherActive { get; }

    /// <summary>Units of the row with the morale boost ability.</summary>
    public int MoraleBoosters { get; }

    /// <summary>Units of the row with the horn ability.</summary>
    public int HornUnits { get; }

    public int CopiesOf(CardDefinition definition)
    {
        var copies = 0;
        foreach (var card in Row.Cards)
        {
            copies += card.Definition.Id == definition.Id ? 1 : 0;
        }

        return copies;
    }
}

/// <summary>One step of the strength pipeline. <paramref name="inRow"/> is true when the card itself sits in the row.</summary>
internal interface IStrengthModifier
{
    int Apply(int strength, in RowFacts row, CardInstance card, bool inRow);
}

internal sealed class WeatherModifier : IStrengthModifier
{
    public int Apply(int strength, in RowFacts row, CardInstance card, bool inRow) =>
        row.IsWeatherActive ? Math.Min(strength, 1) : strength;
}

internal sealed class TightBondModifier : IStrengthModifier
{
    public int Apply(int strength, in RowFacts row, CardInstance card, bool inRow) =>
        card.Definition.Has(UnitAbility.TightBond) ? strength * row.CopiesOf(card.Definition) : strength;
}

/// <summary>Every other morale booster of the row adds one; a booster does not boost itself.</summary>
internal sealed class MoraleBoostModifier : IStrengthModifier
{
    public int Apply(int strength, in RowFacts row, CardInstance card, bool inRow) =>
        strength + row.MoraleBoosters - (inRow && card.Definition.Has(UnitAbility.MoraleBoost) ? 1 : 0);
}

/// <summary>Doubles strength when the horn slot is filled or another unit of the row has the horn ability.</summary>
internal sealed class HornModifier : IStrengthModifier
{
    public int Apply(int strength, in RowFacts row, CardInstance card, bool inRow)
    {
        var otherHornUnits = row.HornUnits - (inRow && card.Definition.Has(UnitAbility.Horn) ? 1 : 0);
        return row.Row.HasHornSlotFilled || otherHornUnits > 0 ? strength * 2 : strength;
    }
}
