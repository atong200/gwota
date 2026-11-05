using Gwota.Core.Data;

namespace Gwota.Core.Tests.Data;

public class GameDataLoaderTests
{
    private const string Leader = """
        { "id": "lich", "name": "Lich", "faction": "abyss", "ability": "play_weather_from_deck", "row": "melee", "abilityName": "Chain Frost" }
        """;

    private static InMemoryDataSource ValidSource()
    {
        var source = new InMemoryDataSource().Add("leaders/lich.json", Leader);
        source.Add("cards/abyss/ghoul.json", """{ "id": "ghoul", "name": "Ghoul", "faction": "abyss", "kind": "unit", "rows": ["melee"], "strength": 3 }""");
        source.Add("cards/neutral/frost.json", """{ "id": "frost", "name": "Frost", "faction": "neutral", "kind": "special", "effect": "weather", "rows": ["melee"] }""");
        source.Add("decks/abyss_starter.json", """
            { "id": "abyss_starter", "name": "Abyss", "faction": "abyss", "leader": "lich", "cards": { "ghoul": 3, "frost": 2 } }
            """);
        return source;
    }

    private static IReadOnlyList<string> Errors(string path, string json, DeckRules? rules = null) =>
        GameDataLoader.TryLoad(new InMemoryDataSource().Add("leaders/lich.json", Leader).Add(path, json), rules).Errors;

    [Fact]
    public void Loads_cards_leaders_and_decks()
    {
        var result = GameDataLoader.TryLoad(ValidSource(), new DeckRules { MinUnits = 3 });

        result.Errors.ShouldBeEmpty();
        var ghoul = result.Data.Cards["ghoul"];
        ghoul.ShouldBe(ghoul with { Name = "Ghoul", Faction = Faction.Abyss, Kind = CardKind.Unit, Strength = 3 });
        ghoul.Rows.ShouldBe([RowType.Melee]);
        result.Data.Cards["frost"].Effect.ShouldBe(SpecialEffect.Weather);
        result.Data.Leaders["lich"].Row.ShouldBe(RowType.Melee);
        result.Data.Leaders["lich"].AbilityName.ShouldBe("Chain Frost");
        result.Data.Decks["abyss_starter"].Cards.ShouldBe([new DeckEntry("ghoul", 3), new DeckEntry("frost", 2)]);
    }

    [Fact]
    public void Loads_the_concord_draw_count()
    {
        var source = ValidSource().Add(
            "cards/neutral/courier.json",
            """{ "id": "courier", "name": "Courier", "faction": "neutral", "kind": "unit", "rows": ["melee"], "strength": 1, "abilities": ["concord"], "concord": 2 }""");

        var result = GameDataLoader.TryLoad(source, new DeckRules { MinUnits = 3 });

        result.Errors.ShouldBeEmpty();
        result.Data.Cards["courier"].ConcordDraw.ShouldBe(2);
        result.Data.Cards["ghoul"].ConcordDraw.ShouldBe(0);
    }
    [Fact]
    public void Accepts_backslash_separated_paths_from_the_source()
    {
        var result = GameDataLoader.TryLoad(new BackslashSource(ValidSource()), new DeckRules { MinUnits = 3 });

        result.Errors.ShouldBeEmpty();
        result.Data.Cards.Keys.ShouldBe(["frost", "ghoul"], ignoreOrder: true);
    }

    [Fact]
    public void Creates_player_setups_from_decks()
    {
        var data = GameDataLoader.TryLoad(ValidSource(), new DeckRules { MinUnits = 3 }).Data;

        var setup = data.CreatePlayerSetup("abyss_starter", "Bot");

        setup.Faction.ShouldBe(Faction.Abyss);
        setup.Leader.Id.ShouldBe("lich");
        setup.Deck.Count.ShouldBe(5);
        Should.Throw<KeyNotFoundException>(() => data.CreatePlayerSetup("missing", "Bot"));
    }

    [Fact]
    public void Load_throws_with_every_error()
    {
        var source = ValidSource();

        var ex = Should.Throw<GameDataException>(() => GameDataLoader.Load(source));

        ex.Errors.ShouldContain(e => e.Contains("abyss_starter") && e.Contains("at least 22"));
    }

    [Fact]
    public void Accepts_schema_reference_and_comments()
    {
        Errors("cards/neutral/frost.json", """
            {
              "$schema": "../../schema/card.schema.json",
              // a comment
              "id": "frost", "name": "Frost", "faction": "neutral", "kind": "special", "effect": "weather", "rows": ["melee"],
            }
            """).ShouldBeEmpty();
    }

