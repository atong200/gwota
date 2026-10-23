namespace Gwota.Core.Tests.Abilities;

public class LeaderAbilityTests
{
    private static Scenario WithLeader(LeaderAbility ability, RowType? row = null) =>
        new(leaderOne: TestCards.Leader(ability, row));

    private static bool CanUseLeader(MatchEngine engine, Seat seat = Seat.One) =>
        engine.GetLegalCommands(seat).Contains(new UseLeaderCommand(seat));

    [Fact]
    public void Leader_is_usable_once_and_ends_the_turn()
    {
        var scenario = WithLeader(LeaderAbility.ScorchRow, RowType.Melee);
        var engine = scenario.Build();

        var events = engine.UseLeader(Seat.One);

        scenario.One.LeaderUsed.ShouldBeTrue();
        engine.State.ActiveSeat.ShouldBe(Seat.Two);
        events.OfType<LeaderUsed>().ShouldHaveSingleItem();
        engine.Pass(Seat.Two);
        CanUseLeader(engine).ShouldBeFalse();
    }

    [Fact]
    public void Clear_weather_needs_active_weather()
    {
        var scenario = WithLeader(LeaderAbility.ClearWeather);
        CanUseLeader(scenario.Build()).ShouldBeFalse();

        scenario.AddWeather(TestCards.Weather(RowType.Melee), Seat.Two);
        var engine = scenario.Build();
        engine.UseLeader(Seat.One);

        engine.State.Weather.ShouldBeEmpty();
    }

    [Fact]
    public void Play_weather_from_deck_takes_a_matching_weather_card()
    {
        var scenario = WithLeader(LeaderAbility.PlayWeatherFromDeck, RowType.Ranged);
        scenario.AddToDeck(Seat.One, TestCards.Weather(RowType.Melee));
        var fog = scenario.AddToDeck(Seat.One, TestCards.Weather(RowType.Ranged, RowType.Siege));
        var engine = scenario.Build();

        engine.UseLeader(Seat.One);

        engine.State.Weather.ShouldBe([fog]);
        scenario.One.Deck.ShouldNotContain(fog);
    }

    [Fact]
    public void Play_weather_from_deck_needs_a_matching_card()
    {
        var scenario = WithLeader(LeaderAbility.PlayWeatherFromDeck, RowType.Ranged);
        scenario.AddToDeck(Seat.One, TestCards.Weather(RowType.Melee));
        scenario.AddToHand(Seat.One, TestCards.Weather(RowType.Ranged));

        CanUseLeader(scenario.Build()).ShouldBeFalse();
    }

    [Fact]
    public void Horn_row_doubles_its_row_and_fills_the_slot()
    {
        var scenario = WithLeader(LeaderAbility.HornRow, RowType.Siege);
        scenario.AddToBoard(Seat.One, RowType.Siege, TestCards.Unit(6, RowType.Siege));
        var horn = scenario.AddToHand(Seat.One, TestCards.Horn());
        var engine = scenario.Build();

        engine.UseLeader(Seat.One);

        engine.Score(Seat.One).ShouldBe(12);
        engine.Pass(Seat.Two);
        engine.GetLegalCommands(Seat.One).OfType<PlayCardCommand>()
            .Where(c => c.CardId == horn.Id).ShouldNotContain(c => c.Row == RowType.Siege);
    }

    [Fact]
    public void Horn_row_is_unavailable_when_the_slot_is_filled()
    {
        var scenario = WithLeader(LeaderAbility.HornRow, RowType.Siege);
        scenario.SetHorn(Seat.One, RowType.Siege);

        CanUseLeader(scenario.Build()).ShouldBeFalse();
    }

    [Fact]
    public void Scorch_row_hits_the_opponent_row_at_threshold()
    {
        var scenario = WithLeader(LeaderAbility.ScorchRow, RowType.Ranged);
        var strongest = scenario.AddToBoard(Seat.Two, RowType.Ranged, TestCards.Unit(7, RowType.Ranged));
        scenario.AddToBoard(Seat.Two, RowType.Ranged, TestCards.Unit(4, RowType.Ranged));
        var engine = scenario.Build();

        engine.UseLeader(Seat.One);

        scenario.Two.Discard.ShouldBe([strongest]);
    }

    [Fact]
    public void Return_from_discard_requests_a_choice_and_returns_the_card_to_hand()
    {
        var scenario = WithLeader(LeaderAbility.ReturnFromDiscard);
        var scorch = scenario.AddToDiscard(Seat.One, TestCards.Scorch());
        var hero = scenario.AddToDiscard(Seat.One, TestCards.Hero(10));
        var engine = scenario.Build();

        engine.UseLeader(Seat.One);
        engine.State.PendingChoice.ShouldNotBeNull().Options.ShouldBe([scorch.Id, hero.Id], ignoreOrder: true);
        engine.Choose(Seat.One, hero);

        scenario.One.Hand.ShouldContain(hero);
        scenario.One.Discard.ShouldBe([scorch]);
        engine.State.ActiveSeat.ShouldBe(Seat.Two);
    }

    [Fact]
    public void Return_from_discard_needs_a_non_empty_discard() =>
        CanUseLeader(WithLeader(LeaderAbility.ReturnFromDiscard).Build()).ShouldBeFalse();

    [Fact]
    public void Row_based_leader_without_row_is_a_programming_error()
    {
        var engine = WithLeader(LeaderAbility.ScorchRow).Build();

        Should.Throw<InvalidOperationException>(() => engine.UseLeader(Seat.One));
    }
}
