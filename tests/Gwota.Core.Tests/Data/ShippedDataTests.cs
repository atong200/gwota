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
        foreach (var faction in new[] { Faction.Realms, Faction.Hells })
        {
            Data.Cards.Values.ShouldContain(c => c.Faction == faction);
            Data.Leaders.Values.ShouldContain(l => l.Faction == faction);
            Data.Decks.Values.ShouldContain(d => d.Faction == faction);
        }
    }

    [Fact]
    public void Every_card_is_used_by_at_least_one_deck()
    {
        var used = Data.Decks.Values.SelectMany(d => d.Cards).Select(e => e.CardId).ToHashSet();

        Data.Cards.Keys.Where(id => !used.Contains(id)).ShouldBeEmpty();
    }

    [Fact]
    public void Muster_groups_have_several_members()
    {
        foreach (var group in Data.Cards.Values.Where(c => c.MusterGroup is not null).GroupBy(c => c.MusterGroup))
        {
            group.Count().ShouldBeGreaterThan(1, $"muster group '{group.Key}'");
        }
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
        Data.Cards.Keys.ShouldNotContain(id => LegacyIds.Cards.ContainsKey(id));
        Data.Leaders.Keys.ShouldNotContain(id => LegacyIds.Leaders.ContainsKey(id));
        LegacyIds.Cards.Values.Where(id => !Data.Cards.ContainsKey(id)).ShouldBeEmpty();
        LegacyIds.Leaders.Values.Where(id => !Data.Leaders.ContainsKey(id)).ShouldBeEmpty();
        LegacyIds.Factions.Values.ShouldAllBe(f => Enum.IsDefined(Enum.Parse<Faction>(f, ignoreCase: true)));
    }

    [Fact]
    public void Starter_decks_play_full_ai_matches()
    {
        var realms = Data.CreatePlayerSetup("realms_starter", "Realms");
        var hells = Data.CreatePlayerSetup("hells_starter", "Hells");
        var wins = new Dictionary<string, int>();

        for (ulong seed = 1; seed <= 40; seed++)
        {
            var run = MatchRunner.Run(realms, hells, seed);
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
