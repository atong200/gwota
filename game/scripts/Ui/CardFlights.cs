using Godot;
using Gwota.Core.Cards;

namespace Gwota.Game.Ui;

/// <summary>
/// One card moving between two global rects. A face-down end (<see cref="FaceUpAtStart"/> /
/// <see cref="FaceUpAtEnd"/>) shows the card back; when they differ the card flips half-way.
/// </summary>
public sealed record CardFlight(CardInstance Card, Rect2 From, Rect2 To, bool FaceUpAtStart = true, bool FaceUpAtEnd = true)
{
    /// <summary>Strength shown on the face (e.g. the weakened value of a unit leaving the board).</summary>
    public int? Strength { get; init; }

    /// <summary>Keeps the ghost where it lands (a revealed special waiting for its next flight).</summary>
    public bool KeepAtEnd { get; init; }

    /// <summary>Runs when the card lands (e.g. reveals the real card hidden at the destination).</summary>
    public Action? OnLanded { get; init; }
}

/// <summary>
/// Full-screen, click-through layer where ghost cards fly between zones; the game state is already final.
/// Durations follow the animation speed setting and the sequence can be paused.
/// </summary>
public sealed partial class CardFlights : Control
{
    public const double FlyDuration = 0.42;
    private const double MaxStagger = 0.08;
    private const double MaxTotalStagger = 0.6;
    private const double FlipDuration = 0.09;
    private const double FadeInDuration = 0.08;

    private readonly Dictionary<int, Control> _kept = [];
    private Tween? _tween;

    public CardFlights()
    {
        SetAnchorsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore;
    }

    /// <summary>1 for normal speed, 0.5 for fast; 0 means flights should be skipped by the caller.</summary>
    public float DurationScale { get; set; } = 1f;

    public bool IsRunning => _tween is not null && _tween.IsValid() && _tween.IsRunning();

    public void SetPaused(bool paused)
    {
        if (_tween is null || !_tween.IsValid())
        {
            return;
        }

        if (paused)
        {
            _tween.Pause();
        }
        else
        {
            _tween.Play();
        }
    }

    /// <summary>
    /// Flies every card of one stage (staggered), waits <paramref name="holdAfter"/> seconds, then calls
    /// <paramref name="onFinished"/>. An empty stage only holds.
    /// </summary>
    public void Fly(IReadOnlyList<CardFlight> flights, double holdAfter, Action onFinished)
    {
        ArgumentNullException.ThrowIfNull(flights);
        ArgumentNullException.ThrowIfNull(onFinished);

        _tween?.Kill();
        var tween = _tween = CreateTween().SetParallel();
        tween.TweenInterval(0.01);

        var duration = FlyDuration * DurationScale;
        var stagger = flights.Count <= 1 ? 0 : Math.Min(MaxStagger, MaxTotalStagger / (flights.Count - 1)) * DurationScale;
        for (var i = 0; i < flights.Count; i++)
        {
            Launch(tween, flights[i], i * stagger, duration);
        }

        tween.Chain().TweenInterval(Math.Max(0.01, holdAfter * DurationScale));
        tween.Chain().TweenCallback(Callable.From(onFinished));
    }

    /// <summary>
    /// Shows <paramref name="card"/> face up at <paramref name="at"/> until a flight takes it, so it does not blink out
    /// meanwhile. <paramref name="buildSize"/> is the largest size it will be shown at.
    /// </summary>
    public void Place(CardInstance card, Rect2 at, Vector2 buildSize, int? strength)
    {
        ArgumentNullException.ThrowIfNull(card);

        var size = buildSize.X >= at.Size.X ? buildSize : at.Size;
        var ghost = CreateGhost(new CardFlight(card, at, at) { Strength = strength }, size);
        ghost.Modulate = Colors.White;
        ghost.Position = at.Position - GlobalPosition;
        ghost.Scale = Vector2.One * (at.Size.X / size.X);
        ghost.GetNode<Control>("Back").Visible = false;
        _kept[card.Id] = ghost;
    }

    /// <summary>Removes ghosts kept at their destination and stops the running sequence.</summary>
    public void Clear()
    {
        _tween?.Kill();
        _tween = null;
        _kept.Clear();
        this.ClearChildren();
    }

    private void Launch(Tween tween, CardFlight flight, double delay, double duration)
    {
        // Built at the larger size and only scaled down, so text stays crisp.
        var ghost = TakeKept(flight.Card.Id)
            ?? CreateGhost(flight, flight.From.Size.X >= flight.To.Size.X ? flight.From.Size : flight.To.Size);
        var start = flight.From.Size.X / ghost.Size.X;
        var end = flight.To.Size.X / ghost.Size.X;

        ghost.Position = flight.From.Position - GlobalPosition;
        ghost.Scale = new Vector2(start, start);
        var back = ghost.GetNode<Control>("Back");
        var face = ghost.GetNode<Control>("Face");
        back.Visible = !flight.FaceUpAtStart;
        face.Visible = flight.FaceUpAtStart;
        back.Scale = face.Scale = Vector2.One;

        if (ghost.Modulate.A < 1)
        {
            tween.TweenProperty(ghost, "modulate:a", 1f, FadeInDuration * DurationScale).SetDelay(delay);
        }

        tween.TweenProperty(ghost, "position", flight.To.Position - GlobalPosition, duration)
            .SetDelay(delay).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
        tween.TweenProperty(ghost, "scale", new Vector2(end, end), duration)
            .SetDelay(delay).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);

        if (flight.FaceUpAtStart != flight.FaceUpAtEnd)
        {
            var (hide, show) = flight.FaceUpAtStart ? (face, back) : (back, face);
            var flipAt = delay + (duration / 2) - FlipDuration;
            tween.TweenProperty(hide, "scale:x", 0f, FlipDuration).SetDelay(flipAt);
            tween.TweenCallback(Callable.From(() =>
            {
                hide.Visible = false;
                show.Visible = true;
                show.Scale = new Vector2(0, 1);
            })).SetDelay(flipAt + FlipDuration);
            tween.TweenProperty(show, "scale:x", 1f, FlipDuration).SetDelay(flipAt + FlipDuration + 0.001);
        }

        tween.TweenCallback(Callable.From(() =>
        {
            if (flight.KeepAtEnd)
            {
                _kept[flight.Card.Id] = ghost;
            }
            else
            {
                ghost.QueueFree();
            }

            flight.OnLanded?.Invoke();
        })).SetDelay(delay + duration);
    }

    private Control? TakeKept(int cardId)
    {
        if (_kept.Remove(cardId, out var ghost) && IsInstanceValid(ghost))
        {
            return ghost;
        }

        return null;
    }

    private Control CreateGhost(CardFlight flight, Vector2 size)
    {
        var ghost = new Control { Size = size, MouseFilter = MouseFilterEnum.Ignore, Modulate = new Color(1, 1, 1, 0) };
        var back = new CardBack(size) { Name = "Back", PivotOffset = size / 2 };
        var face = new CardView(flight.Card, size, flight.Strength)
        {
            Name = "Face",
            ShowsPreview = false,
            MouseFilter = MouseFilterEnum.Ignore,
            Size = size,
            PivotOffset = size / 2,
        };
        ghost.AddChild(back);
        ghost.AddChild(face);
        AddChild(ghost);
        return ghost;
    }
}
