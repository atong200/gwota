namespace Gwota.Core.Match;

/// <summary>Everything one player owns during a match. Mutated only by the engine.</summary>
public sealed class PlayerState
{
    private readonly BoardRow[] _rows;

    internal PlayerState(Seat seat, string name, Faction faction, LeaderDefinition leader, int gems)
    {
        Seat = seat;
        Name = name;
        Faction = faction;
        Leader = leader;
        Gems = gems;
        _rows = [new BoardRow(RowType.Melee), new BoardRow(RowType.Ranged), new BoardRow(RowType.Siege)];
    }

    public Seat Seat { get; }

    public string Name { get; }

    public Faction Faction { get; }

    public LeaderDefinition Leader { get; }

    public bool LeaderUsed { get; internal set; }

    /// <summary>Remaining lives. A player at zero gems has lost the match.</summary>
    public int Gems { get; internal set; }

    public bool HasPassed { get; internal set; }

    /// <summary>Most cards this player may redraw during the draft phase.</summary>
    public int RedrawLimit { get; internal set; }

    /// <summary>Most cards the hand may hold; draws stop there.</summary>
    public int HandLimit { get; internal set; } = int.MaxValue;

    public bool IsHandFull => HandList.Count >= HandLimit;

    public bool HasFinishedDraft { get; internal set; }

    public IReadOnlyList<CardInstance> Deck => DeckList;

    public IReadOnlyList<CardInstance> Hand => HandList;

    public IReadOnlyList<CardInstance> Discard => DiscardList;

    public IReadOnlyList<BoardRow> Rows => _rows;

    /// <summary>Every card currently on this player's side of the board (row cards and horn cards).</summary>
    public IEnumerable<CardInstance> BoardCards =>
        _rows.SelectMany(r => r.HornCard is null ? r.Cards : r.Cards.Append(r.HornCard));

    internal List<CardInstance> DeckList { get; } = [];

    internal List<CardInstance> HandList { get; } = [];

    internal List<CardInstance> DiscardList { get; } = [];

    public BoardRow Row(RowType type) => _rows[(int)type];

    /// <summary>The card with <paramref name="cardId"/> in this player's hand, or null.</summary>
    public CardInstance? FindInHand(int cardId) => Find(HandList, cardId);

    /// <summary>Finds a card in any of this player's zones (deck, hand, discard, board).</summary>
    public CardInstance? FindCard(int cardId)
    {
        if ((Find(DeckList, cardId) ?? Find(HandList, cardId) ?? Find(DiscardList, cardId)) is { } card)
        {
            return card;
        }

        foreach (var row in _rows)
        {
            if (Find(row.CardList, cardId) is { } rowCard)
            {
                return rowCard;
            }

            if (row.HornCard?.Id == cardId)
            {
                return row.HornCard;
            }
        }

        return null;
    }

    internal static CardInstance? Find(List<CardInstance> pile, int cardId)
    {
        foreach (var card in pile)
        {
            if (card.Id == cardId)
            {
                return card;
            }
        }

        return null;
    }

    internal PlayerState Clone()
    {
        var copy = new PlayerState(Seat, Name, Faction, Leader, Gems)
        {
            LeaderUsed = LeaderUsed,
            HasPassed = HasPassed,
            RedrawLimit = RedrawLimit,
            HandLimit = HandLimit,
            HasFinishedDraft = HasFinishedDraft,
        };
        copy.DeckList.AddRange(DeckList);
        copy.HandList.AddRange(HandList);
        copy.DiscardList.AddRange(DiscardList);
        for (var i = 0; i < _rows.Length; i++)
        {
            copy._rows[i] = _rows[i].Clone();
        }

        return copy;
    }
}
