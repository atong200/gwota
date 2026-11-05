namespace Gwota.Core.Presentation;

/// <summary>What a click on a hand card or on the leader did to a <see cref="MatchSelection"/>.</summary>
public enum SelectionChange
{
    /// <summary>The card or leader is now selected; its targets light up.</summary>
    Selected,

    /// <summary>The selection was dropped (second click on the same card, or on a targeted leader ability).</summary>
    Cleared,

    /// <summary>Nothing can be played with it right now; the selection is unchanged.</summary>
    Unplayable,

    /// <summary>Second click on a board-wide leader ability: submit <see cref="PlayTargets.ForBoard"/>.</summary>
    Use,
}

/// <summary>The hand card or leader ability the local player has selected, with what it can act on.</summary>
public sealed class MatchSelection
{
    public int? CardId { get; private set; }

    public bool IsLeader { get; private set; }

    public PlayTargets Targets { get; private set; } = PlayTargets.None;

    public bool IsEmpty => CardId is null && !IsLeader;

    /// <summary>Selects a hand card, or drops the selection when that card was already selected.</summary>
    public SelectionChange ClickCard(MatchState state, IReadOnlyList<GameCommand> legal, Seat seat, int cardId)
    {
        if (CardId == cardId)
        {
            Clear();
            return SelectionChange.Cleared;
        }

        var targets = PlayTargets.For(state, legal, seat, cardId);
        if (targets.IsEmpty)
        {
            return SelectionChange.Unplayable;
        }

        Select(cardId, isLeader: false, targets);
        return SelectionChange.Selected;
    }

    /// <summary>
    /// First click selects the leader ability. A second click uses a board-wide ability (<see cref="SelectionChange.Use"/>)
    /// or cancels a targeted one, which is used by clicking one of its targets instead.
    /// </summary>
    public SelectionChange ClickLeader(MatchState state, IReadOnlyList<GameCommand> legal, Seat seat)
    {
        if (IsLeader)
        {
            if (Targets.IsBoardWide)
            {
                return SelectionChange.Use;
            }

            Clear();
            return SelectionChange.Cleared;
        }

        var targets = PlayTargets.ForLeader(state, legal, seat);
        if (targets.IsEmpty)
        {
            return SelectionChange.Unplayable;
        }

        Select(null, isLeader: true, targets);
        return SelectionChange.Selected;
    }

    /// <summary>Recomputes the targets after the state changed; drops the selection when it can no longer be played.</summary>
    public void Refresh(MatchState state, IReadOnlyList<GameCommand> legal, Seat seat)
    {
        Targets = IsLeader ? PlayTargets.ForLeader(state, legal, seat)
            : CardId is { } cardId ? PlayTargets.For(state, legal, seat, cardId)
            : PlayTargets.None;
        if (Targets.IsEmpty)
        {
            CardId = null;
            IsLeader = false;
        }
    }

    /// <summary>Drops the selected card or leader ability. Returns whether there was one.</summary>
    public bool Clear()
    {
        if (IsEmpty)
        {
            return false;
        }

        Select(null, isLeader: false, PlayTargets.None);
        return true;
    }

    private void Select(int? cardId, bool isLeader, PlayTargets targets)
    {
        CardId = cardId;
        IsLeader = isLeader;
        Targets = targets;
    }
}