    [Theory]
    [InlineData("""{ "id": "a", "name": "A", "faction": "abyss", "kind": "unit", "rows": ["melee"], "strenght": 3 }""", "strenght")]
    [InlineData("""{ "id": "a", "name": "A", "faction": "abyss", "kind": "unit", "rows": ["middle"], "strength": 3 }""", "Invalid JSON")]
    [InlineData("""{ "id": "a", "name": "A", "faction": "abyss", "kind": "unit", "rows": ["melee"], "strength": 3, "strength": 4 }""", "Invalid JSON")]
    [InlineData("""{ "id": "a", "name": "A", "faction": "abyss", "kind": 0, "rows": ["melee"], "strength": 3 }""", "Invalid JSON")]
    [InlineData("""not json""", "Invalid JSON")]
    [InlineData("""null""", "empty")]
    public void Rejects_malformed_json(string json, string expected) =>
        Errors("cards/abyss/a.json", json).ShouldContain(e => e.StartsWith("cards/abyss/a.json: ", StringComparison.Ordinal) && e.Contains(expected));

    [Theory]
    [InlineData("cards/abyss/b.json", """{ "id": "a", "name": "A", "faction": "abyss", "kind": "unit", "rows": ["melee"], "strength": 3 }""", "match the file name")]
    [InlineData("cards/abyss/Bad-Id.json", """{ "id": "Bad-Id", "name": "A", "faction": "abyss", "kind": "unit", "rows": ["melee"], "strength": 3 }""", "snake_case")]
    [InlineData("cards/realms/a.json", """{ "id": "a", "name": "A", "faction": "abyss", "kind": "unit", "rows": ["melee"], "strength": 3 }""", "cards/abyss/")]
    [InlineData("cards/abyss/a.json", """{ "id": "a", "faction": "abyss", "kind": "unit", "rows": ["melee"], "strength": 3 }""", "'name' is required")]
    [InlineData("cards/abyss/a.json", """{ "id": "a", "name": "A", "kind": "unit", "rows": ["melee"], "strength": 3 }""", "'faction' is required")]
    [InlineData("cards/abyss/a.json", """{ "id": "a", "name": "A", "faction": "abyss", "rows": ["melee"], "strength": 3 }""", "'kind' is required")]
    [InlineData("cards/abyss/a.json", """{ "id": "a", "name": "A", "faction": "abyss", "kind": "unit", "strength": 3 }""", "at least one row")]
    [InlineData("cards/abyss/a.json", """{ "id": "a", "name": "A", "faction": "abyss", "kind": "unit", "rows": ["melee", "melee"], "strength": 3 }""", "'rows' contains duplicates")]
    [InlineData("cards/abyss/a.json", """{ "id": "a", "name": "A", "faction": "abyss", "kind": "unit", "rows": ["melee"] }""", "'strength'")]
    [InlineData("cards/abyss/a.json", """{ "id": "a", "name": "A", "faction": "abyss", "kind": "unit", "rows": ["melee"], "strength": -1 }""", "'strength'")]
    [InlineData("cards/abyss/a.json", """{ "id": "a", "name": "A", "faction": "abyss", "kind": "unit", "rows": ["melee"], "strength": 3, "effect": "scorch" }""", "cannot have an 'effect'")]
    [InlineData("cards/abyss/a.json", """{ "id": "a", "name": "A", "faction": "abyss", "kind": "unit", "rows": ["melee"], "strength": 3, "abilities": ["muster"] }""", "musterGroup")]
    [InlineData("cards/abyss/a.json", """{ "id": "a", "name": "A", "faction": "abyss", "kind": "unit", "rows": ["melee"], "strength": 3, "abilities": ["concord"] }""", "'concord' draw count")]
    [InlineData("cards/abyss/a.json", """{ "id": "a", "name": "A", "faction": "abyss", "kind": "unit", "rows": ["melee"], "strength": 3, "abilities": ["concord"], "concord": 0 }""", "'concord' draw count")]
    [InlineData("cards/abyss/a.json", """{ "id": "a", "name": "A", "faction": "abyss", "kind": "unit", "rows": ["melee"], "strength": 3, "concord": 2 }""", "needs the 'concord' ability")]
    [InlineData("cards/abyss/a.json", """{ "id": "a", "name": "A", "faction": "abyss", "kind": "special", "effect": "scorch", "concord": 2 }""", "cannot have a 'concord'")]
    [InlineData("cards/abyss/a.json", """{ "id": "a", "name": "A", "faction": "abyss", "kind": "unit", "rows": ["melee"], "strength": 3, "abilities": ["spy", "spy"] }""", "'abilities' contains duplicates")]
    [InlineData("cards/abyss/a.json", """{ "id": "a", "name": "A", "faction": "abyss", "kind": "special" }""", "needs an 'effect'")]
    [InlineData("cards/abyss/a.json", """{ "id": "a", "name": "A", "faction": "abyss", "kind": "special", "effect": "scorch", "strength": 3 }""", "cannot have a 'strength'")]
    [InlineData("cards/abyss/a.json", """{ "id": "a", "name": "A", "faction": "abyss", "kind": "special", "effect": "scorch", "hero": true }""", "cannot be a hero")]
    [InlineData("cards/abyss/a.json", """{ "id": "a", "name": "A", "faction": "abyss", "kind": "special", "effect": "scorch", "abilities": ["spy"] }""", "cannot have 'abilities'")]
    [InlineData("cards/abyss/a.json", """{ "id": "a", "name": "A", "faction": "abyss", "kind": "special", "effect": "scorch", "musterGroup": "x" }""", "musterGroup")]
    [InlineData("cards/abyss/a.json", """{ "id": "a", "name": "A", "faction": "abyss", "kind": "special", "effect": "weather" }""", "affected rows")]
    [InlineData("cards/abyss/a.json", """{ "id": "a", "name": "A", "faction": "abyss", "kind": "special", "effect": "decoy", "rows": ["melee"] }""", "cannot have 'rows'")]
    public void Rejects_invalid_cards(string path, string json, string expected) =>
        Errors(path, json).ShouldContain(e => e.StartsWith(path + ": ", StringComparison.Ordinal) && e.Contains(expected));

