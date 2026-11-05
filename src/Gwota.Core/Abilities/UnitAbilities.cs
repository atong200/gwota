namespace Gwota.Core.Abilities;

/// <summary>
/// An ability that triggers when its unit enters play. Passive abilities (tight bond, morale boost,
/// horn) have no handler: they live in the <see cref="StrengthCalculator"/> pipeline.
/// </summary>
internal interface IUnitAbilityHandler
{
    UnitAbility Ability { get; }

    void OnPlay(MatchContext context, Seat player, CardInstance card, RowType row);
}

internal static class UnitAbilities
{
    private static readonly Dictionary<UnitAbility, IUnitAbilityHandler> Handlers = new IUnitAbilityHandler[]
    {
        new SpyAbility(),
        new MedicAbility(),
        new MusterAbility(),
        new ScorchAbility(),
        new ConcordAbility(),
    }.ToDictionary(h => h.Ability);

    public static IUnitAbilityHandler? Find(UnitAbility ability) => Handlers.GetValueOrDefault(ability);
}

internal sealed class SpyAbility : IUnitAbilityHandler
{
    public UnitAbility Ability => UnitAbility.Spy;

    public void OnPlay(MatchContext context, Seat player, CardInstance card, RowType row) =>
        context.Draw(player, context.Rules.SpyDrawCount);
}

internal sealed class MedicAbility : IUnitAbilityHandler
{
    public UnitAbility Ability => UnitAbility.Medic;

    public void OnPlay(MatchContext context, Seat player, CardInstance card, RowType row)
    {
        var options = context.Player(player).DiscardList.Where(c => c.Definition.IsNonHeroUnit).Select(c => c.Id).ToList();
        if (options.Count > 0)
        {
            context.RequestChoice(player, ChoiceKind.MedicRevive, options);
        }
    }
}

internal sealed class MusterAbility : IUnitAbilityHandler
{
    public UnitAbility Ability => UnitAbility.Muster;

    public void OnPlay(MatchContext context, Seat player, CardInstance card, RowType row)
    {
        var group = card.Definition.MusterGroup;
        if (string.IsNullOrEmpty(group))
        {
            return;
        }

        var mustered = context.Player(player).DeckList.Where(c => c.Definition.MusterGroup == group).ToList();
        if (mustered.Count == 0)
        {
            return;
        }

        foreach (var musteredCard in mustered)
        {
            var targetRow = musteredCard.Definition.Rows.Contains(row) ? row : musteredCard.Definition.Rows[0];
            context.MoveDeckToRow(player, musteredCard, targetRow);
        }

        context.Emit(new CardsMustered(player, mustered));
    }
}

internal sealed class ScorchAbility : IUnitAbilityHandler
{
    public UnitAbility Ability => UnitAbility.Scorch;

    public void OnPlay(MatchContext context, Seat player, CardInstance card, RowType row) => context.ScorchBattlefield();
}

internal sealed class ConcordAbility : IUnitAbilityHandler
{
    public UnitAbility Ability => UnitAbility.Concord;

    public void OnPlay(MatchContext context, Seat player, CardInstance card, RowType row)
    {
        context.Draw(player, card.Definition.ConcordDraw);
        context.Draw(player.Opponent(), card.Definition.ConcordDraw);
    }
}
