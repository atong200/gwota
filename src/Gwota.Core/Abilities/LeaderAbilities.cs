namespace Gwota.Core.Abilities;

/// <summary>Behaviour of a leader ability. A leader is usable once per match, instead of playing a card.</summary>
internal interface ILeaderAbilityHandler
{
    LeaderAbility Ability { get; }

    /// <summary>Every legal way to use the ability now; empty when it cannot be used.</summary>
    IEnumerable<UseLeaderCommand> LegalUses(MatchState state, Seat seat, LeaderDefinition leader);

    void Use(MatchContext context, UseLeaderCommand command, LeaderDefinition leader);
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
        new ScorchLeader(),
        new ReturnFromBoardLeader(),
    }.ToDictionary(h => h.Ability);

    public static ILeaderAbilityHandler Get(LeaderAbility ability) => Handlers[ability];

    public static RowType RequireRow(LeaderDefinition leader) =>
        leader.Row ?? throw new InvalidOperationException($"Leader '{leader.Id}' requires a row for ability {leader.Ability}.");

    public static IEnumerable<UseLeaderCommand> Untargeted(Seat seat, bool usable) =>
        usable ? [new UseLeaderCommand(seat)] : [];
}

internal sealed class ClearWeatherLeader : ILeaderAbilityHandler
{
    public LeaderAbility Ability => LeaderAbility.ClearWeather;

    public IEnumerable<UseLeaderCommand> LegalUses(MatchState state, Seat seat, LeaderDefinition leader) =>
        LeaderAbilities.Untargeted(seat, state.Weather.Count > 0);

    public void Use(MatchContext context, UseLeaderCommand command, LeaderDefinition leader) => context.ClearWeather();
}

internal sealed class PlayWeatherFromDeckLeader : ILeaderAbilityHandler
{
    public LeaderAbility Ability => LeaderAbility.PlayWeatherFromDeck;

    public IEnumerable<UseLeaderCommand> LegalUses(MatchState state, Seat seat, LeaderDefinition leader) =>
        LeaderAbilities.Untargeted(seat, FindWeather(state.Player(seat), leader) is not null);

    public void Use(MatchContext context, UseLeaderCommand command, LeaderDefinition leader)
    {
        var weather = FindWeather(context.Player(command.Seat), leader)
            ?? throw new InvalidOperationException($"Seat {command.Seat} has no matching weather card in the deck.");
        context.AddWeather(command.Seat, context.TakeFromDeck(command.Seat, weather.Id));
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

    public IEnumerable<UseLeaderCommand> LegalUses(MatchState state, Seat seat, LeaderDefinition leader) =>
        LeaderAbilities.Untargeted(seat, !state.Player(seat).Row(LeaderAbilities.RequireRow(leader)).HasHornSlotFilled);

    public void Use(MatchContext context, UseLeaderCommand command, LeaderDefinition leader) =>
        context.Player(command.Seat).Row(LeaderAbilities.RequireRow(leader)).HasLeaderHorn = true;
}

internal sealed class ScorchRowLeader : ILeaderAbilityHandler
{
    public LeaderAbility Ability => LeaderAbility.ScorchRow;

    public IEnumerable<UseLeaderCommand> LegalUses(MatchState state, Seat seat, LeaderDefinition leader) =>
        LeaderAbilities.Untargeted(seat, usable: true);

    public void Use(MatchContext context, UseLeaderCommand command, LeaderDefinition leader) =>
        context.ScorchRow(command.Seat.Opponent(), LeaderAbilities.RequireRow(leader));
}

internal sealed class ReturnFromDiscardLeader : ILeaderAbilityHandler
{
    public LeaderAbility Ability => LeaderAbility.ReturnFromDiscard;

    public IEnumerable<UseLeaderCommand> LegalUses(MatchState state, Seat seat, LeaderDefinition leader) =>
        LeaderAbilities.Untargeted(seat, state.Player(seat) is { Discard.Count: > 0, IsHandFull: false });

    public void Use(MatchContext context, UseLeaderCommand command, LeaderDefinition leader) =>
        context.RequestChoice(command.Seat, ChoiceKind.ReturnFromDiscard, context.Player(command.Seat).DiscardList.Select(c => c.Id).ToList());
}

internal sealed class ScorchLeader : ILeaderAbilityHandler
{
    public LeaderAbility Ability => LeaderAbility.Scorch;

    public IEnumerable<UseLeaderCommand> LegalUses(MatchState state, Seat seat, LeaderDefinition leader) =>
        LeaderAbilities.Untargeted(seat, usable: true);

    public void Use(MatchContext context, UseLeaderCommand command, LeaderDefinition leader) => context.ScorchBattlefield();
}

internal sealed class ReturnFromBoardLeader : ILeaderAbilityHandler
{
    public LeaderAbility Ability => LeaderAbility.ReturnFromBoard;

    public IEnumerable<UseLeaderCommand> LegalUses(MatchState state, Seat seat, LeaderDefinition leader)
    {
        var player = state.Player(seat);
        return player.IsHandFull
            ? []
            : player.Rows.SelectMany(row => row.Cards.Where(c => c.Definition.IsNonHeroUnit).Select(target => new UseLeaderCommand(seat, target.Id)));
    }

    public void Use(MatchContext context, UseLeaderCommand command, LeaderDefinition leader)
    {
        var targetId = command.TargetId ?? throw new InvalidOperationException($"{command} needs a target.");
        context.ReturnToHand(command.Seat, context.TakeFromBoard(command.Seat, targetId));
    }
}
