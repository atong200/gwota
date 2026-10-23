namespace Gwota.Core.Tests.Abilities;

public class UnitAbilityTests
{
    private readonly Scenario _scenario = new();

    [Fact]
    public void Plain_unit_goes_to_the_chosen_row_and_ends_the_turn()
    {
        var card = _scenario.AddToHand(Seat.One, TestCards.Agile(5));
        var engine = _scenario.Build();

        engine.Play(card, RowType.Ranged);

        _scenario.One.Row(RowType.Ranged).Cards.ShouldContain(card);
        _scenario.One.Hand.ShouldNotContain(card);
        engine.State.ActiveSeat.ShouldBe(Seat.Two);
    }

    [Fact]
    public void Agile_unit_can_be_played_in_each_of_its_rows_only()
    {
        var card = _scenario.AddToHand(Seat.One, TestCards.Agile(5));
        var engine = _scenario.Build();

        engine.GetLegalCommands(Seat.One).OfType<PlayCardCommand>().Where(c => c.CardId == card.Id)
            .Select(c => c.Row).ShouldBe([RowType.Melee, RowType.Ranged], ignoreOrder: true);
        engine.Apply(new PlayCardCommand(Seat.One, card.Id, RowType.Siege)).IsSuccess.ShouldBeFalse();
    }

    [Fact]
    public void Spy_goes_to_the_opponent_side_and_draws_two()
    {
        var spy = _scenario.AddToHand(Seat.One, TestCards.Spy(4));
        var deckCards = Enumerable.Range(0, 3).Select(_ => _scenario.AddToDeck(Seat.One, TestCards.Unit(2))).ToList();
        var engine = _scenario.Build();

        engine.Play(spy);

        _scenario.Two.Row(RowType.Melee).Cards.ShouldContain(spy);
        _scenario.One.Row(RowType.Melee).Cards.ShouldBeEmpty();
        _scenario.One.Hand.ShouldContain(deckCards[0]);
        _scenario.One.Hand.ShouldContain(deckCards[1]);
        _scenario.One.Deck.ShouldBe([deckCards[2]]);
        engine.Score(Seat.Two).ShouldBe(4);
    }

    [Fact]
    public void Spy_with_short_deck_draws_what_is_left()
    {
        var spy = _scenario.AddToHand(Seat.One, TestCards.Spy());
        var last = _scenario.AddToDeck(Seat.One, TestCards.Unit(2));
        var engine = _scenario.Build();

        engine.Play(spy);

        _scenario.One.Hand.ShouldContain(last);
        _scenario.One.Deck.ShouldBeEmpty();
    }

    [Fact]
    public void Medic_requests_a_choice_among_non_hero_units_in_discard()
    {
        var medic = _scenario.AddToHand(Seat.One, TestCards.Medic());
        var unit = _scenario.AddToDiscard(Seat.One, TestCards.Unit(5));
        _scenario.AddToDiscard(Seat.One, TestCards.Hero(10));
        _scenario.AddToDiscard(Seat.One, TestCards.Scorch());
        var engine = _scenario.Build();

        engine.Play(medic);

        engine.State.PendingChoice.ShouldNotBeNull().Options.ShouldBe([unit.Id]);
        engine.State.ActiveSeat.ShouldBe(Seat.One);
        engine.GetLegalCommands(Seat.One).ShouldBe([new ResolveChoiceCommand(Seat.One, unit.Id)]);
        engine.GetLegalCommands(Seat.Two).ShouldBeEmpty();
    }

    [Fact]
    public void Resolving_medic_choice_revives_the_unit_and_ends_the_turn()
    {
        var medic = _scenario.AddToHand(Seat.One, TestCards.Medic());
        var unit = _scenario.AddToDiscard(Seat.One, TestCards.Unit(5, RowType.Siege));
        var engine = _scenario.Build();

        engine.Play(medic);
        var events = engine.Choose(Seat.One, unit);

        _scenario.One.Row(RowType.Siege).Cards.ShouldContain(unit);
        _scenario.One.Discard.ShouldBeEmpty();
        engine.State.PendingChoice.ShouldBeNull();
        engine.State.ActiveSeat.ShouldBe(Seat.Two);
        events.OfType<CardRevived>().ShouldHaveSingleItem().Card.ShouldBe(unit);
    }

    [Fact]
    public void Medic_with_nothing_to_revive_just_ends_the_turn()
    {
        var medic = _scenario.AddToHand(Seat.One, TestCards.Medic());
        var engine = _scenario.Build();

        engine.Play(medic);

        engine.State.PendingChoice.ShouldBeNull();
        engine.State.ActiveSeat.ShouldBe(Seat.Two);
    }

    [Fact]
    public void Revived_medic_chains_into_another_choice()
    {
        var medic = _scenario.AddToHand(Seat.One, TestCards.Medic());
        var secondMedic = _scenario.AddToDiscard(Seat.One, TestCards.Medic());
        var unit = _scenario.AddToDiscard(Seat.One, TestCards.Unit(5));
        var engine = _scenario.Build();

        engine.Play(medic);
        engine.Choose(Seat.One, secondMedic);

        engine.State.PendingChoice.ShouldNotBeNull().Options.ShouldBe([unit.Id]);
        engine.Choose(Seat.One, unit);
        engine.Score(Seat.One).ShouldBe(7);
        engine.State.ActiveSeat.ShouldBe(Seat.Two);
    }

