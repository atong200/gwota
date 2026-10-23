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
        new ScorchRowAbility(),
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

    public static bool CanBeRevived(CardInstance card) => card.Definition.IsUnit && !card.Definition.IsHero;

    public void OnPlay(MatchContext context, Seat player, CardInstance card, RowType row)
    {
        var options = context.Player(player).DiscardList.Where(CanBeRevived).Select(c => c.Id).ToList();
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

        var playerState = context.Player(player);
        var mustered = playerState.DeckList.Where(c => c.Definition.MusterGroup == group).ToList();
        if (mustered.Count == 0)
        {
            return;
        }

        foreach (var musteredCard in mustered)
        {
            playerState.DeckList.Remove(musteredCard);
            var targetRow = musteredCard.Definition.Rows.Contains(row) ? row : musteredCard.Definition.Rows[0];
            playerState.Row(targetRow).CardList.Add(musteredCard);
        }

        context.Emit(new CardsMustered(player, mustered));
    }
}

internal sealed class ScorchRowAbility : IUnitAbilityHandler
{
    public UnitAbility Ability => UnitAbility.ScorchRow;

    public void OnPlay(MatchContext context, Seat player, CardInstance card, RowType row) =>
        context.ScorchRow(player.Opponent(), row);
}
