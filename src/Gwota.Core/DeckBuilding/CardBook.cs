namespace Gwota.Core.DeckBuilding;

/// <summary>Narrows the book to one combat row (agile cards appear under each of their rows) or to specials.</summary>
public enum BookFilter
{
    All,
    Melee,
    Ranged,
    Siege,
    Special,
}

/// <summary>
/// The deck builder's paged collection: one tab per faction, a row filter and fixed-size pages (back to page one on change).
/// Order: heroes, then other units, each strongest first, then specials; ties by name.
/// </summary>
public sealed class CardBook
{
    public const int DefaultPageSize = 10;

    private readonly List<CardDefinition> _cards;
    private List<CardDefinition> _visible = [];
    private Faction _tab;
    private BookFilter _filter;

    public CardBook(IEnumerable<CardDefinition> cards, IReadOnlyList<Faction> tabs, int pageSize = DefaultPageSize)
    {
        ArgumentNullException.ThrowIfNull(cards);
        ArgumentNullException.ThrowIfNull(tabs);
        ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);
        if (tabs.Count == 0 || tabs.Distinct().Count() != tabs.Count)
        {
            throw new ArgumentException("A book needs at least one tab, without duplicates.", nameof(tabs));
        }

        Tabs = tabs.ToList();
        PageSize = pageSize;
        _tab = Tabs[0];
        _cards = cards
            .Where(c => Tabs.Contains(c.Faction))
            .OrderBy(c => c.IsSpecial)
            .ThenByDescending(c => c.IsHero)
            .ThenByDescending(c => c.IsUnit ? c.Strength : 0)
            .ThenBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(c => c.Id, StringComparer.Ordinal)
            .ToList();
        Refresh();
    }

    /// <summary>The whole collection: every playable faction, then Neutral.</summary>
    public static CardBook ForCollection(IEnumerable<CardDefinition> cards, int pageSize = DefaultPageSize) =>
        new(cards, [.. Enum.GetValues<Faction>().Where(f => f != Faction.Neutral), Faction.Neutral], pageSize);

    /// <summary>The cards a deck of <paramref name="faction"/> may use: its faction, then Neutral.</summary>
    public static CardBook ForDeck(IEnumerable<CardDefinition> cards, Faction faction, int pageSize = DefaultPageSize)
    {
        if (faction == Faction.Neutral)
        {
            throw new ArgumentException("A deck faction cannot be Neutral.", nameof(faction));
        }

        return new(cards, [faction, Faction.Neutral], pageSize);
    }

    /// <summary>The bookmark tabs, in display order.</summary>
    public IReadOnlyList<Faction> Tabs { get; }
    public int PageSize { get; }

    /// <summary>The open tab; must be one of <see cref="Tabs"/>.</summary>
    public Faction Tab
    {
        get => _tab;
        set
        {
            if (!Tabs.Contains(value))
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "This book has no such tab.");
            }

            _tab = value;
            Refresh();
        }
    }

    public BookFilter Filter
    {
        get => _filter;
        set
        {
            _filter = value;
            Refresh();
        }
    }

    /// <summary>Zero-based index of the current page, always within <c>[0, PageCount - 1]</c>.</summary>
    public int Page { get; private set; }

    /// <summary>Number of pages for the current tab and filter (at least one, possibly empty).</summary>
    public int PageCount => Math.Max(1, (_visible.Count + PageSize - 1) / PageSize);

    /// <summary>Every card matching the current tab and filter, in book order.</summary>
    public IReadOnlyList<CardDefinition> Matching => _visible;

    public IReadOnlyList<CardDefinition> CurrentPage => _visible.Skip(Page * PageSize).Take(PageSize).ToList();

    public bool HasPreviousPage => Page > 0;

    public bool HasNextPage => Page < PageCount - 1;

    public bool NextPage() => GoTo(Page + 1);

    public bool PreviousPage() => GoTo(Page - 1);

    /// <summary>Moves to <paramref name="page"/> if it exists; returns false (and stays put) otherwise.</summary>
    public bool GoTo(int page)
    {
        if (page < 0 || page >= PageCount || page == Page)
        {
            return false;
        }

        Page = page;
        return true;
    }

    public static bool Matches(CardDefinition card, BookFilter filter)
    {
        ArgumentNullException.ThrowIfNull(card);
        return filter switch
        {
            BookFilter.All => true,
            BookFilter.Special => card.IsSpecial,
            BookFilter.Melee => card.IsUnit && card.Rows.Contains(RowType.Melee),
            BookFilter.Ranged => card.IsUnit && card.Rows.Contains(RowType.Ranged),
            BookFilter.Siege => card.IsUnit && card.Rows.Contains(RowType.Siege),
            _ => throw new ArgumentOutOfRangeException(nameof(filter), filter, null),
        };
    }

    private void Refresh()
    {
        _visible = _cards.Where(c => c.Faction == _tab && Matches(c, _filter)).ToList();
        Page = 0;
    }
}
