using Gwota.Core.Data;
using Gwota.Core.DeckBuilding;

namespace Gwota.Core.Tests.DeckBuilding;

public class DeckCollectionSerializerTests
{
    private static readonly GameData Data = DeckBuildingData.Create();

    private static DeckDefinition Only(string json, GameData? data = null) =>
        DeckCollectionSerializer.Deserialize(json, data ?? Data)!.Decks.ShouldHaveSingleItem();

    private static string Current(string decks) => $$"""{"version": {{DeckCollectionSerializer.CurrentVersion}}, "decks": [{{decks}}]}""";

    [Fact]
    public void Collections_round_trip()
    {
        var collection = DeckCollection.FromDefaults(Data);
        var created = collection.Create(Faction.Hells, Data);
        collection.Save(created with { Cards = [new DeckEntry("hells_1", 2), new DeckEntry("horn", 1)] });

        var restored = DeckCollectionSerializer.Deserialize(DeckCollectionSerializer.Serialize(collection), Data);

        restored.ShouldNotBeNull();
        restored.Decks.Count.ShouldBe(2);
        for (var i = 0; i < 2; i++)
        {
            Describe(restored.Decks[i]).ShouldBe(Describe(collection.Decks[i]));
        }
    }

    private static string Describe(DeckDefinition d) =>
        $"{d.Id}|{d.Name}|{d.Faction}|{d.LeaderId}|{string.Join(',', d.Cards.Select(e => $"{e.CardId}x{e.Count}"))}";

    [Fact]
    public void The_file_uses_the_shipped_deck_shape()
    {
        var json = DeckCollectionSerializer.Serialize(DeckCollection.FromDefaults(Data));

        json.ShouldContain($"\"version\": {DeckCollectionSerializer.CurrentVersion}");
        json.ShouldContain("\"faction\": \"realms\"");
        json.ShouldContain("\"leader\": \"legion_commander\"");
        json.ShouldContain("\"realms_1\": 2");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("{\"version\": 2, \"decks\": []}")]
    [InlineData("{\"version\": 2, \"decks\": [{\"id\": \"x\", \"faction\": \"neutral\"}]}")]
    [InlineData("{\"version\": 2, \"decks\": [{\"id\": \"x\", \"faction\": \"sentinel\"}]}")]
    [InlineData("{\"version\": 2, \"decks\": [{\"id\": \"x\", \"faction\": \"7\"}]}")]
    [InlineData("{\"version\": 2, \"decks\": [{\"name\": \"no id\", \"faction\": \"hells\"}]}")]
    [InlineData("{\"version\": 2, \"decks\": [{\"id\": \"x\", \"faction\": \"dire\"}]}")]
    [InlineData("{\"version\": 2, \"decks\": [null]}")]
    [InlineData("{\"decks\": [null]}")]
    public void Nothing_usable_yields_null(string? json) =>
        DeckCollectionSerializer.Deserialize(json, Data).ShouldBeNull();

    [Fact]
    public void Unknown_and_off_faction_cards_are_dropped_and_counts_clamped()
    {
        var deck = Only(Current("""
            {"id": "d", "name": "Mine", "faction": "Realms", "leader": "legion_commander",
             "cards": {"realms_1": 9, "realms_hero": 2, "hells_1": 1, "gone": 1, "neutral_1": 0}}
            """));

        deck.Faction.ShouldBe(Faction.Realms);
        deck.Cards.ShouldBe([new DeckEntry("realms_1", 3), new DeckEntry("realms_hero", 1)]);
    }

    [Fact]
    public void Unknown_or_off_faction_leaders_fall_back_to_the_default()
    {
        Only(Current("""{"id": "d", "name": "Mine", "faction": "realms", "leader": "gone"}""")).LeaderId.ShouldBe("abaddon");
        Only(Current("""{"id": "d", "name": "Mine", "faction": "realms", "leader": "underlord"}""")).LeaderId.ShouldBe("abaddon");
    }

    [Fact]
    public void Blank_names_get_a_placeholder() =>
        Only(Current("""{"id": "d", "name": "  ", "faction": "hells"}""")).Name.ShouldBe("Unnamed deck");

    [Fact]
    public void Unusable_decks_are_skipped_but_the_rest_are_kept()
    {
        var collection = DeckCollectionSerializer.Deserialize(
            Current("""{"id": "bad", "faction": "neutral"}, {"id": "good", "name": "Ok", "faction": "hells"}"""), Data);

        collection!.Decks.Select(d => d.Id).ShouldBe(["good"]);
    }

    [Theory]
    [InlineData("radiant", Faction.Realms)]
    [InlineData("Dire", Faction.Hells)]
    public void Legacy_factions_are_renamed(string legacy, Faction expected) =>
        Only($$"""{"version": 1, "decks": [{"id": "d", "name": "Old", "faction": "{{legacy}}"}]}""").Faction.ShouldBe(expected);

    [Theory]
    [InlineData("radiant", "zeus", "abaddon")]
    [InlineData("dire", "lich", "underlord")]
    public void Legacy_leaders_are_renamed(string faction, string legacy, string expected) =>
        Only($$"""{"version": 1, "decks": [{"id": "d", "name": "Old", "faction": "{{faction}}", "leader": "{{legacy}}"}]}""")
            .LeaderId.ShouldBe(expected);

    [Fact]
    public void Legacy_cards_are_renamed_and_merged_and_retired_cards_dropped()
    {
        var data = new GameData(
            new[]
            {
                TestCards.Unit(6, RowType.Melee, "dragon_knight", Faction.Realms),
                TestCards.Unit(6, RowType.Melee, "lina", Faction.Realms),
            }.ToDictionary(c => c.Id),
            new[] { TestCards.Leader(LeaderAbility.ClearWeather) with { Id = "abaddon" } }.ToDictionary(l => l.Id),
            new Dictionary<string, DeckDefinition>());

        var deck = Only("""
            {"version": 1, "decks": [{"id": "d", "name": "Old", "faction": "radiant", "leader": "zeus",
             "cards": {"earthshaker": 2, "dragon_knight": 2, "lina": 1, "tidehunter": 2}}]}
            """, data);

        deck.LeaderId.ShouldBe("abaddon");
        deck.Cards.ShouldBe([new DeckEntry("dragon_knight", 3), new DeckEntry("lina", 1)]);
    }

    [Fact]
    public void Current_files_are_not_migrated()
    {
        var data = new GameData(
            new[] { TestCards.Unit(5, RowType.Melee, "abaddon", Faction.Hells) }.ToDictionary(c => c.Id),
            new[] { TestCards.Leader(LeaderAbility.ClearWeather, faction: Faction.Hells) with { Id = "underlord" } }.ToDictionary(l => l.Id),
            new Dictionary<string, DeckDefinition>());

        Only(Current("""{"id": "d", "name": "Mine", "faction": "hells", "cards": {"abaddon": 1}}"""), data)
            .Cards.ShouldBe([new DeckEntry("abaddon", 1)]);
    }
}