    [Fact]
    public void Revived_spy_goes_to_the_opponent_and_draws()
    {
        var medic = _scenario.AddToHand(Seat.One, TestCards.Medic());
        var spy = _scenario.AddToDiscard(Seat.One, TestCards.Spy());
        _scenario.AddToDeck(Seat.One, TestCards.Unit(2));
        _scenario.AddToDeck(Seat.One, TestCards.Unit(3));
        var engine = _scenario.Build();

        engine.Play(medic);
        engine.Choose(Seat.One, spy);

        _scenario.Two.Row(RowType.Melee).Cards.ShouldContain(spy);
        _scenario.One.Hand.Count.ShouldBe(3);
    }

    [Fact]
    public void Muster_pulls_every_group_member_from_the_deck()
    {
        var first = _scenario.AddToHand(Seat.One, TestCards.Muster(2, "creeps"));
        var meleeCopy = _scenario.AddToDeck(Seat.One, TestCards.Muster(2, "creeps"));
        var rangedCopy = _scenario.AddToDeck(Seat.One, TestCards.Muster(3, "creeps", RowType.Ranged));
        var unrelated = _scenario.AddToDeck(Seat.One, TestCards.Unit(9));
        var engine = _scenario.Build();

        var events = engine.Play(first);

        _scenario.One.Row(RowType.Melee).Cards.ShouldBe([first, meleeCopy]);
        _scenario.One.Row(RowType.Ranged).Cards.ShouldBe([rangedCopy]);
        _scenario.One.Deck.ShouldBe([unrelated]);
        events.OfType<CardsMustered>().ShouldHaveSingleItem().Cards.Count.ShouldBe(2);
    }

    [Fact]
    public void Muster_does_not_pull_from_hand()
    {
        var first = _scenario.AddToHand(Seat.One, TestCards.Muster(2, "creeps"));
        var inHand = _scenario.AddToHand(Seat.One, TestCards.Muster(2, "creeps"));
        var engine = _scenario.Build();

        engine.Play(first);

        _scenario.One.Hand.ShouldContain(inHand);
    }

    [Fact]
    public void Mustered_agile_card_follows_the_played_row_when_possible()
    {
        var first = _scenario.AddToHand(Seat.One, TestCards.Muster(2, "pack", RowType.Ranged));
        var agile = _scenario.AddToDeck(Seat.One, TestCards.Muster(2, "pack") with { Id = "pack_agile", Rows = [RowType.Melee, RowType.Ranged] });
        var engine = _scenario.Build();

        engine.Play(first);

        _scenario.One.Row(RowType.Ranged).Cards.ShouldContain(agile);
    }

    [Fact]
    public void Scorch_row_destroys_the_strongest_enemy_units_in_the_same_row_at_threshold()
    {
        var scorcher = _scenario.AddToHand(Seat.One, TestCards.WithAbility(4, UnitAbility.ScorchRow));
        var strongA = _scenario.AddToBoard(Seat.Two, RowType.Melee, TestCards.Unit(5));
        var strongB = _scenario.AddToBoard(Seat.Two, RowType.Melee, TestCards.Unit(5));
        var weak = _scenario.AddToBoard(Seat.Two, RowType.Melee, TestCards.Unit(2));
        var hero = _scenario.AddToBoard(Seat.Two, RowType.Melee, TestCards.Hero(8));
        var engine = _scenario.Build();

        engine.Play(scorcher);

        _scenario.Two.Row(RowType.Melee).Cards.ShouldBe([weak, hero]);
        _scenario.Two.Discard.ShouldBe([strongA, strongB], ignoreOrder: true);
    }

    [Fact]
    public void Scorch_row_does_nothing_below_threshold()
    {
        var scorcher = _scenario.AddToHand(Seat.One, TestCards.WithAbility(4, UnitAbility.ScorchRow));
        _scenario.AddToBoard(Seat.Two, RowType.Melee, TestCards.Unit(5));
        _scenario.AddToBoard(Seat.Two, RowType.Melee, TestCards.Unit(4));
        var engine = _scenario.Build();

        engine.Play(scorcher);

        _scenario.Two.Row(RowType.Melee).Cards.Count.ShouldBe(2);
    }

    [Fact]
    public void Scorch_row_ignores_other_rows_and_own_side()
    {
        var scorcher = _scenario.AddToHand(Seat.One, TestCards.WithAbility(4, UnitAbility.ScorchRow));
        _scenario.AddToBoard(Seat.Two, RowType.Ranged, TestCards.Unit(12, RowType.Ranged));
        _scenario.AddToBoard(Seat.One, RowType.Melee, TestCards.Unit(12));
        var engine = _scenario.Build();

        engine.Play(scorcher);

        _scenario.Two.Row(RowType.Ranged).Cards.Count.ShouldBe(1);
        _scenario.One.Row(RowType.Melee).Cards.Count.ShouldBe(2);
    }
}
