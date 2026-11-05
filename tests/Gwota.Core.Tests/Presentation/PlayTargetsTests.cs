using Gwota.Core.Presentation;

namespace Gwota.Core.Tests.Presentation;

public class PlayTargetsTests
{
    private readonly Scenario _scenario = new();

    [Fact]
    public void A_single_row_unit_targets_its_own_row()
    {
        var unit = _scenario.AddToHand(Seat.One, TestCards.Unit(4, RowType.Ranged));
        var targets = PlayTargets.For(_scenario.Build(), Seat.One, unit.Id);

        targets.Rows.ShouldBe([(Seat.One, RowType.Ranged)]);
        targets.Cards.ShouldBeEmpty();
        targets.IsBoardWide.ShouldBeFalse();
        targets.ForRow(Seat.One, RowType.Ranged).ShouldBe(new PlayCardCommand(Seat.One, unit.Id, RowType.Ranged));
        targets.ForRow(Seat.One, RowType.Melee).ShouldBeNull();
        targets.ForRow(Seat.Two, RowType.Ranged).ShouldBeNull();
        targets.ForBoard().ShouldBeNull();
    }

    [Fact]
    public void An_agile_unit_targets_each_of_its_rows()
    {
        var agile = _scenario.AddToHand(Seat.One, TestCards.Agile(3, "agile", RowType.Melee, RowType.Ranged));
        var targets = PlayTargets.For(_scenario.Build(), Seat.One, agile.Id);

        targets.Rows.ShouldBe([(Seat.One, RowType.Melee), (Seat.One, RowType.Ranged)], ignoreOrder: true);
        targets.AcceptsRow(Seat.One, RowType.Siege).ShouldBeFalse();
    }

    [Fact]
    public void A_spy_targets_the_opponent_side()
    {
        var spy = _scenario.AddToHand(Seat.One, TestCards.Spy(row: RowType.Siege));
        var targets = PlayTargets.For(_scenario.Build(), Seat.One, spy.Id);

        targets.Rows.ShouldBe([(Seat.Two, RowType.Siege)]);
        targets.ForRow(Seat.One, RowType.Siege).ShouldBeNull();
        targets.ForRow(Seat.Two, RowType.Siege).ShouldBe(new PlayCardCommand(Seat.One, spy.Id, RowType.Siege));
    }

    [Fact]
    public void A_horn_targets_rows_with_a_free_slot()
    {
        var horn = _scenario.AddToHand(Seat.One, TestCards.Horn());
        _scenario.SetHorn(Seat.One, RowType.Melee);
        var targets = PlayTargets.For(_scenario.Build(), Seat.One, horn.Id);

        targets.Rows.ShouldBe([(Seat.One, RowType.Ranged), (Seat.One, RowType.Siege)], ignoreOrder: true);
    }

    [Fact]
    public void A_decoy_targets_cards_not_rows()
    {
        var decoy = _scenario.AddToHand(Seat.One, TestCards.Decoy());
        var unit = _scenario.AddToBoard(Seat.One, RowType.Ranged, TestCards.Unit(5, RowType.Ranged));
        _scenario.AddToBoard(Seat.One, RowType.Melee, TestCards.Hero(10));
        var enemy = _scenario.AddToBoard(Seat.Two, RowType.Melee, TestCards.Unit(5));
        var targets = PlayTargets.For(_scenario.Build(), Seat.One, decoy.Id);

        targets.Rows.ShouldBeEmpty();
        targets.Cards.ShouldBe([unit.Id]);
        targets.AcceptsCard(enemy.Id).ShouldBeFalse();
        targets.ForCard(unit.Id).ShouldBe(new PlayCardCommand(Seat.One, decoy.Id, RowType.Ranged, unit.Id));
        targets.ForCard(enemy.Id).ShouldBeNull();
        targets.ForRow(Seat.One, RowType.Ranged).ShouldBeNull();
    }

    [Theory]
    [InlineData(SpecialEffect.Weather)]
    [InlineData(SpecialEffect.ClearWeather)]
    [InlineData(SpecialEffect.Scorch)]
    public void Untargeted_specials_are_played_by_any_board_click(SpecialEffect effect)
    {
        var card = _scenario.AddToHand(Seat.One, effect == SpecialEffect.Weather ? TestCards.Weather(RowType.Melee) : TestCards.Special(effect));
        var unit = _scenario.AddToBoard(Seat.Two, RowType.Siege, TestCards.Unit(5, RowType.Siege));
        var targets = PlayTargets.For(_scenario.Build(), Seat.One, card.Id);
        var play = new PlayCardCommand(Seat.One, card.Id);

        targets.IsBoardWide.ShouldBeTrue();
        targets.Rows.ShouldBeEmpty();
        targets.Cards.ShouldBeEmpty();
        targets.ForBoard().ShouldBe(play);
        targets.ForRow(Seat.Two, RowType.Melee).ShouldBe(play);
        targets.ForCard(unit.Id).ShouldBe(play);
    }

