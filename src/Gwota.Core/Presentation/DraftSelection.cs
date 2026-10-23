namespace Gwota.Core.Presentation;

/// <summary>
/// UI-side state of the draft phase: the hand cards a player has marked for redraw. Clicking a card toggles it;
/// once the limit is reached further cards are ignored until one is deselected.
/// </summary>
public sealed class DraftSelection
{
    private readonly List<int> _selected = [];

    public DraftSelection(PlayerState player)
    {
        ArgumentNullException.ThrowIfNull(player);
        Seat = player.Seat;
        Limit = Math.Min(player.RedrawLimit, Math.Min(player.Deck.Count, player.Hand.Count));
    }

    public Seat Seat { get; }

    /// <summary>Most cards that can be selected (bounded by the rules, the deck and the hand).</summary>
    public int Limit { get; }

    public IReadOnlyList<int> Selected => _selected;

    public bool IsFull => _selected.Count >= Limit;

    public bool IsSelected(int cardId) => _selected.Contains(cardId);

    /// <summary>Selects or deselects a card. Returns false when the card could not be selected (limit reached).</summary>
    public bool Toggle(int cardId)
    {
        if (_selected.Remove(cardId))
        {
            return true;
        }

        if (IsFull)
        {
            return false;
        }

        _selected.Add(cardId);
        return true;
    }

    public FinishDraftCommand ToCommand() => new(Seat, _selected.ToList());

    /// <summary>Label of the confirm button: "Keep hand" with no selection, otherwise "Redraw" (the highlights show what).</summary>
    public string ConfirmText => _selected.Count == 0 ? "Keep hand" : "Redraw";

    public string Instructions => Limit == 0
        ? "No card can be redrawn."
        : $"Select up to {Limit} card{(Limit == 1 ? string.Empty : "s")} to redraw. Click again to deselect.";
}
