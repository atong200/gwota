namespace Gwota.Core.Data;

public sealed record DeckEntry(string CardId, int Count);

/// <summary>A deck as authored in data: a faction, a leader and card counts.</summary>
public sealed record DeckDefinition
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public required Faction Faction { get; init; }

    public required string LeaderId { get; init; }

    public required IReadOnlyList<DeckEntry> Cards { get; init; }
}

/// <summary>Deck-building constraints (by default at least 22 units and at most 10 specials).</summary>
public sealed record DeckRules
{
    public static DeckRules Classic { get; } = new();

    public int MinUnits { get; init; } = 22;

    public int MaxSpecials { get; init; } = 10;

    public int MaxCopiesPerCard { get; init; } = 3;

    public int MaxHeroCopies { get; init; } = 1;

    /// <summary>How many copies of <paramref name="card"/> a deck may hold.</summary>
    public int MaxCopiesOf(CardDefinition card)
    {
        ArgumentNullException.ThrowIfNull(card);
        return card.IsHero ? MaxHeroCopies : MaxCopiesPerCard;
    }
}
