using Gwota.Core.Presentation;

namespace Gwota.Core.Tests.Presentation;

public class CardMovesTests
{
    private readonly Scenario _scenario = new();

    private static MovePlan Apply(MatchEngine engine, Func<MatchEngine, IReadOnlyList<GameEvent>> command)
    {
        var before = CardMoves.Snapshot(engine.State);
        var events = command(engine);
        return CardMoves.Plan(before, CardMoves.Snapshot(engine.State), events);
    }

    private static CardMove Move(MovePlan plan, CardInstance card) => plan.Moves.Single(m => m.Card.Id == card.Id);

    [Fact]
    public void Snapshot_locates_every_zone()
    {
        var deck = _scenario.AddToDeck(Seat.One, TestCards.Unit(1));
        var discard = _scenario.AddToDiscard(Seat.Two, TestCards.Unit(2));
        var spy = _scenario.AddToBoard(Seat.Two, RowType.Ranged, TestCards.Spy(), owner: Seat.One);
        var horn = _scenario.SetHorn(Seat.One, RowType.Siege);
        var weather = _scenario.AddWeather(TestCards.Weather(RowType.Melee), Seat.Two);

        var snapshot = CardMoves.Snapshot(_scenario.State);

        snapshot[deck.Id].Zone.ShouldBe(CardZone.Deck(Seat.One));
        snapshot[discard.Id].Zone.ShouldBe(CardZone.Discard(Seat.Two));
        snapshot[spy.Id].Zone.ShouldBe(CardZone.OnRow(Seat.Two, RowType.Ranged));
        snapshot[horn.Id].Zone.ShouldBe(CardZone.HornOf(Seat.One, RowType.Siege));
        snapshot[weather.Id].Zone.ShouldBe(CardZone.Weather(Seat.Two));
        snapshot[weather.Id].Card.ShouldBe(weather);
        snapshot.Count.ShouldBe(7);
    }

    [Fact]
    public void Playing_a_unit_moves_it_from_hand_to_its_row()
    {
        var unit = _scenario.AddToHand(Seat.One, TestCards.Unit(4, RowType.Ranged));

        var plan = Apply(_scenario.Build(), e => e.Play(unit));

        plan.Moves.ShouldBe([new CardMove(unit, CardZone.Hand(Seat.One), CardZone.OnRow(Seat.One, RowType.Ranged), MoveStage.EnterPlay)]);
        plan.HoldBeforePiles.ShouldBeFalse();
    }

    [Fact]
    public void A_spy_crosses_to_the_opponent_side_then_its_draws_reach_the_hand()
    {
        var spy = _scenario.AddToHand(Seat.One, TestCards.Spy());
        var first = _scenario.AddToDeck(Seat.One, TestCards.Unit(2));
        var second = _scenario.AddToDeck(Seat.One, TestCards.Unit(3));

        var plan = Apply(_scenario.Build(), e => e.Play(spy));

        Move(plan, spy).To.ShouldBe(CardZone.OnRow(Seat.Two, RowType.Melee));
        plan.InStage(MoveStage.ToHand).Select(m => m.Card).ShouldBe([first, second], ignoreOrder: true);
        plan.InStage(MoveStage.ToHand).ShouldAllBe(m => m.From == CardZone.Deck(Seat.One) && m.To == CardZone.Hand(Seat.One));
        plan.Moves[0].Stage.ShouldBe(MoveStage.EnterPlay);
    }

    [Fact]
    public void Muster_brings_its_group_from_the_deck_in_the_same_stage()
    {
        var played = _scenario.AddToHand(Seat.One, TestCards.Muster(2, "creeps"));
        var pulled = _scenario.AddToDeck(Seat.One, TestCards.Muster(2, "creeps"));

        var plan = Apply(_scenario.Build(), e => e.Play(played));

        plan.InStage(MoveStage.EnterPlay).Select(m => m.Card).ShouldBe([played, pulled], ignoreOrder: true);
        Move(plan, pulled).From.ShouldBe(CardZone.Deck(Seat.One));
    }

    [Fact]
    public void A_revived_unit_comes_back_from_the_discard_pile()
    {
        var medic = _scenario.AddToHand(Seat.One, TestCards.Medic());
        var dead = _scenario.AddToDiscard(Seat.One, TestCards.Unit(5, RowType.Siege));
        var engine = _scenario.Build();
        engine.Play(medic);

        var plan = Apply(engine, e => e.Choose(Seat.One, dead));

        plan.Moves.ShouldBe([new CardMove(dead, CardZone.Discard(Seat.One), CardZone.OnRow(Seat.One, RowType.Siege), MoveStage.EnterPlay)]);
    }

