namespace Gwota.Core.Abilities;

/// <summary>
/// Computes effective unit strength. Heroes are unaffected; other units apply, in order:
/// weather → tight bond → morale boost → horn.
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
            strength = modifier.Apply(strength, state, row, card);
        }

        return strength;
    }

    public static int RowTotal(MatchState state, BoardRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        return row.Cards.Sum(card => Calculate(state, row, card));
    }

    public static int PlayerTotal(MatchState state, Seat seat)
    {
        ArgumentNullException.ThrowIfNull(state);
        return state.Player(seat).Rows.Sum(row => RowTotal(state, row));
    }
}

internal interface IStrengthModifier
{
    int Apply(int strength, MatchState state, BoardRow row, CardInstance card);
}

internal sealed class WeatherModifier : IStrengthModifier
{
    public int Apply(int strength, MatchState state, BoardRow row, CardInstance card) =>
        state.IsWeatherActive(row.Type) ? Math.Min(strength, 1) : strength;
}

internal sealed class TightBondModifier : IStrengthModifier
{
    public int Apply(int strength, MatchState state, BoardRow row, CardInstance card)
    {
        if (!card.Definition.Has(UnitAbility.TightBond))
        {
            return strength;
        }

        var copies = row.Cards.Count(c => c.Definition.Id == card.Definition.Id);
        return strength * copies;
    }
}

internal sealed class MoraleBoostModifier : IStrengthModifier
{
    public int Apply(int strength, MatchState state, BoardRow row, CardInstance card)
    {
        var boosters = row.Cards.Count(c => c.Id != card.Id && c.Definition.Has(UnitAbility.MoraleBoost));
        return strength + boosters;
    }
}

internal sealed class HornModifier : IStrengthModifier
{
    public int Apply(int strength, MatchState state, BoardRow row, CardInstance card)
    {
        var horned = row.HasHornSlotFilled
            || row.Cards.Any(c => c.Id != card.Id && c.Definition.Has(UnitAbility.Horn));
        return horned ? strength * 2 : strength;
    }
}
