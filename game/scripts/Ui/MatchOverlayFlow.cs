using Gwota.Core.Cards;
using Gwota.Core.Common;
using Gwota.Core.Match;
using Gwota.Core.Presentation;
using Gwota.Core.Session;

namespace Gwota.Game.Ui;

/// <summary>
/// Picks what the overlay shows between moves: the draft, a pending choice, the end of the match or a stopped
/// remote match.
/// </summary>
/// <remarks><paramref name="rematch"/> starts a new match against the computer; a remote rematch is requested through <paramref name="driver"/>.</remarks>
public sealed class MatchOverlayFlow(Overlay overlay, MatchSession session, IMatchLink? link, Seat local, INavigator navigator, Action rematch, MatchDriver driver)
{
    private DraftSelection? _draft;
    private DraftSelection? _shownDraft;

    private string OpponentName => session.State.Player(local.Opponent()).Name;

    public void Show(bool localCanAct)
    {
        var state = session.State;
        if (state.Phase != MatchPhase.Draft)
        {
            _draft = null;
        }

        if (state.Phase == MatchPhase.Finished)
        {
            ShowEnd();
        }
        else if (link?.CloseReason is { } reason)
        {
            overlay.Open(MatchTexts.StoppedTitle, reason, [], null, MainMenuButton());
        }
        else if (state.Phase == MatchPhase.Draft && localCanAct)
        {
            ShowDraft();
        }
        else if (state.Phase == MatchPhase.Draft && link is not null && session.CanAct(local))
        {
            // The draft was sent; its redraw animation plays once it comes back.
            overlay.SetButtons();
        }
        else if (state.Phase == MatchPhase.Draft && link is not null)
        {
            overlay.Open(MatchTexts.DraftTitle, MatchTexts.WaitingForDraft(OpponentName), [], null);
        }
        else if (state.PendingChoice is { } choice && choice.Seat == local && localCanAct)
        {
            var title = MatchTexts.ChoiceTitle(choice.Kind);
            var cards = choice.Options.Select(state.FindCard).OfType<CardInstance>();
            overlay.Open(title, string.Empty, cards, card => driver.Submit(new ResolveChoiceCommand(local, card.Id)));
        }
        else
        {
            overlay.Close();
        }
    }

    /// <summary>Forgets the cards picked for redraw (the draft command is being applied or sent).</summary>
    public void ResetDraft() => _draft = null;

    private void ShowEnd()
    {
        var end = MatchTexts.End(session.State, local, link is null ? null : LinkState.Of(link));
        var menu = MainMenuButton();
        if (end.RematchLabel is not { } rematchLabel)
        {
            overlay.Open(end.Title, end.Body, [], null, menu);
            return;
        }

        Action onRematch = link is null ? rematch : driver.RequestRematch;
        overlay.Open(end.Title, end.Body, [], null, (rematchLabel, onRematch), menu);
    }

    private (string, Action) MainMenuButton() => ("Main menu", () => navigator.ShowMainMenu());

    /// <summary>Draft phase: clicking toggles a card's selection; the button redraws the selection (or keeps the hand).</summary>
    private void ShowDraft()
    {
        var draft = _draft ??= new DraftSelection(session.State.Player(local));
        if (_shownDraft == draft && overlay.Visible)
        {
            UpdateDraft(draft);
            return;
        }

        _shownDraft = draft;
        overlay.Open(MatchTexts.DraftTitle, draft.Instructions, session.State.Player(local).Hand, Overlay.DraftCardSize, card =>
        {
            if (draft.Toggle(card.Id))
            {
                UpdateDraft(draft);
            }
        });
        UpdateDraft(draft);
    }

    /// <summary>Updates highlights and the button in place, so hovering (and the card preview) is not interrupted.</summary>
    private void UpdateDraft(DraftSelection draft)
    {
        overlay.Highlight(card => draft.IsSelected(card.Id) ? CardHighlight.Redraw : CardHighlight.None);
        overlay.SetGameButton(draft.ConfirmText, () => driver.SubmitDraft(draft.ToCommand()));
    }
}
