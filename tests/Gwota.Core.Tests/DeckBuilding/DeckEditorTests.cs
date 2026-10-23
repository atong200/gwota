using Gwota.Core.Data;
using Gwota.Core.DeckBuilding;

namespace Gwota.Core.Tests.DeckBuilding;

public class DeckEditorTests
{
    private static readonly GameData Data = DeckBuildingData.Create();

    private static DeckEditor Editor(DeckDefinition? deck = null, DeckRules? rules = null) =>
        new(deck ?? DeckBuildingData.EmptyDeck(), Data, rules);

    [Fact]
    public void Adding_and_removing_copies_updates_the_deck()
    {
        var editor = Editor();

        editor.TryAdd("realms_2").ShouldBeTrue();
        editor.TryAdd("realms_2").ShouldBeTrue();
        editor.TryAdd("neutral_1").ShouldBeTrue();
        editor.Remove("realms_2").ShouldBeTrue();

        editor.CountOf("realms_2").ShouldBe(1);
        editor.Deck.Cards.ShouldBe([new DeckEntry("realms_2", 1), new DeckEntry("neutral_1", 1)]);
    }

    [Fact]
    public void Removing_the_last_copy_drops_the_entry_and_removing_a_missing_card_fails()
    {
        var editor = Editor();
        editor.TryAdd("realms_1");

        editor.Remove("realms_1").ShouldBeTrue();
        editor.Remove("realms_1").ShouldBeFalse();

        editor.Deck.Cards.ShouldBeEmpty();
    }

    [Fact]
    public void Copies_are_capped_at_three_and_heroes_at_one()
    {
        var editor = Editor();
        for (var i = 0; i < 3; i++)
        {
            editor.TryAdd("realms_1").ShouldBeTrue();
        }

        editor.TryAdd("realms_1").ShouldBeFalse();
        editor.AddRefusal(Data.Cards["realms_1"]).ShouldBe("At most 3 copies per card.");
        editor.TryAdd("realms_hero").ShouldBeTrue();
        editor.TryAdd("realms_hero").ShouldBeFalse();
        editor.AddRefusal(Data.Cards["realms_hero"]).ShouldBe("Heroes are limited to 1 copy per deck.");
    }

    [Fact]
    public void Remaining_counts_down_the_copies_left_to_add()
    {
        var editor = Editor();
        var unit = Data.Cards["realms_1"];
        var hero = Data.Cards["realms_hero"];

        editor.Remaining(unit).ShouldBe(3);
        editor.Remaining(hero).ShouldBe(1);
        editor.TryAdd(unit.Id);
        editor.TryAdd(hero.Id);

        editor.Remaining(unit).ShouldBe(2);
        editor.Remaining(hero).ShouldBe(0);
    }

    [Fact]
    public void Remaining_never_goes_negative_when_rules_shrink()
    {
        var deck = DeckBuildingData.EmptyDeck() with { Cards = [new DeckEntry("realms_1", 3)] };

        Editor(deck, DeckRules.Classic with { MaxCopiesPerCard = 2 }).Remaining(Data.Cards["realms_1"]).ShouldBe(0);
    }

    [Fact]
    public void Rules_give_heroes_their_own_copy_limit()
    {
        var rules = DeckRules.Classic with { MaxCopiesPerCard = 4, MaxHeroCopies = 2 };

        rules.MaxCopiesOf(Data.Cards["realms_1"]).ShouldBe(4);
        rules.MaxCopiesOf(Data.Cards["realms_hero"]).ShouldBe(2);
    }

    [Fact]
    public void Heroes_lead_their_row_group_even_when_weaker()
    {
        var data = new GameData(
            new[]
            {
                TestCards.Unit(9, RowType.Melee, "strong", Faction.Realms),
                TestCards.Unit(5, RowType.Melee, "weak_hero", Faction.Realms, hero: true),
            }.ToDictionary(c => c.Id),
            Data.Leaders,
            new Dictionary<string, DeckDefinition>());
        var editor = new DeckEditor(DeckBuildingData.EmptyDeck(), data);
        editor.TryAdd("strong");
        editor.TryAdd("weak_hero");

        editor.Lines.Select(l => l.Card.Id).ShouldBe(["weak_hero", "strong"]);
    }

    [Fact]
    public void Other_faction_and_unknown_cards_are_refused()
    {
        var editor = Editor();

        editor.AddRefusal(Data.Cards["hells_1"]).ShouldBe("hells_1 belongs to The Seven Hells.");
        editor.TryAdd("hells_1").ShouldBeFalse();
        editor.TryAdd("no_such_card").ShouldBeFalse();
    }

