using Godot;
using Gwota.Core.Cards;
using Gwota.Core.Common;
using Gwota.Core.Match;
using Gwota.Core.Presentation;

namespace Gwota.Game.Ui;

/// <summary>What the screen renders while <see cref="MoveAnimator"/> runs.</summary>
public interface IMoveAnimationHost
{
    /// <summary>Renders the new state with the moving cards hidden and the numbers kept, while the cards fly.</summary>
    void RenderMoving(int pendingOpponentDraws);

    /// <summary>Updates scores, gems and piles once the cards have reached the board and the piles.</summary>
    void RenderLanded();

    void AnimationFinished();
}

public enum AnimationState
{
    Idle,
    Moves,
    Redraw,
}

/// <summary>
/// Animates what a command did: cards enter play (a revealed special or a round end pauses), then go to the piles,
/// then the numbers update and cards are drawn. Also runs the draft redraw.
/// </summary>
public sealed class MoveAnimator(CardFlights flights, Overlay overlay, MatchBoardView board, MatchSidePanel side, Seat own, IMoveAnimationHost host)
{
    private const double RevealHold = 0.7;
    private const double RoundEndHold = 1.1;
    private const float RevealScale = 1.5f;

    private int _run;

    public AnimationState State { get; private set; }

    public bool IsRunning => State != AnimationState.Idle;

    /// <summary>
    /// Flies the cards of <paramref name="plan"/>; <paramref name="origins"/> are where the cards were drawn before the
    /// command (see <see cref="MatchBoardView.Capture"/>).
    /// </summary>
    public void Play(MovePlan plan, IReadOnlyDictionary<int, CardOrigin> origins, float durationScale)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(origins);

        var run = Start(AnimationState.Moves);
        flights.DurationScale = durationScale;

        // Cards leaving a visible spot stay drawn there (as ghosts) until their own stage flies them away.
        foreach (var move in plan.Moves.DistinctBy(m => m.Card.Id))
        {
            if (origins.TryGetValue(move.Card.Id, out var origin))
            {
                var size = move.To.Kind == ZoneKind.Reveal ? RevealRect().Size : origin.Rect.Size;
                flights.Place(move.Card, origin.Rect, size, origin.Strength);
            }
        }

        board.ClearHidden();
        board.Hide(plan.Moves.Where(m => m.To.IsCardSlotFor(own)).Select(m => m.Card.Id));
        host.RenderMoving(plan.Moves.Count(m => m.To == CardZone.Hand(own.Opponent())));

        // Containers lay the new cards out at the end of this frame; their rects are known on the next one.
        flights.GetTree().Connect(
            SceneTree.SignalName.ProcessFrame,
            Callable.From(() => RunStages(run, plan, origins)),
            (uint)GodotObject.ConnectFlags.OneShot);
    }

    /// <summary>
    /// Draft redraw: the returned cards of <paramref name="result"/> fade out of the overlay and the drawn ones fly in
    /// from <paramref name="drawPile"/>. Returns false, without animating, when nothing was redrawn.
    /// </summary>
    public bool PlayRedraw(DraftFinished result, MatchState state, Rect2 drawPile, float durationScale)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(state);

        if (result.ReturnedCardIds.Count == 0 || durationScale <= 0)
        {
            return false;
        }

        var swaps = new List<(int ReturnedId, CardInstance Drawn)>();
        foreach (var (returnedId, drawnId) in result.ReturnedCardIds.Zip(result.DrawnCardIds))
        {
            if (state.FindCard(drawnId) is { } drawn)
            {
                swaps.Add((returnedId, drawn));
            }
        }

        var run = Start(AnimationState.Redraw);
        overlay.AnimateRedraw(swaps, drawPile, durationScale, () => Finish(run));
        return true;
    }

    /// <summary>Drops any running animation without finishing it (e.g. on forfeit).</summary>
    public void Cancel()
    {
        _run++;
        overlay.StopAnimations();
        flights.Clear();
        board.ClearHidden();
        State = AnimationState.Idle;
    }

    private int Start(AnimationState state)
    {
        State = state;
        return ++_run;
    }

    private void RunStages(int run, MovePlan plan, IReadOnlyDictionary<int, CardOrigin> origins)
    {
        if (run != _run || !flights.IsInsideTree())
        {
            return;
        }

        var entering = plan.InStage(MoveStage.EnterPlay);
        var hold = Math.Max(
            entering.Any(m => m.To.Kind == ZoneKind.Reveal) ? RevealHold : 0,
            plan.HoldBeforePiles ? RoundEndHold : 0);

        flights.Fly(Flights(entering, origins), hold, () =>
            flights.Fly(Flights(plan.InStage(MoveStage.ToPile), origins), 0, () =>
            {
                host.RenderLanded();
                flights.Fly(Flights(plan.InStage(MoveStage.ToHand), origins), 0, () => Finish(run));
            }));
    }

    private void Finish(int run)
    {
        if (run != _run)
        {
            return;
        }

        flights.Clear();
        board.ClearHidden();
        State = AnimationState.Idle;
        host.AnimationFinished();
    }

    private List<CardFlight> Flights(IReadOnlyList<CardMove> moves, IReadOnlyDictionary<int, CardOrigin> origins) =>
        moves.Select(move =>
        {
            var id = move.Card.Id;
            int? strength = origins.TryGetValue(id, out var origin) ? origin.Strength
                : board.TryGetView(id, out var target) ? target.CurrentStrength
                : null;
            return new CardFlight(move.Card, ZoneRect(move.From, id, origins), ZoneRect(move.To, id, null), move.From.IsFaceUpFor(own), move.To.IsFaceUpFor(own))
            {
                Strength = strength,
                KeepAtEnd = move.To.Kind == ZoneKind.Reveal,
                OnLanded = () => board.Reveal(id),
            };
        }).ToList();

    /// <summary>
    /// Where a card is drawn in <paramref name="zone"/>: its captured rect (<paramref name="origins"/>, before the
    /// command) or its rendered view (after), otherwise the zone's anchor (pile, opponent hand, reveal spot).
    /// </summary>
    private Rect2 ZoneRect(CardZone zone, int cardId, IReadOnlyDictionary<int, CardOrigin>? origins)
    {
        if (zone.IsCardSlotFor(own))
        {
            if (origins is not null && origins.TryGetValue(cardId, out var origin))
            {
                return origin.Rect;
            }

            if (origins is null && board.TryGetView(cardId, out var view))
            {
                return view.GetGlobalRect();
            }
        }

        var isOwn = zone.Seat == own;
        return zone.Kind switch
        {
            ZoneKind.Deck => side.PileRect(isOwn, PileKind.Draw),
            ZoneKind.Discard => side.PileRect(isOwn, PileKind.Discard),
            ZoneKind.Reveal => RevealRect(),
            ZoneKind.Hand when !isOwn => Centered(board.OpponentHandRect.GetCenter(), CardView.BoardSize * 0.45f),
            ZoneKind.Hand => Centered(board.HandRect.GetCenter(), CardView.HandSize),
            ZoneKind.Row or ZoneKind.Horn when zone.Row is { } row => Centered(board.RowRect(zone.Seat, row).GetCenter(), CardView.BoardSize),
            _ => Centered(board.WeatherRect.GetCenter(), CardView.BoardSize),
        };
    }

    /// <summary>Where one-shot specials (scorch, clear weather) are shown before going to the discard pile.</summary>
    private Rect2 RevealRect() => Centered(board.FieldRect.GetCenter(), CardView.HandSize * RevealScale);

    private static Rect2 Centered(Vector2 center, Vector2 size) => new(center - (size / 2), size);
}
