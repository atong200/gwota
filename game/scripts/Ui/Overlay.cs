using Godot;
using Gwota.Core.Cards;

namespace Gwota.Game.Ui;

/// <summary>Modal dialog over the board: a title, a message, optional clickable cards and buttons.</summary>
public sealed partial class Overlay : Control
{
    /// <summary>Larger faces for the draft phase, where the hand is the only thing to look at.</summary>
    public static readonly Vector2 DraftCardSize = new(124, 176);

    private const int CardSeparation = 6;
    private const int MaxCardsPerLine = 10;
    private const float MinCardsWidth = 960;
    private const double FadeOutDuration = 0.35;
    private const double HoldDuration = 0.5;

    private readonly Label _title;
    private readonly Label _message;
    private readonly HFlowContainer _cards;
    private readonly HBoxContainer _buttons;
    private readonly CardFlights _flights = new();
    private Vector2 _cardSize = CardView.HandSize;
    private Tween? _fade;

    public Overlay()
    {
        SetAnchorsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;
        Visible = false;

        var dim = new ColorRect { Color = Palette.Dim, MouseFilter = MouseFilterEnum.Ignore };
        dim.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(dim);

        _title = Widgets.Label(string.Empty, 0, Palette.Highlight, HorizontalAlignment.Center).WithDisplayFont(30);
        _message = Widgets.Label(string.Empty, 18, Palette.Text, HorizontalAlignment.Center);
        _message.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _cards = new HFlowContainer { Alignment = FlowContainer.AlignmentMode.Center };
        _cards.AddThemeConstantOverride("h_separation", CardSeparation);
        _cards.AddThemeConstantOverride("v_separation", CardSeparation);
        _buttons = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        _buttons.AddThemeConstantOverride("separation", 16);

        var layout = new VBoxContainer();
        layout.AddThemeConstantOverride("separation", 14);
        layout.AddChild(_title);
        layout.AddChild(_message);
        layout.AddChild(_cards);
        layout.AddChild(_buttons);

        var center = new CenterContainer { MouseFilter = MouseFilterEnum.Ignore };
        center.SetAnchorsPreset(LayoutPreset.FullRect);
        center.AddChild(Widgets.Panel(layout, Widgets.Box(Palette.Panel, Palette.Highlight, 2, radius: 10, margin: 24)));
        AddChild(center);
        AddChild(_flights);
    }

    /// <summary>True while <see cref="AnimateRedraw"/> runs; the dialog ignores clicks meanwhile.</summary>
    public bool IsAnimating { get; private set; }

    public void Open(
        string title,
        string message,
        IEnumerable<CardInstance> cards,
        Action<CardInstance>? onCardClicked,
        params (string Text, Action OnPressed)[] buttons) =>
        Open(title, message, cards, CardView.HandSize, onCardClicked, buttons);

    public void Open(
        string title,
        string message,
        IEnumerable<CardInstance> cards,
        Vector2 cardSize,
        Action<CardInstance>? onCardClicked,
        params (string Text, Action OnPressed)[] buttons)
    {
        ArgumentNullException.ThrowIfNull(cards);

        _title.Text = title.ToUpperInvariant();
        _message.Text = message;
        _message.Visible = message.Length > 0;
        _cardSize = cardSize;

        _cards.ClearChildren();
        foreach (var card in cards)
        {
            var view = new CardView(card, cardSize);
            if (onCardClicked is not null)
            {
                view.Clicked += v =>
                {
                    if (!IsAnimating)
                    {
                        onCardClicked(v.Card);
                    }
                };
            }

            _cards.AddChild(view);
        }

        var count = _cards.GetChildCount();
        var perLine = Math.Clamp(count, 1, MaxCardsPerLine);
        _cards.CustomMinimumSize = new Vector2(Math.Max(MinCardsWidth, (perLine * (cardSize.X + CardSeparation)) - CardSeparation), 0);
        _cards.Visible = count > 0;

        SetButtons(buttons);
        Visible = true;
    }

    /// <summary>Replaces the buttons only (e.g. a label that follows the selection) without rebuilding the cards.</summary>
    public void SetButtons(params (string Text, Action OnPressed)[] buttons)
    {
        ArgumentNullException.ThrowIfNull(buttons);

        _buttons.ClearChildren();
        foreach (var (text, onPressed) in buttons)
        {
            _buttons.AddChild(Widgets.Button(text, onPressed).AsDisplay(20));
        }

        _buttons.Visible = buttons.Length > 0;
        _buttons.Modulate = Colors.White;
    }

    public void Close() => Visible = false;

    /// <summary>Cancels a running <see cref="AnimateRedraw"/> without calling its completion (e.g. on forfeit).</summary>
    public void StopAnimations()
    {
        _fade?.Kill();
        _fade = null;
        _flights.Clear();
        IsAnimating = false;
    }

    /// <summary>Re-highlights the dialog's cards (e.g. the selected ones during the draft phase).</summary>
    public void Highlight(Func<CardInstance, CardHighlight> highlight)
    {
        ArgumentNullException.ThrowIfNull(highlight);
        foreach (var view in _cards.GetChildren().OfType<CardView>())
        {
            view.SetHighlight(highlight(view.Card));
        }
    }

    /// <summary>
    /// Draft redraw: returned cards fade out, drawn cards fly face down from <paramref name="drawPile"/> into the freed
    /// slots and flip up, then <paramref name="onFinished"/> runs after a short hold.
    /// </summary>
    public void AnimateRedraw(IReadOnlyList<(int ReturnedId, CardInstance Drawn)> swaps, Rect2 drawPile, float durationScale, Action onFinished)
    {
        ArgumentNullException.ThrowIfNull(swaps);
        ArgumentNullException.ThrowIfNull(onFinished);

        var slots = swaps
            .Select(s => (Old: _cards.GetChildren().OfType<CardView>().FirstOrDefault(v => v.Card.Id == s.ReturnedId), s.Drawn))
            .Where(s => s.Old is not null)
            .Select(s => (Old: s.Old!, s.Drawn))
            .ToList();

        IsAnimating = true;

        // Hide the buttons but keep their space, so the dialog does not shrink and jump mid-animation.
        foreach (var button in _buttons.GetChildren().OfType<Button>())
        {
            button.Disabled = true;
        }

        _buttons.Modulate = Colors.Transparent;

        var fade = _fade = CreateTween().SetParallel();
        fade.TweenInterval(0.01);
        foreach (var (old, _) in slots)
        {
            old.SetHighlight(CardHighlight.None);
            fade.TweenProperty(old, "modulate:a", 0f, FadeOutDuration * durationScale);
        }

        fade.Chain().TweenCallback(Callable.From(() => FlyIn(slots, drawPile, durationScale, onFinished)));
    }

    private void FlyIn(List<(CardView Old, CardInstance Drawn)> slots, Rect2 drawPile, float durationScale, Action onFinished)
    {
        var flights = new List<CardFlight>();
        foreach (var (old, drawn) in slots)
        {
            var target = old.GetGlobalRect();
            var replacement = new CardView(drawn, _cardSize) { Modulate = Colors.Transparent };
            _cards.AddChild(replacement);
            _cards.MoveChild(replacement, old.GetIndex());
            _cards.RemoveChild(old);
            old.QueueFree();

            flights.Add(new CardFlight(drawn, drawPile, target, FaceUpAtStart: false)
            {
                OnLanded = () => replacement.Modulate = Colors.White,
            });
        }

        _flights.DurationScale = durationScale;
        _flights.Fly(flights, HoldDuration, () =>
        {
            _flights.Clear();
            IsAnimating = false;
            onFinished();
        });
    }
}