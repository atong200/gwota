namespace Gwota.Core.Tests.Match;

public class MatchContextTests
{
    private static MatchContext ContextFor(Scenario scenario) => new(scenario.State, new MatchRules(), new SeededRandom(1));

    [Fact]
    public void Take_from_a_pile_removes_the_card()
    {
        var scenario = new Scenario();
        var card = scenario.AddToDiscard(Seat.One, TestCards.Unit(3));
        var context = ContextFor(scenario);

        context.TakeFromDiscard(Seat.One, card.Id).ShouldBe(card);

        scenario.One.Discard.ShouldBeEmpty();
    }

    [Fact]
    public void Take_from_a_pile_names_the_pile_when_the_card_is_missing()
    {
        var context = ContextFor(new Scenario());

        Should.Throw<InvalidOperationException>(() => context.TakeFromDeck(Seat.Two, 999)).Message.ShouldContain("deck");
    }

    [Fact]
    public void Take_from_board_ignores_the_other_side()
    {
        var scenario = new Scenario();
        var card = scenario.AddToBoard(Seat.Two, RowType.Ranged, TestCards.Unit(4));
        var context = ContextFor(scenario);

        Should.Throw<InvalidOperationException>(() => context.TakeFromBoard(Seat.One, card.Id));
        context.TakeFromBoard(Seat.Two, card.Id).ShouldBe(card);
        scenario.Two.Row(RowType.Ranged).Cards.ShouldBeEmpty();
    }

    [Fact]
    public void Return_to_hand_keeps_the_owner_of_an_own_card()
    {
        var scenario = new Scenario();
        var card = new CardInstance(500, TestCards.Unit(2), Seat.One);
        var context = ContextFor(scenario);

        context.ReturnToHand(Seat.One, card).ShouldBeSameAs(card);

        scenario.One.Hand.ShouldContain(card);
        context.TakeEvents().ShouldBe([new CardReturnedToHand(Seat.One, card)]);
    }

    [Fact]
    public void Return_to_hand_hands_an_enemy_card_to_the_new_owner()
    {
        var scenario = new Scenario();
        var spy = new CardInstance(500, TestCards.Spy(), Seat.Two);
        var context = ContextFor(scenario);

        var returned = context.ReturnToHand(Seat.One, spy);

        returned.Owner.ShouldBe(Seat.One);
        returned.Id.ShouldBe(spy.Id);
        scenario.One.Hand.ShouldContain(returned);
    }

    [Fact]
    public void Move_deck_to_row_plays_the_card_without_events()
    {
        var scenario = new Scenario();
        var card = scenario.AddToDeck(Seat.One, TestCards.Unit(5, RowType.Siege));
        var context = ContextFor(scenario);

        context.MoveDeckToRow(Seat.One, card, RowType.Siege);

        scenario.One.Deck.ShouldBeEmpty();
        scenario.One.Row(RowType.Siege).Cards.ShouldBe([card]);
        context.TakeEvents().ShouldBeEmpty();
    }

    [Fact]
    public void Add_weather_puts_it_in_play_and_reports_the_play()
    {
        var scenario = new Scenario();
        var weather = new CardInstance(500, TestCards.Weather(RowType.Melee), Seat.Two);
        var context = ContextFor(scenario);

        context.AddWeather(Seat.Two, weather);

        scenario.State.Weather.ShouldBe([weather]);
        context.TakeEvents().ShouldBe([new CardPlayed(Seat.Two, weather, Seat.Two, null)]);
    }

    [Fact]
    public void Find_in_hand_only_looks_at_the_hand()
    {
        var scenario = new Scenario();
        var inHand = scenario.AddToHand(Seat.One, TestCards.Unit(1));
        var inDeck = scenario.AddToDeck(Seat.One, TestCards.Unit(1));

        scenario.One.FindInHand(inHand.Id).ShouldBe(inHand);
        scenario.One.FindInHand(inDeck.Id).ShouldBeNull();
        scenario.Two.FindInHand(inHand.Id).ShouldBeNull();
    }
}
