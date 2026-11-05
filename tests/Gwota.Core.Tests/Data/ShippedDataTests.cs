using System.Text.Json.Nodes;
using Gwota.Core.Data;
using Gwota.Core.DeckBuilding;

namespace Gwota.Core.Tests.Data;

/// <summary>Validates the real content shipped in <c>game/data</c>: adding a broken card fails the build.</summary>
public class ShippedDataTests
{
    private static readonly Lazy<GameDataLoadResult> Loaded =
        new(() => GameDataLoader.TryLoad(new FileSystemDataSource(RepoPaths.GameData)));

    private static GameData Data => Loaded.Value.Data;

    [Fact]
    public void All_shipped_data_is_valid() =>
        Loaded.Value.Errors.ShouldBeEmpty(string.Join(Environment.NewLine, Loaded.Value.Errors));

    [Fact]
    public void Content_exists_for_every_playable_faction()
    {
        foreach (var faction in FactionInfo.Playable)
        {
            Data.Cards.Values.ShouldContain(c => c.Faction == faction);
            Data.Leaders.Values.ShouldContain(l => l.Faction == faction);
            Data.Decks.Values.ShouldContain(d => d.Faction == faction);
        }
    }

    [Fact]
    public void Every_faction_card_is_used_by_at_least_one_deck()
    {
        var used = Data.Decks.Values.SelectMany(d => d.Cards).Select(e => e.CardId).ToHashSet();

        // Neutral cards are a shared pool for every deck, so starter decks need not use them all.
        Data.Cards.Values.Where(c => c.Faction != Faction.Neutral && !used.Contains(c.Id)).Select(c => c.Id).ShouldBeEmpty();
    }

    [Fact]
    public void Muster_groups_can_pull_more_than_one_card()
    {
        foreach (var group in Data.Cards.Values.Where(c => c.MusterGroup is not null).GroupBy(c => c.MusterGroup))
        {
            (group.Count() > 1 || group.Any(c => !c.IsHero)).ShouldBeTrue($"muster group '{group.Key}' has a single hero");
        }
    }

    [Theory]
    [InlineData("dire_creep", RowType.Melee, 2, UnitAbility.Muster)]
    [InlineData("dire_super_creep", RowType.Melee, 4, UnitAbility.Muster)]
    [InlineData("radiant_creep", RowType.Melee, 2, UnitAbility.TightBond)]
    [InlineData("radiant_super_creep", RowType.Melee, 4, UnitAbility.TightBond)]
    [InlineData("alpha_dire_wolf", RowType.Melee, 4, UnitAbility.MoraleBoost)]
    [InlineData("sapling", RowType.Melee, 1, UnitAbility.Muster)]
    [InlineData("young_treant", RowType.Melee, 2, UnitAbility.Muster)]
    [InlineData("elder_treant", RowType.Melee, 3, UnitAbility.Muster)]
    [InlineData("chestling", RowType.Melee, 2, UnitAbility.Concord)]
    [InlineData("courier", RowType.Melee, 1, UnitAbility.Concord)]
    [InlineData("enchantress", RowType.Ranged, 4, UnitAbility.Medic)]
    public void Neutral_units_have_their_stats(string id, RowType row, int strength, UnitAbility ability)
    {
        var card = Data.Cards[id];

        card.Faction.ShouldBe(Faction.Neutral);
        card.Rows.ShouldBe([row]);
        card.Strength.ShouldBe(strength);
        card.Abilities.ShouldBe([ability]);
        if (ability == UnitAbility.Muster)
        {
            card.MusterGroup.ShouldBe(id);
        }
    }

    [Theory]
    [InlineData("catapult", RowType.Siege, 6)]
    [InlineData("trebuchet", RowType.Siege, 7)]
    [InlineData("animated_armor", RowType.Melee, 4)]
    public void Plain_neutral_units_have_their_stats(string id, RowType row, int strength)
    {
        var card = Data.Cards[id];

        card.Rows.ShouldBe([row]);
        card.Strength.ShouldBe(strength);
        card.Abilities.ShouldBeEmpty();
        card.IsHero.ShouldBeFalse();
    }

    [Fact]
    public void Concord_cards_and_heroes_are_set()
    {
        Data.Cards["chestling"].ConcordDraw.ShouldBe(1);
        Data.Cards["courier"].ConcordDraw.ShouldBe(2);
        Data.Cards["enchantress"].IsHero.ShouldBeTrue();
    }

