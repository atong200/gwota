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

    [Theory]
    [InlineData(19, true)]
    [InlineData(20, false)]
    public void Return_from_discard_needs_room_in_hand(int handSize, bool usable)
    {
        var scenario = WithLeader(LeaderAbility.ReturnFromDiscard);
        scenario.AddToDiscard(Seat.One, TestCards.Unit(3));
        scenario.FillHand(Seat.One, handSize);

        CanUseLeader(scenario.Build()).ShouldBe(usable);
    }

    [Fact]
    public void Row_based_leader_without_row_is_a_programming_error()
    {
        var engine = WithLeader(LeaderAbility.ScorchRow).Build();

        Should.Throw<InvalidOperationException>(() => engine.UseLeader(Seat.One));
    }

    [Fact]
    public void Scorch_destroys_the_strongest_non_hero_units_on_both_sides()
    {
        var scenario = WithLeader(LeaderAbility.Scorch);
        var mine = scenario.AddToBoard(Seat.One, RowType.Melee, TestCards.Unit(8));
        var theirs = scenario.AddToBoard(Seat.Two, RowType.Siege, TestCards.Unit(8, RowType.Siege));
        var weaker = scenario.AddToBoard(Seat.Two, RowType.Ranged, TestCards.Unit(5, RowType.Ranged));
        var hero = scenario.AddToBoard(Seat.Two, RowType.Melee, TestCards.Hero(10));
        var engine = scenario.Build();

        engine.UseLeader(Seat.One);

        scenario.One.Discard.ShouldBe([mine]);
        scenario.Two.Discard.ShouldBe([theirs]);
        scenario.Two.Row(RowType.Ranged).Cards.ShouldBe([weaker]);
        scenario.Two.Row(RowType.Melee).Cards.ShouldBe([hero]);
    }

    [Fact]
    public void Scorch_is_usable_on_an_empty_board()
    {
        var engine = WithLeader(LeaderAbility.Scorch).Build();

        CanUseLeader(engine).ShouldBeTrue();
        engine.UseLeader(Seat.One);
        engine.State.ActiveSeat.ShouldBe(Seat.Two);
    }

    [Fact]
    public void Return_from_board_targets_each_own_non_hero_unit()
    {
        var scenario = WithLeader(LeaderAbility.ReturnFromBoard);
        var melee = scenario.AddToBoard(Seat.One, RowType.Melee, TestCards.Unit(4));
        var ranged = scenario.AddToBoard(Seat.One, RowType.Ranged, TestCards.Unit(6, RowType.Ranged));
        scenario.AddToBoard(Seat.One, RowType.Siege, TestCards.Hero(10, RowType.Siege));
        scenario.AddToBoard(Seat.Two, RowType.Melee, TestCards.Unit(5));

        var uses = scenario.Build().GetLegalCommands(Seat.One).OfType<UseLeaderCommand>();

        uses.ShouldBe([new UseLeaderCommand(Seat.One, melee.Id), new UseLeaderCommand(Seat.One, ranged.Id)], ignoreOrder: true);
    }

    [Fact]
    public void Return_from_board_moves_the_unit_to_hand_and_ends_the_turn()
    {
        var scenario = WithLeader(LeaderAbility.ReturnFromBoard);
        var unit = scenario.AddToBoard(Seat.One, RowType.Melee, TestCards.Unit(4));
        var engine = scenario.Build();

        var events = engine.ApplyOk(new UseLeaderCommand(Seat.One, unit.Id));

        scenario.One.Row(RowType.Melee).Cards.ShouldBeEmpty();
        scenario.One.Hand.ShouldContain(unit);
        events.OfType<CardReturnedToHand>().ShouldHaveSingleItem().Card.ShouldBe(unit);
        engine.State.ActiveSeat.ShouldBe(Seat.Two);
        engine.Score(Seat.One).ShouldBe(0);
    }

    [Fact]
    public void Return_from_board_takes_an_enemy_spy_into_the_player_hand()
    {
        var scenario = WithLeader(LeaderAbility.ReturnFromBoard);
        var spy = scenario.AddToBoard(Seat.One, RowType.Melee, TestCards.Spy(), owner: Seat.Two);
        var engine = scenario.Build();

        engine.ApplyOk(new UseLeaderCommand(Seat.One, spy.Id));

        scenario.One.Hand.Single(c => c.Id == spy.Id).Owner.ShouldBe(Seat.One);
    }

    [Fact]
    public void Return_from_board_is_unavailable_without_a_target_or_with_a_full_hand()
    {
        var scenario = WithLeader(LeaderAbility.ReturnFromBoard);
        scenario.AddToBoard(Seat.One, RowType.Melee, TestCards.Hero(10));
        CanUseAnyLeader(scenario.Build()).ShouldBeFalse();

        scenario.AddToBoard(Seat.One, RowType.Melee, TestCards.Unit(3));
        CanUseAnyLeader(scenario.Build()).ShouldBeTrue();

        scenario.FillHand(Seat.One, 20);
        CanUseAnyLeader(scenario.Build()).ShouldBeFalse();
    }

    [Fact]
    public void Return_from_board_cannot_target_a_decoy()
    {
        var scenario = WithLeader(LeaderAbility.ReturnFromBoard);
        var decoy = scenario.AddToBoard(Seat.One, RowType.Melee, TestCards.Decoy());
        var engine = scenario.Build();

        CanUseAnyLeader(engine).ShouldBeFalse();
        engine.Apply(new UseLeaderCommand(Seat.One, decoy.Id)).IsSuccess.ShouldBeFalse();
    }

    [Fact]
    public void Return_from_board_rejects_an_untargeted_use()
    {
        var scenario = WithLeader(LeaderAbility.ReturnFromBoard);
        scenario.AddToBoard(Seat.One, RowType.Melee, TestCards.Unit(3));

        scenario.Build().Apply(new UseLeaderCommand(Seat.One)).IsSuccess.ShouldBeFalse();
    }

    private static bool CanUseAnyLeader(MatchEngine engine, Seat seat = Seat.One) =>
        engine.GetLegalCommands(seat).OfType<UseLeaderCommand>().Any();
}
