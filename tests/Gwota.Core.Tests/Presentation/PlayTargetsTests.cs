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
}