    [Fact]
    public void A_decoy_takes_the_unit_place_and_the_unit_returns_to_hand_last()
    {
        var decoy = _scenario.AddToHand(Seat.One, TestCards.Decoy());
        var unit = _scenario.AddToBoard(Seat.One, RowType.Melee, TestCards.Unit(6));

        var plan = Apply(_scenario.Build(), e => e.Play(decoy, RowType.Melee, unit.Id));

        plan.Moves.ShouldBe([
            new CardMove(decoy, CardZone.Hand(Seat.One), CardZone.OnRow(Seat.One, RowType.Melee), MoveStage.EnterPlay),
            new CardMove(unit, CardZone.OnRow(Seat.One, RowType.Melee), CardZone.Hand(Seat.One), MoveStage.ToHand),
        ]);
    }

    [Fact]
    public void Weather_and_horn_go_to_their_slots()
    {
        var weather = _scenario.AddToHand(Seat.One, TestCards.Weather(RowType.Siege));
        var horn = _scenario.AddToHand(Seat.Two, TestCards.Horn());
        var engine = _scenario.Build();

        Move(Apply(engine, e => e.Play(weather)), weather).To.ShouldBe(CardZone.Weather(Seat.One));
        Move(Apply(engine, e => e.Play(horn, RowType.Ranged)), horn).To.ShouldBe(CardZone.HornOf(Seat.Two, RowType.Ranged));
    }

    [Fact]
    public void A_one_shot_special_is_revealed_then_discarded_with_its_victims()
    {
        var scorch = _scenario.AddToHand(Seat.One, TestCards.Scorch());
        var victim = _scenario.AddToBoard(Seat.Two, RowType.Melee, TestCards.Unit(8));

        var plan = Apply(_scenario.Build(), e => e.Play(scorch));

        plan.InStage(MoveStage.EnterPlay).ShouldBe([new CardMove(scorch, CardZone.Hand(Seat.One), CardZone.Reveal(Seat.One), MoveStage.EnterPlay)]);
        plan.InStage(MoveStage.ToPile).ShouldBe([
            new CardMove(scorch, CardZone.Reveal(Seat.One), CardZone.Discard(Seat.One), MoveStage.ToPile),
            new CardMove(victim, CardZone.OnRow(Seat.Two, RowType.Melee), CardZone.Discard(Seat.Two), MoveStage.ToPile),
        ], ignoreOrder: true);
        plan.HoldBeforePiles.ShouldBeFalse();
    }

    [Fact]
    public void Clear_weather_sends_the_weather_cards_to_their_owners_piles()
    {
        var clear = _scenario.AddToHand(Seat.One, TestCards.ClearWeather());
        var weather = _scenario.AddWeather(TestCards.Weather(RowType.Melee), Seat.Two);

        var plan = Apply(_scenario.Build(), e => e.Play(clear));

        Move(plan, weather).ShouldBe(new CardMove(weather, CardZone.Weather(Seat.Two), CardZone.Discard(Seat.Two), MoveStage.ToPile));
    }

    [Fact]
    public void Ending_a_round_sweeps_the_board_after_a_pause_then_perk_draws_reach_the_hand()
    {
        var scenario = new Scenario(factionOne: Faction.Realms);
        var unit = scenario.AddToBoard(Seat.One, RowType.Melee, TestCards.Unit(5));
        var horn = scenario.SetHorn(Seat.One, RowType.Melee);
        var weather = scenario.AddWeather(TestCards.Weather(RowType.Siege));
        var top = scenario.AddToDeck(Seat.One, TestCards.Unit(3));
        var engine = scenario.Build();
        engine.Pass(Seat.One);

        var plan = Apply(engine, e => e.Pass(Seat.Two));

        plan.HoldBeforePiles.ShouldBeTrue();
        plan.InStage(MoveStage.EnterPlay).ShouldBeEmpty();
        plan.InStage(MoveStage.ToPile).Select(m => m.Card).ShouldBe([unit, horn, weather], ignoreOrder: true);
        plan.InStage(MoveStage.ToHand).ShouldBe([new CardMove(top, CardZone.Deck(Seat.One), CardZone.Hand(Seat.One), MoveStage.ToHand)]);
    }

    [Fact]
    public void A_unit_kept_by_the_hells_perk_does_not_move()
    {
        var scenario = new Scenario(factionOne: Faction.Hells);
        var kept = scenario.AddToBoard(Seat.One, RowType.Melee, TestCards.Unit(5));
        var engine = scenario.Build();
        engine.Pass(Seat.One);

        var plan = Apply(engine, e => e.Pass(Seat.Two));

        plan.IsEmpty.ShouldBeTrue();
        plan.HoldBeforePiles.ShouldBeFalse();
        scenario.One.BoardCards.ShouldBe([kept]);
    }

    [Fact]
    public void Passing_without_ending_the_round_moves_nothing()
    {
        var plan = Apply(_scenario.Build(), e => e.Pass(Seat.One));

        plan.ShouldBe(MovePlan.Empty);
    }

    [Fact]
    public void Stages_are_ordered_enter_play_then_piles_then_hand()
    {
        var spy = _scenario.AddToHand(Seat.One, TestCards.Spy());
        _scenario.AddToDeck(Seat.One, TestCards.Unit(2));

        var plan = Apply(_scenario.Build(), e => e.Play(spy));

        plan.Moves.Select(m => m.Stage).ShouldBeInOrder();
    }
}
