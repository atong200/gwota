using Gwota.Core.Data;

namespace Gwota.Core.Tests.Data;

public class DeckValidatorTests
{
    private static readonly LeaderDefinition RealmsLeader = TestCards.Leader(LeaderAbility.ClearWeather);

    private static readonly Dictionary<string, LeaderDefinition> Leaders = new()
    {
        [RealmsLeader.Id] = RealmsLeader,
        ["hells_leader"] = TestCards.Leader(LeaderAbility.ClearWeather, faction: Faction.Hells) with { Id = "hells_leader" },
    };

    private static readonly Dictionary<string, CardDefinition> Cards = new[]
    {
        TestCards.Unit(5, id: "realms_unit", faction: Faction.Realms),
        TestCards.Unit(5, id: "hells_unit", faction: Faction.Hells),
        TestCards.Unit(5, id: "neutral_unit"),
        TestCards.Hero(10, id: "realms_hero") with { Faction = Faction.Realms },
        TestCards.Scorch(),
    }.ToDictionary(c => c.Id);

    private static DeckDefinition Deck(params DeckEntry[] extra) => new()
    {
        Id = "deck",
        Name = "Deck",
        Faction = Faction.Realms,
        LeaderId = RealmsLeader.Id,
        Cards = new[] { new DeckEntry("realms_unit", 3), new DeckEntry("neutral_unit", 3) }
            .Concat(extra)
            .ToList(),
    };

    private static IReadOnlyList<string> Validate(DeckDefinition deck) =>
        DeckValidator.Validate(deck, Cards, Leaders, new DeckRules { MinUnits = 6, MaxSpecials = 2 });

    [Fact]
    public void Legal_deck_has_no_errors() =>
        Validate(Deck(new DeckEntry("scorch", 2), new DeckEntry("realms_hero", 1))).ShouldBeEmpty();

    [Fact]
    public void Too_few_units_is_an_error() =>
        Validate(Deck() with { Cards = [new DeckEntry("realms_unit", 3)] }).ShouldContain(e => e.Contains("at least 6"));

    [Fact]
    public void Too_many_specials_is_an_error() =>
        Validate(Deck(new DeckEntry("scorch", 3))).ShouldContain(e => e.Contains("at most 2"));

    [Fact]
    public void Copies_are_limited_with_heroes_unique()
    {
        var errors = Validate(Deck() with { Cards = [new DeckEntry("realms_unit", 4), new DeckEntry("neutral_unit", 3), new DeckEntry("realms_hero", 2)] });

        errors.ShouldContain(e => e.Contains("'realms_unit' has 4 copies"));
        errors.ShouldContain(e => e.Contains("'realms_hero' has 2 copies; the maximum is 1"));
    }

    [Fact]
    public void Cards_from_the_other_faction_are_rejected() =>
        Validate(Deck(new DeckEntry("hells_unit", 1))).ShouldContain(e => e.Contains("'hells_unit' belongs to The Seven Hells"));

    [Fact]
    public void Unknown_cards_and_bad_counts_are_rejected()
    {
        var errors = Validate(Deck(new DeckEntry("missing", 1), new DeckEntry("scorch", 0)));

        errors.ShouldContain(e => e.Contains("Unknown card 'missing'"));
        errors.ShouldContain(e => e.Contains("count of at least 1"));
    }

    [Fact]
    public void Leader_must_exist_and_match_the_faction()
    {
        Validate(Deck() with { LeaderId = "nobody" }).ShouldContain(e => e.Contains("Unknown leader"));
        Validate(Deck() with { LeaderId = "hells_leader" }).ShouldContain(e => e.Contains("belongs to The Seven Hells, not Realms of Men"));
    }

    [Fact]
    public void Neutral_decks_are_rejected() =>
        Validate(Deck() with { Faction = Faction.Neutral, LeaderId = "nobody" }).ShouldContain(e => e.Contains("not Neutral"));
}
