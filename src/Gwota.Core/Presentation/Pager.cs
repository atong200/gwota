namespace Gwota.Core.Presentation;

/// <summary>Splits a fixed list into pages of <see cref="PageSize"/> items; there is always at least one page.</summary>
public sealed class Pager<T>
{
    private readonly IReadOnlyList<T> _items;

    public Pager(IReadOnlyList<T> items, int pageSize)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);
        _items = items;
        PageSize = pageSize;
    }

    public int PageSize { get; }

    /// <summary>The zero-based current page.</summary>
    public int Page { get; private set; }

    public int PageCount => Math.Max(1, (_items.Count + PageSize - 1) / PageSize);

    /// <summary>True when the items do not fit on one page (the pager controls are needed).</summary>
    public bool HasPages => PageCount > 1;

    public bool HasPrevious => Page > 0;

    public bool HasNext => Page < PageCount - 1;

    public IReadOnlyList<T> CurrentPage => _items.Skip(Page * PageSize).Take(PageSize).ToList();

    public bool Next()
    {
        if (!HasNext)
        {
            return false;
        }

        Page++;
        return true;
    }

    public bool Previous()
    {
        if (!HasPrevious)
        {
            return false;
        }

        Page--;
        return true;
    }

    /// <summary>Turns to the page holding the first item matching <paramref name="predicate"/>; false if none does.</summary>
    public bool ShowFirst(Func<T, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        for (var i = 0; i < _items.Count; i++)
        {
            if (predicate(_items[i]))
            {
                Page = i / PageSize;
                return true;
            }
        }

        return false;
    }
}
