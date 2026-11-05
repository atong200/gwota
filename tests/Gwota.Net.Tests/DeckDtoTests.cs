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
        copy.CardBackId.ShouldBe(deck.CardBackId);
    }

    [Fact]
    public void The_card_back_travels_with_the_deck() =>
        DeckDto.From(Shipped.FirstDeck with { CardBackId = "red_mist" }).ToDefinition("remote").CardBackId.ShouldBe("red_mist");

    [Fact]
    public void A_renamed_card_back_from_an_older_peer_gets_its_current_id() =>
        DeckDto.From(Shipped.FirstDeck with { CardBackId = "aegis_seal" }).ToDefinition("remote").CardBackId.ShouldBe("aegis");

    [Fact]
    public void A_missing_card_back_becomes_the_default() =>
        new DeckDto("Old", Faction.Realms, "leader", []).ToDefinition("x").CardBackId.ShouldBe(CardBacks.DefaultId);

    [Fact]
    public void Missing_cards_become_an_empty_list() =>
        new DeckDto("Empty", Faction.Neutral, "leader", null!).ToDefinition("x").Cards.ShouldBeEmpty();
}