    [Fact]
    public void Scorch_affects_every_row_but_not_the_weather_lane()
    {
        var scorch = _scenario.AddToHand(Seat.One, TestCards.Special(SpecialEffect.Scorch));
        var targets = PlayTargets.For(_scenario.Build(), Seat.One, scorch.Id);

        targets.AffectedRows.Count.ShouldBe(6);
        targets.Highlights(Seat.Two, RowType.Siege).ShouldBeTrue();
        targets.AffectsWeatherLane.ShouldBeFalse();
    }

    [Fact]
    public void Weather_affects_its_rows_on_both_sides_and_the_weather_lane()
    {
        var rain = _scenario.AddToHand(Seat.One, TestCards.Weather(RowType.Ranged));
        var targets = PlayTargets.For(_scenario.Build(), Seat.One, rain.Id);

        targets.AffectedRows.ShouldBe([(Seat.One, RowType.Ranged), (Seat.Two, RowType.Ranged)], ignoreOrder: true);
        targets.Highlights(Seat.One, RowType.Melee).ShouldBeFalse();
        targets.AffectsWeatherLane.ShouldBeTrue();
    }

    [Fact]
    public void Weather_on_two_rows_affects_both()
    {
        var storm = _scenario.AddToHand(Seat.One, TestCards.Weather(RowType.Ranged, RowType.Siege));
        var targets = PlayTargets.For(_scenario.Build(), Seat.One, storm.Id);

        targets.AffectedRows.ShouldBe(
            [(Seat.One, RowType.Ranged), (Seat.Two, RowType.Ranged), (Seat.One, RowType.Siege), (Seat.Two, RowType.Siege)],
            ignoreOrder: true);
    }

    [Fact]
    public void Clear_weather_only_affects_the_weather_lane()
    {
        var clear = _scenario.AddToHand(Seat.One, TestCards.Special(SpecialEffect.ClearWeather));
        var targets = PlayTargets.For(_scenario.Build(), Seat.One, clear.Id);

        targets.AffectedRows.ShouldBeEmpty();
        targets.AffectsWeatherLane.ShouldBeTrue();
        targets.ForRow(Seat.One, RowType.Melee).ShouldNotBeNull();
    }

    [Fact]
    public void Targeted_cards_highlight_only_their_target_rows()
    {
        var unit = _scenario.AddToHand(Seat.One, TestCards.Unit(4, RowType.Ranged));
        var horn = _scenario.AddToHand(Seat.One, TestCards.Horn());
        var engine = _scenario.Build();

        var unitTargets = PlayTargets.For(engine, Seat.One, unit.Id);
        unitTargets.AffectedRows.ShouldBeEmpty();
        unitTargets.AffectsWeatherLane.ShouldBeFalse();
        unitTargets.Highlights(Seat.One, RowType.Ranged).ShouldBeTrue();
        unitTargets.Highlights(Seat.One, RowType.Melee).ShouldBeFalse();

        var hornTargets = PlayTargets.For(engine, Seat.One, horn.Id);
        hornTargets.AffectsWeatherLane.ShouldBeFalse();
        hornTargets.Highlights(Seat.Two, RowType.Melee).ShouldBeFalse();
    }

    [Fact]
    public void Nothing_is_targeted_when_it_is_not_the_players_turn()
    {
        var unit = _scenario.AddToHand(Seat.Two, TestCards.Unit(4));
        var targets = PlayTargets.For(_scenario.Build(), Seat.Two, unit.Id);

        targets.IsEmpty.ShouldBeTrue();
        targets.ForBoard().ShouldBeNull();
    }

    [Fact]
    public void Unknown_cards_have_no_targets() =>
        PlayTargets.For(_scenario.Build(), Seat.One, cardId: 999).ShouldBeSameAs(PlayTargets.None);

    [Fact]
    public void Every_resolved_command_is_legal()
    {
        var spy = _scenario.AddToHand(Seat.One, TestCards.Spy());
        var engine = _scenario.Build();
        var targets = PlayTargets.For(engine, Seat.One, spy.Id);

        var (side, row) = targets.Rows.ShouldHaveSingleItem();
        engine.ApplyOk(targets.ForRow(side, row)!);

        _scenario.Two.Row(RowType.Melee).Cards.ShouldContain(c => c.Id == spy.Id);
    }

