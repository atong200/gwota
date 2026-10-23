namespace Gwota.Core.Abilities;

/// <summary>Behaviour of a special card: which plays are legal and what resolving one does.</summary>
internal interface ISpecialEffectHandler
{
    SpecialEffect Effect { get; }

    IEnumerable<PlayCardCommand> LegalPlays(MatchState state, Seat seat, CardInstance card);

    void Resolve(MatchContext context, PlayCardCommand command, CardInstance card);
}

internal static class SpecialEffects
{
    private static readonly Dictionary<SpecialEffect, ISpecialEffectHandler> Handlers = new ISpecialEffectHandler[]
    {
        new HornEffect(),
        new ScorchEffect(),
        new DecoyEffect(),
        new WeatherEffect(),
        new ClearWeatherEffect(),
    }.ToDictionary(h => h.Effect);

    public static ISpecialEffectHandler Get(SpecialEffect effect) => Handlers[effect];
}

internal sealed class HornEffect : ISpecialEffectHandler
{
    private static readonly RowType[] AllRows = Enum.GetValues<RowType>();

    public SpecialEffect Effect => SpecialEffect.Horn;

    public IEnumerable<PlayCardCommand> LegalPlays(MatchState state, Seat seat, CardInstance card)
    {
        var rows = card.Definition.Rows.Count > 0 ? card.Definition.Rows : AllRows;
        return rows
            .Where(row => !state.Player(seat).Row(row).HasHornSlotFilled)
            .Select(row => new PlayCardCommand(seat, card.Id, row));
    }

    public void Resolve(MatchContext context, PlayCardCommand command, CardInstance card)
    {
        var row = command.Row!.Value;
        context.Player(command.Seat).Row(row).HornCard = card;
        context.Emit(new CardPlayed(command.Seat, card, command.Seat, row));
    }
}

internal sealed class ScorchEffect : ISpecialEffectHandler
{
    public SpecialEffect Effect => SpecialEffect.Scorch;

    public IEnumerable<PlayCardCommand> LegalPlays(MatchState state, Seat seat, CardInstance card) =>
        [new PlayCardCommand(seat, card.Id)];

    public void Resolve(MatchContext context, PlayCardCommand command, CardInstance card)
    {
        context.Emit(new CardPlayed(command.Seat, card, command.Seat, null));
        context.ScorchStrongest(context.UnitsOnBoard(Seat.One).Concat(context.UnitsOnBoard(Seat.Two)));
        context.Discard(card);
    }
}

internal sealed class DecoyEffect : ISpecialEffectHandler
{
    public SpecialEffect Effect => SpecialEffect.Decoy;

    public static bool CanBeDecoyed(CardInstance card) => card.Definition.IsUnit && !card.Definition.IsHero;

    public IEnumerable<PlayCardCommand> LegalPlays(MatchState state, Seat seat, CardInstance card) =>
        state.Player(seat).Rows.SelectMany(row => row.Cards
            .Where(CanBeDecoyed)
            .Select(target => new PlayCardCommand(seat, card.Id, row.Type, target.Id)));

    public void Resolve(MatchContext context, PlayCardCommand command, CardInstance card)
    {
        var row = context.Player(command.Seat).Row(command.Row!.Value);
        var index = row.CardList.FindIndex(c => c.Id == command.TargetId);
        var target = row.CardList[index];
        row.CardList[index] = card;

        // A decoyed enemy spy now belongs to the decoy's player.
        var returned = target with { Owner = command.Seat };
        context.Player(command.Seat).HandList.Add(returned);

        context.Emit(new CardPlayed(command.Seat, card, command.Seat, row.Type));
        context.Emit(new CardReturnedToHand(command.Seat, returned));
    }
}

internal sealed class WeatherEffect : ISpecialEffectHandler
{
    public SpecialEffect Effect => SpecialEffect.Weather;

    public IEnumerable<PlayCardCommand> LegalPlays(MatchState state, Seat seat, CardInstance card) =>
        [new PlayCardCommand(seat, card.Id)];

    public void Resolve(MatchContext context, PlayCardCommand command, CardInstance card)
    {
        context.State.WeatherList.Add(card);
        context.Emit(new CardPlayed(command.Seat, card, command.Seat, null));
    }
}

internal sealed class ClearWeatherEffect : ISpecialEffectHandler
{
    public SpecialEffect Effect => SpecialEffect.ClearWeather;

    public IEnumerable<PlayCardCommand> LegalPlays(MatchState state, Seat seat, CardInstance card) =>
        [new PlayCardCommand(seat, card.Id)];

    public void Resolve(MatchContext context, PlayCardCommand command, CardInstance card)
    {
        context.Emit(new CardPlayed(command.Seat, card, command.Seat, null));
        context.ClearWeather();
        context.Discard(card);
    }
}
