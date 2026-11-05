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
        var created = collection.Create(Faction.Abyss, Data);
        collection.Save(created with { Cards = [new DeckEntry("abyss_1", 2), new DeckEntry("horn", 1)], CardBackId = "aegis" });

        var restored = DeckCollectionSerializer.Deserialize(DeckCollectionSerializer.Serialize(collection), Data);

        restored.ShouldNotBeNull();
        restored.Decks.Count.ShouldBe(2);
        for (var i = 0; i < 2; i++)
        {
            Describe(restored.Decks[i]).ShouldBe(Describe(collection.Decks[i]));
        }
    }

    private static string Describe(DeckDefinition d) =>
        $"{d.Id}|{d.Name}|{d.Faction}|{d.LeaderId}|{d.CardBackId}|{string.Join(',', d.Cards.Select(e => $"{e.CardId}x{e.Count}"))}";

    [Fact]
    public void The_card_back_is_saved() =>
        DeckCollectionSerializer.Serialize(new DeckCollection([DeckBuildingData.EmptyDeck() with { CardBackId = "red_mist" }]))
            .ShouldContain("\"cardBack\": \"red_mist\"");

    [Theory]
    [InlineData("", "default")]
    [InlineData(", \"cardBack\": null", "default")]
    [InlineData(", \"cardBack\": \"  \"", "default")]
    [InlineData(", \"cardBack\": \" red_mist \"", "red_mist")]
    [InlineData(", \"cardBack\": \"gilded_delta\"", "gilded")]
    public void A_missing_or_blank_card_back_becomes_the_default(string field, string expected) =>
        Only(Current($$"""{"id": "x", "faction": "realms"{{field}}}""")).CardBackId.ShouldBe(expected);

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
    [InlineData("{\"version\": 3, \"decks\": []}")]
    [InlineData("{\"version\": 3, \"decks\": [{\"id\": \"x\", \"faction\": \"neutral\"}]}")]
    [InlineData("{\"version\": 3, \"decks\": [{\"id\": \"x\", \"faction\": \"sentinel\"}]}")]
    [InlineData("{\"version\": 3, \"decks\": [{\"id\": \"x\", \"faction\": \"7\"}]}")]
    [InlineData("{\"version\": 3, \"decks\": [{\"name\": \"no id\", \"faction\": \"hells\"}]}")]
    [InlineData("{\"version\": 3, \"decks\": [{\"id\": \"x\", \"faction\": \"dire\"}]}")]
    [InlineData("{\"version\": 3, \"decks\": [null]}")]
    [InlineData("{\"decks\": [null]}")]
    public void Nothing_usable_yields_null(string? json) =>
        DeckCollectionSerializer.Deserialize(json, Data).ShouldBeNull();

    [Fact]
    public void Unknown_and_off_faction_cards_are_dropped_and_counts_clamped()
    {
        var deck = Only(Current("""
            {"id": "d", "name": "Mine", "faction": "Realms", "leader": "legion_commander",
             "cards": {"realms_1": 9, "realms_hero": 2, "abyss_1": 1, "gone": 1, "neutral_1": 0}}
            """));

        deck.Faction.ShouldBe(Faction.Realms);
        deck.Cards.ShouldBe([new DeckEntry("realms_1", 3), new DeckEntry("realms_hero", 1)]);
    }

    [Fact]
    public void Unknown_or_off_faction_leaders_fall_back_to_the_default()
    {
        Only(Current("""{"id": "d", "name": "Mine", "faction": "realms", "leader": "gone"}""")).LeaderId.ShouldBe("legion_commander");
        Only(Current("""{"id": "d", "name": "Mine", "faction": "realms", "leader": "underlord"}""")).LeaderId.ShouldBe("legion_commander");
    }

    [Fact]
    public void Blank_names_get_a_placeholder() =>
        Only(Current("""{"id": "d", "name": "  ", "faction": "abyss"}""")).Name.ShouldBe("Unnamed deck");

    [Fact]
    public void Unusable_decks_are_skipped_but_the_rest_are_kept()
    {
        var collection = DeckCollectionSerializer.Deserialize(
            Current("""{"id": "bad", "faction": "neutral"}, {"id": "good", "name": "Ok", "faction": "abyss"}"""), Data);

        collection!.Decks.Select(d => d.Id).ShouldBe(["good"]);
    }

    [Theory]
    [InlineData("radiant", Faction.Realms)]
    [InlineData("Dire", Faction.Abyss)]
    public void Version_1_factions_are_renamed(string legacy, Faction expected) =>
        Only($$"""{"version": 1, "decks": [{"id": "d", "name": "Old", "faction": "{{legacy}}"}]}""").Faction.ShouldBe(expected);

    [Theory]
    [InlineData("radiant", "zeus", "luna")]
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
            new[] { TestCards.Leader(LeaderAbility.ClearWeather) with { Id = "luna" } }.ToDictionary(l => l.Id),
            new Dictionary<string, DeckDefinition>());

        var deck = Only("""
            {"version": 1, "decks": [{"id": "d", "name": "Old", "faction": "radiant", "leader": "zeus",
             "cards": {"earthshaker": 2, "dragon_knight": 2, "lina": 1, "tidehunter": 2}}]}
            """, data);

        deck.LeaderId.ShouldBe("luna");
        deck.Cards.ShouldBe([new DeckEntry("dragon_knight", 3), new DeckEntry("lina", 1)]);
    }

    [Theory]
    [InlineData("hells")]
    [InlineData("dire")]
    public void Version_2_factions_are_renamed(string legacy) =>
        Only($$"""{"version": 2, "decks": [{"id": "d", "name": "Old", "faction": "{{legacy}}", "leader": "underlord"}]}""")
            .ShouldSatisfyAllConditions(d => d.Faction.ShouldBe(Faction.Abyss), d => d.LeaderId.ShouldBe("underlord"));

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    public void Card_renames_only_apply_to_version_1_files(int version)
    {
        var data = new GameData(
            new[] { TestCards.Unit(5, RowType.Melee, "axe", Faction.Horde) }.ToDictionary(c => c.Id),
            new[] { TestCards.Leader(LeaderAbility.ClearWeather, faction: Faction.Horde) with { Id = "sorla_khan" } }.ToDictionary(l => l.Id),
            new Dictionary<string, DeckDefinition>());

        Only($$$"""{"version": {{{version}}}, "decks": [{"id": "d", "name": "Mine", "faction": "horde", "leader": "sorla_khan", "cards": {"axe": 1}}]}""", data)
            .Cards.ShouldBe([new DeckEntry("axe", 1)]);
    }

    [Fact]
    public void Older_files_receive_the_shipped_decks_of_new_factions()
    {
        var collection = DeckCollectionSerializer.Deserialize(
            """{"version": 2, "decks": [{"id": "d", "name": "Old", "faction": "hells"}]}""", WithHordeStarter());

        collection!.Decks.Select(d => d.Id).ShouldBe(["d", "horde_starter"]);
    }

    [Fact]
    public void Current_files_are_not_given_new_faction_decks()
    {
        var collection = DeckCollectionSerializer.Deserialize(Current("""{"id": "d", "name": "Mine", "faction": "realms"}"""), WithHordeStarter());

        collection!.Decks.Select(d => d.Id).ShouldBe(["d"]);
    }

    [Fact]
    public void Older_files_do_not_regain_deleted_factions()
    {
        var collection = DeckCollectionSerializer.Deserialize(
            """{"version": 2, "decks": [{"id": "d", "name": "Old", "faction": "hells"}]}""", Data);

        collection!.Decks.Select(d => d.Id).ShouldBe(["d"]);
    }

    private static GameData WithHordeStarter()
    {
        var leader = TestCards.Leader(LeaderAbility.ClearWeather, faction: Faction.Horde) with { Id = "sorla_khan" };
        var deck = new DeckDefinition { Id = "horde_starter", Name = "Warband", Faction = Faction.Horde, LeaderId = leader.Id, Cards = [] };
        return new GameData(
            Data.Cards,
            Data.Leaders.Values.Append(leader).ToDictionary(l => l.Id),
            Data.Decks.Values.Append(deck).ToDictionary(d => d.Id));
    }
}