    [Theory]
    [InlineData("dire_melee_creep")]
    [InlineData("dire_ranged_creep")]
    [InlineData("dire_catapult")]
    [InlineData("radiant_melee_creep")]
    [InlineData("radiant_ranged_creep")]
    [InlineData("radiant_catapult")]
    public void Retired_creeps_are_gone(string id) =>
        Data.Cards.ShouldNotContainKey(id);

    [Theory]
    [InlineData("abyss_starter", "dire_creep", "dire_super_creep")]
    [InlineData("realms_starter", "radiant_creep", "radiant_super_creep")]
    public void Starter_decks_field_three_of_each_creep(string deck, string creep, string superCreep)
    {
        Data.Decks[deck].Cards.ShouldContain(new DeckEntry(creep, 3));
        Data.Decks[deck].Cards.ShouldContain(new DeckEntry(superCreep, 3));
    }

    [Fact]
    public void Weather_leaders_have_matching_weather_in_their_faction_decks()
    {
        foreach (var leader in Data.Leaders.Values.Where(l => l.Ability == LeaderAbility.PlayWeatherFromDeck))
        {
            Data.Cards.Values.ShouldContain(
                c => c.Effect == SpecialEffect.Weather && c.Rows.Contains(leader.Row!.Value),
                $"leader '{leader.Id}' has no {leader.Row} weather card to play");
        }
    }

    [Fact]
    public void Legacy_ids_point_to_shipped_content()
    {
        Data.Leaders.Keys.ShouldNotContain(id => LegacyIds.Leaders.ContainsKey(id));
        LegacyIds.Cards.Values.Where(id => !Data.Cards.ContainsKey(id)).ShouldBeEmpty();
        LegacyIds.Leaders.Values.Where(id => !Data.Leaders.ContainsKey(id)).ShouldBeEmpty();
        LegacyIds.Factions.Values.ShouldAllBe(f => Enum.IsDefined(Enum.Parse<Faction>(f, ignoreCase: true)));
    }

    [Theory]
    [InlineData("realms_starter", "abyss_starter")]
    [InlineData("abyss_starter", "horde_starter")]
    [InlineData("horde_starter", "realms_starter")]
    public void Starter_decks_play_full_ai_matches(string deckOne, string deckTwo)
    {
        var one = Data.CreatePlayerSetup(deckOne, "One");
        var two = Data.CreatePlayerSetup(deckTwo, "Two");
        var wins = new Dictionary<string, int>();

        for (ulong seed = 1; seed <= 40; seed++)
        {
            var run = MatchRunner.Run(one, two, seed);
            var key = run.Outcome.Winner?.ToString() ?? "Draw";
            wins[key] = wins.GetValueOrDefault(key) + 1;
        }

        // A rough balance smoke check: neither starter deck should be hopeless.
        wins.GetValueOrDefault(nameof(Seat.One)).ShouldBeGreaterThan(5);
        wins.GetValueOrDefault(nameof(Seat.Two)).ShouldBeGreaterThan(5);
    }

    [Theory]
    [InlineData("card.schema.json", "faction", typeof(Faction))]
    [InlineData("card.schema.json", "kind", typeof(CardKind))]
    [InlineData("card.schema.json", "rows", typeof(RowType))]
    [InlineData("card.schema.json", "abilities", typeof(UnitAbility))]
    [InlineData("card.schema.json", "effect", typeof(SpecialEffect))]
    [InlineData("leader.schema.json", "ability", typeof(LeaderAbility))]
    [InlineData("leader.schema.json", "row", typeof(RowType))]
    public void Json_schemas_list_every_enum_value(string schemaFile, string property, Type enumType)
    {
        var schema = JsonNode.Parse(File.ReadAllText(Path.Combine(RepoPaths.GameData, "schema", schemaFile)))!;
        var node = schema["properties"]![property]!;
        var values = (node["items"] ?? node)["enum"]!.AsArray().Select(v => v!.GetValue<string>()).ToList();

        var expected = Enum.GetNames(enumType).Select(n => System.Text.Json.JsonNamingPolicy.SnakeCaseLower.ConvertName(n)).ToList();
        if (enumType == typeof(Faction) && schemaFile == "leader.schema.json")
        {
            expected.Remove("neutral");
        }

        values.ShouldBe(expected, ignoreOrder: true);
    }
}
