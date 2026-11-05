using Gwota.Core.Cards;
using Gwota.Core.Common;
using Gwota.Core.Match;
using Gwota.Core.Presentation;
using Gwota.Core.Session;

namespace Gwota.Game.Ui;

/// <summary>
/// Turns the player's clicks into a selection (hand card or leader ability) and, on a target, into a command.
/// </summary>
public sealed class SelectionController(MatchSession session, Seat local)
{
    private readonly MatchSelection _selection = new();

    /// <summary>The selection or its targets changed; highlights need an update.</summary>
    public event Action? Changed;

    /// <summary>A target of the selection was clicked: the command to play.</summary>
    public event Action<GameCommand>? Play;

    /// <summary>The clicked card cannot be played now; the hint explains why.</summary>
    public event Action<string>? Rejected;

    public MatchSelection Selection => _selection;

    public PlayTargets Targets => _selection.Targets;

    public int? CardId => _selection.CardId;

    public bool IsLeader => _selection.IsLeader;

    /// <summary>False while the player here cannot act: hand card and leader clicks are ignored.</summary>
    public bool Enabled { get; private set; }

    /// <summary>Recomputes the targets for the new state; drops the selection when it can no longer be played.</summary>
    public void Refresh(IReadOnlyList<GameCommand> legal, bool enabled)
    {
        Enabled = enabled;
        _selection.Refresh(session.State, legal, local);
    }

    public void ClickHandCard(CardInstance card)
    {
        ArgumentNullException.ThrowIfNull(card);
        if (!Enabled)
        {
            return;
        }

        if (_selection.ClickCard(session.State, session.GetLegalCommands(local), local, card.Id) == SelectionChange.Unplayable)
        {
            Rejected?.Invoke(MatchTexts.Unplayable(card.Definition));
            return;
        }

        Changed?.Invoke();
    }

    /// <summary>
    /// First click selects the leader ability (its targets light up). A second click uses a board-wide ability, or
    /// cancels a targeted one (used by clicking one of its highlighted units instead).
    /// </summary>
    public void ClickLeader()
    {
        if (!Enabled)
        {
            return;
        }

        switch (_selection.ClickLeader(session.State, session.GetLegalCommands(local), local))
        {
            case SelectionChange.Use:
                PlayOn(Targets.ForBoard());
                break;
            case SelectionChange.Selected or SelectionChange.Cleared:
                Changed?.Invoke();
                break;
        }
    }

    public void ClickRow(Seat side, RowType row) => PlayOn(Targets.ForRow(side, row));

    /// <summary>A click on a row card targets it (decoy); otherwise it counts as a click on its row.</summary>
    public void ClickCard(int cardId, Seat side, RowType row) => PlayOn(Targets.ForCard(cardId) ?? Targets.ForRow(side, row));

    public void ClickField() => PlayOn(Targets.ForBoard());

    /// <summary>Plays the selection on the player's discard pile if it acts on it (medic). Returns whether it did.</summary>
    public bool ClickDiscard()
    {
        if (!Targets.AffectsOwnDiscard)
        {
            return false;
        }

        PlayOn(Targets.ForBoard());
        return true;
    }

    /// <summary>Drops the selected hand card or leader ability, if any. Returns whether there was one.</summary>
    public bool Clear()
    {
        if (!_selection.Clear())
        {
            return false;
        }

        Changed?.Invoke();
        return true;
    }

    /// <summary>Drops the selection without notifying (a command is about to be applied).</summary>
    public void Reset() => _selection.Clear();

    private void PlayOn(GameCommand? command)
    {
        if (command is not null)
        {
            Play?.Invoke(command);
        }
    }
}
