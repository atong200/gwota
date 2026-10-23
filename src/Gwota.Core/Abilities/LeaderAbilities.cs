namespace Gwota.Core.Abilities;

/// <summary>Behaviour of a leader ability. A leader is usable once per match, instead of playing a card.</summary>
internal interface ILeaderAbilityHandler
{
    LeaderAbility Ability { get; }

    bool CanUse(MatchState state, Seat seat, LeaderDefinition leader);

    void Use(MatchContext context, Seat seat, LeaderDefinition leader);
}

internal static class LeaderAbilities
{
    private static readonly Dictionary<LeaderAbility, ILeaderAbilityHandler> Handlers = new ILeaderAbilityHandler[]
    {
        new ClearWeatherLeader(),
        new PlayWeatherFromDeckLeader(),
        new HornRowLeader(),
        new ScorchRowLeader(),
        new ReturnFromDiscardLeader(),
    }.ToDictionary(h => h.Ability);

    public static ILeaderAbilityHandler Get(LeaderAbility ability) => Handlers[ability];

    public static RowType RequireRow(LeaderDefinition leader) =>
        leader.Row ?? throw new InvalidOperationException($"Leader '{leader.Id}' requires a row for ability {leader.Ability}.");
}

internal sealed class ClearWeatherLeader : ILeaderAbilityHandler
{
    public LeaderAbility Ability => LeaderAbility.ClearWeather;

    public bool CanUse(MatchState state, Seat seat, LeaderDefinition leader) => state.Weather.Count > 0;

    public void Use(MatchContext context, Seat seat, LeaderDefinition leader) => context.ClearWeather();
}

internal sealed class PlayWeatherFromDeckLeader : ILeaderAbilityHandler
{
    public LeaderAbility Ability => LeaderAbility.PlayWeatherFromDeck;

    public bool CanUse(MatchState state, Seat seat, LeaderDefinition leader) => FindWeather(state.Player(seat), leader) is not null;

    public void Use(MatchContext context, Seat seat, LeaderDefinition leader)
    {
        var player = context.Player(seat);
        var weather = FindWeather(player, leader)!;
        player.DeckList.Remove(weather);
        context.State.WeatherList.Add(weather);
        context.Emit(new CardPlayed(seat, weather, seat, null));
    }

    private static CardInstance? FindWeather(PlayerState player, LeaderDefinition leader)
    {
        var row = LeaderAbilities.RequireRow(leader);
        return player.DeckList.FirstOrDefault(c => c.Definition.Is(SpecialEffect.Weather) && c.Definition.Rows.Contains(row));
    }
}

internal sealed class HornRowLeader : ILeaderAbilityHandler
{
    public LeaderAbility Ability => LeaderAbility.HornRow;

    public bool CanUse(MatchState state, Seat seat, LeaderDefinition leader) =>
        !state.Player(seat).Row(LeaderAbilities.RequireRow(leader)).HasHornSlotFilled;

    public void Use(MatchContext context, Seat seat, LeaderDefinition leader) =>
        context.Player(seat).Row(LeaderAbilities.RequireRow(leader)).HasLeaderHorn = true;
}

internal sealed class ScorchRowLeader : ILeaderAbilityHandler
{
    public LeaderAbility Ability => LeaderAbility.ScorchRow;

    public bool CanUse(MatchState state, Seat seat, LeaderDefinition leader) => true;

    public void Use(MatchContext context, Seat seat, LeaderDefinition leader) =>
        context.ScorchRow(seat.Opponent(), LeaderAbilities.RequireRow(leader));
}

internal sealed class ReturnFromDiscardLeader : ILeaderAbilityHandler
{
    public LeaderAbility Ability => LeaderAbility.ReturnFromDiscard;

    public bool CanUse(MatchState state, Seat seat, LeaderDefinition leader) => state.Player(seat).Discard.Count > 0;

    public void Use(MatchContext context, Seat seat, LeaderDefinition leader) =>
        context.RequestChoice(seat, ChoiceKind.ReturnFromDiscard, context.Player(seat).DiscardList.Select(c => c.Id).ToList());
}