    [Theory]
    [InlineData("""{ "id": "x", "name": "X", "faction": "neutral", "ability": "clear_weather" }""", "other than neutral")]
    [InlineData("""{ "id": "x", "name": "X", "faction": "abyss" }""", "'ability' is required")]
    [InlineData("""{ "id": "x", "name": "X", "faction": "abyss", "ability": "horn_row" }""", "needs a 'row'")]
    [InlineData("""{ "id": "x", "name": "X", "faction": "abyss", "ability": "clear_weather", "row": "melee" }""", "does not use a 'row'")]
    [InlineData("""{ "id": "x", "name": "X", "faction": "abyss", "ability": "scorch", "abilityName": "A", "row": "melee" }""", "does not use a 'row'")]
    [InlineData("""{ "id": "x", "name": "X", "faction": "abyss", "ability": "return_from_board", "abilityName": "A", "row": "siege" }""", "does not use a 'row'")]
    [InlineData("""{ "id": "x", "name": "X", "faction": "abyss", "ability": "clear_weather" }""", "'abilityName' is required")]
    [InlineData("""{ "id": "x", "name": "X", "faction": "abyss", "ability": "clear_weather", "abilityName": " " }""", "'abilityName' is required")]
    [InlineData("""{ "id": "x", "name": "X", "faction": "abyss", "ability": "clear_weather", "abilityName": "A", "description": "A: b." }""", "description")]
    public void Rejects_invalid_leaders(string json, string expected) =>
        Errors("leaders/x.json", json).ShouldContain(e => e.StartsWith("leaders/x.json: ", StringComparison.Ordinal) && e.Contains(expected));

    [Theory]
    [InlineData("""{ "id": "d", "name": "D", "leader": "lich", "cards": { "a": 1 } }""", "'faction' is required")]
    [InlineData("""{ "id": "d", "name": "D", "faction": "abyss", "cards": { "a": 1 } }""", "'leader' is required")]
    [InlineData("""{ "id": "d", "name": "D", "faction": "abyss", "leader": "lich", "cards": {} }""", "at least one card")]
    public void Rejects_invalid_deck_files(string json, string expected) =>
        Errors("decks/d.json", json).ShouldContain(e => e.StartsWith("decks/d.json: ", StringComparison.Ordinal) && e.Contains(expected));

    [Fact]
    public void Cards_may_be_organised_in_subfolders_of_their_faction() =>
        Errors("cards/abyss/creeps/a.json", """{ "id": "a", "name": "A", "faction": "abyss", "kind": "unit", "rows": ["melee"], "strength": 3 }""")
            .ShouldBeEmpty();

    [Fact]
    public void Rejects_duplicate_ids_across_folders()
    {
        var source = new InMemoryDataSource()
            .Add("cards/abyss/a.json", """{ "id": "a", "name": "A", "faction": "abyss", "kind": "unit", "rows": ["melee"], "strength": 3 }""")
            .Add("cards/abyss/sub/a.json", """{ "id": "a", "name": "A", "faction": "abyss", "kind": "unit", "rows": ["melee"], "strength": 3 }""");

        GameDataLoader.TryLoad(source).Errors.ShouldContain(e => e.Contains("Duplicate id 'a'"));
    }

    [Fact]
    public void Data_names_are_snake_case() =>
        GameDataLoader.ToDataName(LeaderAbility.PlayWeatherFromDeck).ShouldBe("play_weather_from_deck");

    [Theory]
    [InlineData("scorch", LeaderAbility.Scorch)]
    [InlineData("return_from_board", LeaderAbility.ReturnFromBoard)]
    public void Loads_leaders_without_a_row(string ability, LeaderAbility expected)
    {
        var source = new InMemoryDataSource()
            .Add("leaders/x.json", $$"""{ "id": "x", "name": "X", "faction": "horde", "ability": "{{ability}}", "abilityName": "A" }""");

        var leader = GameDataLoader.TryLoad(source).Data.ShouldNotBeNull().Leaders["x"];

        leader.Ability.ShouldBe(expected);
        leader.Row.ShouldBeNull();
    }

    private sealed class BackslashSource(IDataSource inner) : IDataSource
    {
        public IEnumerable<string> ListFiles(string directory, string extension) =>
            inner.ListFiles(directory, extension).Select(path => path.Replace('/', '\\'));

        public string ReadAllText(string path)
        {
            path.ShouldNotContain('/');
            return inner.ReadAllText(path.Replace('\\', '/'));
        }
    }
}
