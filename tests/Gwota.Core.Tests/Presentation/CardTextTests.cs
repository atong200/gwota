using Gwota.Core.Presentation;

namespace Gwota.Core.Tests.Presentation;

public class CardTextTests
{
    [Fact]
    public void Every_unit_ability_has_rules_text()
    {
        foreach (var ability in Enum.GetValues<UnitAbility>())
        {
            CardText.Ability(ability).ShouldNotBeNullOrWhiteSpace();
        }
    }

    [Fact]
    public void Every_special_effect_has_rules_text()
    {
        foreach (var effect in Enum.GetValues<SpecialEffect>())
        {
            CardText.Effect(effect, [RowType.Melee]).ShouldNotBeNullOrWhiteSpace();
        }
    }

    [Fact]
    public void Every_leader_ability_has_rules_text()
    {
        foreach (var ability in Enum.GetValues<LeaderAbility>())
        {
            CardText.Leader(TestCards.Leader(ability, RowType.Siege)).ShouldNotBeNullOrWhiteSpace();
        }
    }

    [Fact]
    public void Weather_text_names_the_affected_rows() =>
        CardText.Effect(SpecialEffect.Weather, [RowType.Ranged, RowType.Siege]).ShouldContain("ranged/siege");

    [Fact]
    public void Row_leaders_name_their_row() =>
        CardText.Leader(TestCards.Leader(LeaderAbility.HornRow, RowType.Siege)).ShouldBe("Double your siege row.");

    [Fact]
    public void Unit_description_lists_type_hero_abilities_and_flavor()
    {
        var card = TestCards.Hero(10) with { Name = "Sven", Abilities = [UnitAbility.MoraleBoost], Description = "God's Strength." };

        CardText.Describe(card).Split('\n').ShouldBe(
        [
            "Sven",
            "Neutral hero - melee - 10",
            "Hero: immune to weather, horns, scorch and medics. Only one copy per deck.",
            CardText.Ability(UnitAbility.MoraleBoost),
            "God's Strength.",
        ]);
    }

    [Fact]
    public void Special_description_shows_its_effect()
    {
        var lines = CardText.Describe(TestCards.Scorch()).Split('\n');

        lines[1].ShouldBe("Neutral special");
        lines[2].ShouldBe(CardText.Effect(SpecialEffect.Scorch, []));
    }

    [Fact]
    public void Agile_units_show_every_row() =>
        CardText.TypeLine(TestCards.Agile(6, "agile", RowType.Melee, RowType.Ranged)).ShouldBe("Neutral unit - melee/ranged - 6");

    [Fact]
    public void Rules_split_keywords_from_their_text_and_mark_flavor()
    {
        var card = TestCards.Hero(10) with { Name = "Sven", Abilities = [UnitAbility.TightBond], Description = "God's Strength." };

        CardText.Rules(card).ShouldBe(
        [
            new CardTextLine("immune to weather, horns, scorch and medics. Only one copy per deck.", "Hero"),
            new CardTextLine("strength multiplied by copies of this card in the row.", "Tight Bond"),
            new CardTextLine("God's Strength.", IsFlavor: true),
        ]);
    }

    [Fact]
    public void Rules_leave_out_the_name_type_rows_and_strength()
    {
        var card = TestCards.Unit(7, RowType.Siege, "Techies");

        CardText.Rules(card).ShouldBeEmpty();
    }

    [Fact]
    public void Special_rules_show_the_effect_keyword() =>
        CardText.Rules(TestCards.Weather(RowType.Ranged)).ShouldHaveSingleItem().Keyword.ShouldBe("Weather");

    [Fact]
    public void Every_ability_and_effect_line_has_a_keyword()
    {
        foreach (var ability in Enum.GetValues<UnitAbility>())
        {
            CardText.AbilityLine(ability).Keyword.ShouldNotBeNullOrWhiteSpace();
        }

        foreach (var effect in Enum.GetValues<SpecialEffect>())
        {
            CardText.EffectLine(effect, [RowType.Melee]).Keyword.ShouldNotBeNullOrWhiteSpace();
        }
    }

    [Fact]
    public void Plain_text_joins_keyword_and_text()
    {
        new CardTextLine("text", "Spy").Plain.ShouldBe("Spy: text");
        new CardTextLine("flavor", IsFlavor: true).Plain.ShouldBe("flavor");
    }

    [Theory]
    [InlineData(0, "draw pile", "No cards in draw pile")]
    [InlineData(1, "discard pile", "1 card in discard pile")]
    [InlineData(12, "draw pile", "12 cards in draw pile")]
    public void Pile_counts_read_as_a_sentence(int count, string pile, string expected) =>
        CardText.PileCount(count, pile).ShouldBe(expected);

    [Theory]
    [InlineData(0, "x0")]
    [InlineData(3, "x3")]
    public void Copy_counters_show_remaining_copies(int count, string expected) =>
        CardText.Copies(count).ShouldBe(expected);

    [Fact]
    public void Copy_counters_cannot_be_negative() =>
        Should.Throw<ArgumentOutOfRangeException>(() => CardText.Copies(-1));

    [Theory]
    [InlineData(22, 4, 1, 98, "22 units, 4 specials, 1 hero - 98 strength")]
    [InlineData(1, 1, 0, 5, "1 unit, 1 special - 5 strength")]
    [InlineData(0, 0, 0, 0, "0 units, 0 specials - 0 strength")]
    [InlineData(25, 2, 3, 150, "25 units, 2 specials, 3 heroes - 150 strength")]
    public void Deck_summaries_list_counts_and_strength(int units, int specials, int heroes, int strength, string expected) =>
        CardText.DeckSummary(new Gwota.Core.DeckBuilding.DeckStats(units, specials, heroes, strength)).ShouldBe(expected);

    [Fact]
    public void Pile_counts_reject_negative_counts() =>
        Should.Throw<ArgumentOutOfRangeException>(() => CardText.PileCount(-1, "draw pile"));
}