    [Fact]
    public void Specials_are_capped_by_the_rules()
    {
        var editor = Editor(rules: DeckRules.Classic with { MaxSpecials = 2 });
        editor.TryAdd("horn");
        editor.TryAdd("scorch");

        editor.TryAdd("horn").ShouldBeFalse();
        editor.AddRefusal(Data.Cards["horn"]).ShouldBe("At most 2 special cards per deck.");
    }

    [Fact]
    public void An_incomplete_deck_reports_its_problems_and_is_not_playable()
    {
        var editor = Editor();
        editor.TryAdd("realms_1");

        editor.IsPlayable.ShouldBeFalse();
        editor.Problems.ShouldContain("The deck has 1 unit cards; at least 22 are required.");
    }

    [Fact]
    public void A_deck_meeting_the_rules_is_playable()
    {
        var editor = Editor(rules: DeckRules.Classic with { MinUnits = 2 });

        editor.TryAdd("realms_1");
        editor.TryAdd("neutral_1");

        editor.IsPlayable.ShouldBeTrue();
    }

    [Fact]
    public void Stats_count_units_specials_heroes_and_strength()
    {
        var editor = Editor();
        editor.TryAdd("realms_3");
        editor.TryAdd("realms_3");
        editor.TryAdd("realms_hero");
        editor.TryAdd("horn");

        editor.Stats.ShouldBe(new DeckStats(Units: 3, Specials: 1, Heroes: 1, TotalStrength: 16));
        editor.Stats.Total.ShouldBe(4);
    }

    [Fact]
    public void Lines_are_grouped_by_row_then_strongest_first_with_specials_last()
    {
        var editor = Editor();
        foreach (var id in new[] { "horn", "neutral_1", "realms_hero", "realms_1", "neutral_agile", "realms_3" })
        {
            editor.TryAdd(id);
        }

        editor.Lines.Select(l => l.Card.Id).ShouldBe(["neutral_agile", "realms_3", "realms_1", "neutral_1", "realms_hero", "horn"]);
        editor.Lines.Select(l => l.Group).ShouldBe(
            [BookFilter.Melee, BookFilter.Melee, BookFilter.Melee, BookFilter.Ranged, BookFilter.Siege, BookFilter.Special]);
    }

    [Theory]
    [InlineData("  My   deck  ", "My deck")]
    [InlineData("A name that is definitely longer than thirty-two characters", "A name that is definitely longer")]
    public void Names_are_trimmed_collapsed_and_capped(string input, string expected)
    {
        var editor = Editor();

        editor.Rename(input).ShouldBeTrue();

        editor.Name.ShouldBe(expected);
        editor.Deck.Name.ShouldBe(expected);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Blank_names_are_refused(string? name)
    {
        var editor = Editor();

        editor.Rename(name).ShouldBeFalse();

        editor.Name.ShouldBe("Test deck");
    }

    [Fact]
    public void Only_faction_leaders_can_be_chosen_and_they_are_listed_by_name()
    {
        var editor = Editor();

        editor.Leaders.Select(l => l.Id).ShouldBe(["abaddon", "legion_commander"]);
        editor.SetLeader("underlord").ShouldBeFalse();
        editor.SetLeader("abaddon").ShouldBeTrue();
        editor.Deck.LeaderId.ShouldBe("abaddon");
    }

    [Fact]
    public void An_unusable_leader_falls_back_to_the_faction_default()
    {
        var editor = Editor(DeckBuildingData.EmptyDeck(leader: "underlord"));

        editor.Leader.Id.ShouldBe("abaddon");
    }

    [Fact]
    public void Unknown_cards_in_the_source_deck_are_ignored()
    {
        var deck = DeckBuildingData.EmptyDeck() with { Cards = [new DeckEntry("gone", 2), new DeckEntry("realms_1", 1)] };

        new DeckEditor(deck, Data).Deck.Cards.ShouldBe([new DeckEntry("realms_1", 1)]);
    }

    [Fact]
    public void Shipped_decks_are_playable_in_the_editor()
    {
        var shipped = GameDataLoader.Load(new FileSystemDataSource(RepoPaths.GameData));

        foreach (var deck in shipped.Decks.Values)
        {
            new DeckEditor(deck, shipped).IsPlayable.ShouldBeTrue(deck.Id);
        }
    }
}
