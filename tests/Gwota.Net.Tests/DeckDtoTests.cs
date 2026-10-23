namespace Gwota.Net.Tests;

public class DeckDtoTests
{
    [Fact]
    public void A_deck_survives_a_round_trip_under_a_new_id()
    {
        var deck = Shipped.FirstDeck;

        var copy = DeckDto.From(deck).ToDefinition("remote");

        copy.Id.ShouldBe("remote");
        copy.Name.ShouldBe(deck.Name);
        copy.Faction.ShouldBe(deck.Faction);
        copy.LeaderId.ShouldBe(deck.LeaderId);
        copy.Cards.ShouldBe(deck.Cards);
    }

    [Fact]
    public void Missing_cards_become_an_empty_list() =>
        new DeckDto("Empty", Faction.Neutral, "leader", null!).ToDefinition("x").Cards.ShouldBeEmpty();
}