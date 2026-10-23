using Gwota.Core.Data;
using Gwota.Core.DeckBuilding;

namespace Gwota.Core.Tests.DeckBuilding;

public class DeckLoadResultTests
{
    private static readonly GameData Data = DeckBuildingData.Create();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Without_a_saved_file_the_shipped_decks_are_seeded(string? json)
    {
        var result = DeckLoadResult.From(json, Data);

        result.Status.ShouldBe(DeckLoadStatus.Seeded);
        result.Collection.Decks.Select(d => d.Id).ShouldBe(DeckCollection.FromDefaults(Data).Decks.Select(d => d.Id));
    }

    [Fact]
    public void Saved_decks_are_loaded()
    {
        var saved = DeckCollection.FromDefaults(Data);
        saved.Save(saved.Create(Faction.Hells, Data));

        var result = DeckLoadResult.From(DeckCollectionSerializer.Serialize(saved), Data);

        result.Status.ShouldBe(DeckLoadStatus.Loaded);
        result.Collection.Count.ShouldBe(saved.Count);
    }

    [Theory]
    [InlineData("{ not json")]
    [InlineData("{\"version\":1,\"decks\":[]}")]
    [InlineData("{\"version\":1,\"decks\":[{\"id\":\"x\",\"faction\":\"nowhere\"}]}")]
    public void An_unusable_file_is_reported_as_recovered_with_the_shipped_decks(string json)
    {
        var result = DeckLoadResult.From(json, Data);

        result.Status.ShouldBe(DeckLoadStatus.Recovered);
        result.Collection.Count.ShouldBe(DeckCollection.FromDefaults(Data).Count);
    }
}
