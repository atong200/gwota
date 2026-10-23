using Gwota.Core.Data;
using Gwota.Core.DeckBuilding;

namespace Gwota.Core.Tests.DeckBuilding;

public class DeckCollectionTests
{
    private static readonly GameData Data = DeckBuildingData.Create();

    [Fact]
    public void Defaults_copy_the_shipped_decks()
    {
        var collection = DeckCollection.FromDefaults(Data);

        collection.Decks.Select(d => d.Id).ShouldBe(["realms_starter"]);
    }

    [Fact]
    public void New_decks_get_unique_ids_and_names_and_the_faction_default_leader()
    {
        var collection = DeckCollection.FromDefaults(Data);

        var first = collection.Create(Faction.Realms, Data);
        var second = collection.Create(Faction.Realms, Data);
        var hells = collection.Create(Faction.Hells, Data);

        first.Id.ShouldBe("deck_1");
        second.Id.ShouldBe("deck_2");
        first.Name.ShouldBe("New Realms deck");
        second.Name.ShouldBe("New Realms deck 2");
        hells.Name.ShouldBe("New Hells deck");
        first.LeaderId.ShouldBe("abaddon");
        hells.LeaderId.ShouldBe("underlord");
        first.Cards.ShouldBeEmpty();
        collection.Count.ShouldBe(4);
    }

    [Fact]
    public void Neutral_decks_cannot_be_created() =>
        Should.Throw<ArgumentException>(() => DeckCollection.FromDefaults(Data).Create(Faction.Neutral, Data));

    [Fact]
    public void Save_replaces_in_place_or_appends()
    {
        var collection = DeckCollection.FromDefaults(Data);
        var created = collection.Create(Faction.Realms, Data);

        collection.Save(collection.Decks[0] with { Name = "Renamed" });
        collection.Save(created with { Id = "other" });

        collection.Decks.Select(d => d.Name).ShouldBe(["Renamed", "New Realms deck", "New Realms deck"]);
    }

    [Fact]
    public void The_last_deck_cannot_be_deleted()
    {
        var collection = DeckCollection.FromDefaults(Data);
        var created = collection.Create(Faction.Hells, Data);

        collection.Delete("unknown").ShouldBeFalse();
        collection.Delete(created.Id).ShouldBeTrue();
        collection.CanDelete.ShouldBeFalse();
        collection.Delete("realms_starter").ShouldBeFalse();
        collection.Count.ShouldBe(1);
    }

    [Fact]
    public void Duplicate_ids_keep_the_first_deck()
    {
        var deck = DeckBuildingData.EmptyDeck();

        new DeckCollection([deck, deck with { Name = "Copy" }]).Decks.ShouldBe([deck]);
    }
}
