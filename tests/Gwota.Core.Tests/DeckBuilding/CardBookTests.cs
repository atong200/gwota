using Gwota.Core.DeckBuilding;

namespace Gwota.Core.Tests.DeckBuilding;

public class CardBookTests
{
    private static CardBook Book(int realmsUnits = 3, int pageSize = CardBook.DefaultPageSize) =>
        CardBook.ForDeck(DeckBuildingData.Create(realmsUnits).Cards.Values, Faction.Realms, pageSize);

    [Fact]
    public void Faction_tab_shows_only_the_deck_faction_heroes_then_strongest_first()
    {
        var book = Book();

        book.Tabs.ShouldBe([Faction.Realms, Faction.Neutral]);
        book.Tab.ShouldBe(Faction.Realms);
        book.CurrentPage.Select(c => c.Id).ShouldBe(["realms_hero", "realms_3", "realms_2", "realms_1"]);
    }

    [Fact]
    public void Neutral_tab_lists_units_before_specials()
    {
        var book = Book();

        book.Tab = Faction.Neutral;

        book.CurrentPage.Select(c => c.Id).ShouldBe(["neutral_agile", "neutral_2", "neutral_1", "horn", "scorch"]);
    }

    [Fact]
    public void Other_faction_cards_never_appear()
    {
        var book = Book();

        book.Matching.ShouldNotContain(c => c.Faction == Faction.Hells);
        book.Tab = Faction.Neutral;
        book.Matching.ShouldNotContain(c => c.Faction == Faction.Hells);
    }

    [Theory]
    [InlineData(BookFilter.Melee, new[] { "neutral_agile" })]
    [InlineData(BookFilter.Ranged, new[] { "neutral_2", "neutral_1" })]
    [InlineData(BookFilter.Siege, new[] { "neutral_agile" })]
    [InlineData(BookFilter.Special, new[] { "horn", "scorch" })]
    public void Filters_narrow_to_a_row_and_agile_cards_show_under_each_of_their_rows(BookFilter filter, string[] expected)
    {
        var book = Book();
        book.Tab = Faction.Neutral;

        book.Filter = filter;

        book.Matching.Select(c => c.Id).ShouldBe(expected);
    }

    [Fact]
    public void Cards_are_split_into_pages()
    {
        var book = Book(realmsUnits: 10);

        book.PageCount.ShouldBe(2);
        book.CurrentPage.Count.ShouldBe(10);
        book.HasPreviousPage.ShouldBeFalse();
        book.NextPage().ShouldBeTrue();
        book.Page.ShouldBe(1);
        book.CurrentPage.Count.ShouldBe(1);
        book.HasNextPage.ShouldBeFalse();
        book.NextPage().ShouldBeFalse();
        book.Page.ShouldBe(1);
    }

    [Fact]
    public void Changing_tab_or_filter_returns_to_the_first_page()
    {
        var book = Book(realmsUnits: 10);
        book.NextPage();

        book.Filter = BookFilter.Melee;

        book.Page.ShouldBe(0);
        book.NextPage();
        book.Tab = Faction.Neutral;
        book.Page.ShouldBe(0);
    }

    [Fact]
    public void An_empty_selection_still_has_one_empty_page()
    {
        var book = Book();

        book.Filter = BookFilter.Special;

        book.PageCount.ShouldBe(1);
        book.CurrentPage.ShouldBeEmpty();
        book.PreviousPage().ShouldBeFalse();
    }

    [Fact]
    public void Neutral_is_not_a_deck_faction() =>
        Should.Throw<ArgumentException>(() => CardBook.ForDeck([], Faction.Neutral));

    [Fact]
    public void The_collection_has_a_tab_per_faction_then_neutral()
    {
        var book = CardBook.ForCollection(DeckBuildingData.Create().Cards.Values);

        book.Tabs.ShouldBe([Faction.Realms, Faction.Hells, Faction.Neutral]);
        book.Tab.ShouldBe(Faction.Realms);
        book.Tab = Faction.Hells;
        book.Matching.Select(c => c.Id).ShouldBe(["hells_1"]);
    }

    [Fact]
    public void Heroes_come_before_stronger_regular_units()
    {
        var cards = new[]
        {
            TestCards.Unit(12, RowType.Melee, "big", Faction.Realms),
            TestCards.Unit(8, RowType.Melee, "hero", Faction.Realms, hero: true),
        };

        CardBook.ForDeck(cards, Faction.Realms).Matching.Select(c => c.Id).ShouldBe(["hero", "big"]);
    }

    [Fact]
    public void A_deck_book_cannot_open_another_faction_tab()
    {
        var book = Book();

        Should.Throw<ArgumentOutOfRangeException>(() => book.Tab = Faction.Hells);
        book.Tab.ShouldBe(Faction.Realms);
    }

    [Theory]
    [InlineData(new Faction[0])]
    [InlineData(new[] { Faction.Realms, Faction.Realms })]
    public void Tabs_must_be_non_empty_and_distinct(Faction[] tabs) =>
        Should.Throw<ArgumentException>(() => new CardBook([], tabs));
}