    [Fact]
    public void Leader_targets_are_empty_when_the_leader_cannot_be_used()
    {
        PlayTargets.ForLeader(_scenario.Build(), Seat.One).ShouldBeSameAs(PlayTargets.None);

        var scenario = new Scenario(leaderTwo: TestCards.Leader(LeaderAbility.HornRow, RowType.Melee));
        PlayTargets.ForLeader(scenario.Build(), Seat.Two).IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public void A_usable_leader_is_used_by_any_board_click()
    {
        var scenario = new Scenario(leaderOne: TestCards.Leader(LeaderAbility.HornRow, RowType.Siege));
        var unit = scenario.AddToBoard(Seat.Two, RowType.Melee, TestCards.Unit(5));
        var engine = scenario.Build();
        var use = new UseLeaderCommand(Seat.One);

        var targets = PlayTargets.ForLeader(engine, Seat.One);

        targets.IsBoardWide.ShouldBeTrue();
        targets.Rows.ShouldBeEmpty();
        targets.Cards.ShouldBeEmpty();
        targets.ForBoard().ShouldBe(use);
        targets.ForRow(Seat.Two, RowType.Ranged).ShouldBe(use);
        targets.ForCard(unit.Id).ShouldBe(use);
        engine.ApplyOk(targets.ForBoard()!);
        scenario.One.LeaderUsed.ShouldBeTrue();
    }

    [Theory]
    [InlineData(LeaderAbility.HornRow, RowType.Siege, true, false)]
    [InlineData(LeaderAbility.ScorchRow, RowType.Melee, false, true)]
    [InlineData(LeaderAbility.PlayWeatherFromDeck, RowType.Ranged, true, true)]
    public void Row_leaders_highlight_the_rows_they_act_on(LeaderAbility ability, RowType row, bool ownRow, bool enemyRow)
    {
        var scenario = new Scenario(leaderOne: TestCards.Leader(ability, row));
        scenario.AddToDeck(Seat.One, TestCards.Weather(row));

        var targets = PlayTargets.ForLeader(scenario.Build(), Seat.One);

        targets.Highlights(Seat.One, row).ShouldBe(ownRow);
        targets.Highlights(Seat.Two, row).ShouldBe(enemyRow);
        targets.AffectedRows.Count.ShouldBe((ownRow ? 1 : 0) + (enemyRow ? 1 : 0));
        targets.AffectsWeatherLane.ShouldBe(ability == LeaderAbility.PlayWeatherFromDeck);
        targets.AffectsOwnDiscard.ShouldBeFalse();
    }

    [Fact]
    public void Clear_weather_leader_highlights_only_the_weather_lane()
    {
        var scenario = new Scenario(leaderOne: TestCards.Leader(LeaderAbility.ClearWeather));
        scenario.AddWeather(TestCards.Weather(RowType.Melee), Seat.Two);

        var targets = PlayTargets.ForLeader(scenario.Build(), Seat.One);

        targets.AffectedRows.ShouldBeEmpty();
        targets.AffectsWeatherLane.ShouldBeTrue();
        targets.AffectsOwnDiscard.ShouldBeFalse();
    }

    [Fact]
    public void Return_from_discard_leader_highlights_the_own_discard_pile()
    {
        var scenario = new Scenario(leaderOne: TestCards.Leader(LeaderAbility.ReturnFromDiscard));
        scenario.AddToDiscard(Seat.One, TestCards.Unit(3));

        var targets = PlayTargets.ForLeader(scenario.Build(), Seat.One);

        targets.IsBoardWide.ShouldBeTrue();
        targets.AffectedRows.ShouldBeEmpty();
        targets.AffectsWeatherLane.ShouldBeFalse();
        targets.AffectsOwnDiscard.ShouldBeTrue();
    }

    [Fact]
    public void Card_targets_never_affect_the_discard_pile()
    {
        var scorch = _scenario.AddToHand(Seat.One, TestCards.Special(SpecialEffect.Scorch));

        PlayTargets.For(_scenario.Build(), Seat.One, scorch.Id).AffectsOwnDiscard.ShouldBeFalse();
    }

    [Fact]
    public void Scorch_leader_highlights_every_row_on_both_sides()
    {
        var scenario = new Scenario(leaderOne: TestCards.Leader(LeaderAbility.Scorch));

        var targets = PlayTargets.ForLeader(scenario.Build(), Seat.One);

        targets.IsBoardWide.ShouldBeTrue();
        targets.AffectedRows.Count.ShouldBe(6);
        targets.AffectsWeatherLane.ShouldBeFalse();
        targets.AffectsOwnDiscard.ShouldBeFalse();
    }

    [Fact]
    public void Return_from_board_leader_targets_own_units_only()
    {
        var scenario = new Scenario(leaderOne: TestCards.Leader(LeaderAbility.ReturnFromBoard));
        var unit = scenario.AddToBoard(Seat.One, RowType.Ranged, TestCards.Unit(4, RowType.Ranged));
        var hero = scenario.AddToBoard(Seat.One, RowType.Melee, TestCards.Hero(10));
        var enemy = scenario.AddToBoard(Seat.Two, RowType.Melee, TestCards.Unit(5));

        var targets = PlayTargets.ForLeader(scenario.Build(), Seat.One);

        targets.IsBoardWide.ShouldBeFalse();
        targets.Cards.ShouldBe([unit.Id]);
        targets.AcceptsCard(hero.Id).ShouldBeFalse();
        targets.ForCard(unit.Id).ShouldBe(new UseLeaderCommand(Seat.One, unit.Id));
        targets.ForCard(enemy.Id).ShouldBeNull();
        targets.ForBoard().ShouldBeNull();
        targets.AffectedRows.ShouldBeEmpty();
    }
}
