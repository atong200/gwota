namespace Gwota.Core.Match;

/// <summary>One combat row on one player's side of the board.</summary>
public sealed class BoardRow
{
    internal BoardRow(RowType type) => Type = type;

    public RowType Type { get; }

    /// <summary>Cards in the row: units, plus decoys that took a unit's place.</summary>
    public IReadOnlyList<CardInstance> Cards => CardList;

    /// <summary>A horn special card occupying this row's horn slot.</summary>
    public CardInstance? HornCard { get; internal set; }

    /// <summary>A horn granted by a leader ability (no physical card).</summary>
    public bool HasLeaderHorn { get; internal set; }

    /// <summary>True when the row's horn slot is taken (horn card or leader horn).</summary>
    public bool HasHornSlotFilled => HornCard is not null || HasLeaderHorn;

    internal List<CardInstance> CardList { get; } = [];

    internal BoardRow Clone()
    {
        var copy = new BoardRow(Type) { HornCard = HornCard, HasLeaderHorn = HasLeaderHorn };
        copy.CardList.AddRange(CardList);
        return copy;
    }
}
