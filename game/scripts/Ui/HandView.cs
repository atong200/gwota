using Godot;
using Gwota.Core.Presentation;

namespace Gwota.Game.Ui;

/// <summary>
/// The player's hand, laid out by <see cref="HandLayout"/>: side by side when the cards fit, overlapping otherwise,
/// with a focus window around the pointed card. Cards slide to their places. A new card list keeps each remaining
/// card where it was, so gaps close smoothly, while added cards appear at their final place (flights land there).
/// Processing stops once every card has settled and resumes when the cards, the size or the pointer change.
/// </summary>
public sealed partial class HandView : Control
{
    public const float Gap = 4;

    /// <summary>Easing time constant of a slide at normal speed, in seconds.</summary>
    private const float SlideTime = 0.05f;

    private readonly List<CardView> _cards = [];
    private float[] _targets = [];

    public HandView()
    {
        MouseFilter = MouseFilterEnum.Pass;
        CustomMinimumSize = new Vector2(0, CardView.HandSize.Y + 16);
        Resized += SnapToTargets;
    }

    /// <summary>Multiplies the slide time; 0 moves cards at once.</summary>
    public float DurationScale { get; set; } = 1;

    /// <summary>
    /// Shows <paramref name="cards"/>, in hand order (each one covers its left neighbor). The views are owned by the
    /// caller: views dropped from the hand are detached, not freed.
    /// </summary>
    public void SetCards(IReadOnlyList<CardView> cards)
    {
        ArgumentNullException.ThrowIfNull(cards);

        var previous = _cards.ToDictionary(card => card.Card.Id, card => card.Position.X);
        this.SyncChildren(cards);
        _cards.Clear();
        _cards.AddRange(cards);

        var targets = Targets();
        for (var i = 0; i < _cards.Count; i++)
        {
            var card = _cards[i];
            card.Size = CardView.HandSize;
            card.Position = new Vector2(previous.GetValueOrDefault(card.Card.Id, targets[i]), Top);
        }

        SetProcess(true);
    }

    public override void _Ready() => SetProcess(_cards.Count > 0);

    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventMouseMotion && _cards.Count > 0)
        {
            SetProcess(true);
        }
    }

    public override void _Process(double delta)
    {
        var targets = Targets();
        var timeConstant = SlideTime * DurationScale;
        var settled = true;
        for (var i = 0; i < _cards.Count; i++)
        {
            var position = _cards[i].Position;
            var next = new Vector2(HandLayout.Ease(position.X, targets[i], (float)delta, timeConstant), Top);
            if (next != position)
            {
                _cards[i].Position = next;
            }

            settled &= next.X == targets[i];
        }

        if (settled)
        {
            SetProcess(false);
        }
    }

    private float Top => MathF.Round((Size.Y - CardView.HandSize.Y) / 2);

    /// <summary>The resting left edge of each card for the current pointer, in a buffer reused between frames.</summary>
    private ReadOnlySpan<float> Targets()
    {
        if (_targets.Length != _cards.Count)
        {
            _targets = new float[_cards.Count];
        }

        HandLayout.Fill(_targets, CardView.HandSize.X, Gap, Size.X, PointerX());
        return _targets;
    }

    /// <summary>The pointer's x when it is over the hand or one of its cards, not over something drawn on top.</summary>
    private float? PointerX() =>
        GetViewport().GuiGetHoveredControl() is { } hovered && (hovered == this || IsAncestorOf(hovered))
            ? GetLocalMousePosition().X
            : null;

    private void SnapToTargets()
    {
        var targets = Targets();
        for (var i = 0; i < _cards.Count; i++)
        {
            _cards[i].Position = new Vector2(targets[i], Top);
        }
    }
}
