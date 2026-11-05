using Godot;
using Gwota.Core.Cards;
using Gwota.Core.Common;
using Gwota.Core.Data;
using Gwota.Core.Match;
using Gwota.Core.Presentation;

namespace Gwota.Game.Ui;

/// <summary>
/// The board column: the opponent's hand on top, the field (the opponent's rows, the weather lane, the player's rows)
/// and the player's hand. Card views are kept by card id across renders; it reports clicks and knows where each
/// card and zone is drawn, for flights.
/// </summary>
public sealed partial class MatchBoardView : VBoxContainer
{
    private static readonly RowType[] RowsFromCenter = [RowType.Melee, RowType.Ranged, RowType.Siege];

    private readonly Dictionary<(Seat Side, RowType Row), RowView> _rows = [];
    private readonly OpponentHandView _opponentHand = new();
    private readonly HandView _hand = new() { SizeFlagsHorizontal = SizeFlags.ExpandFill };
    private readonly VBoxContainer _field = new() { SizeFlagsVertical = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Stop };
    private readonly HBoxContainer _weather = new() { Alignment = AlignmentMode.Center, MouseFilter = MouseFilterEnum.Ignore };
    private readonly StyleBoxFlat _weatherStyle = Widgets.Box(new Color(0, 0, 0, 0.15f), new Color(0, 0, 0, 0), 2, radius: 4, margin: 0);
    private readonly Label _clearSkies = Widgets.Label("Clear skies", 16, Palette.Muted);
    private readonly CardViewCache _boardViews;
    private readonly CardViewCache _handViews;
    private readonly Dictionary<int, (Seat Side, RowType Row)> _rowOf = [];
    private readonly HashSet<int> _hidden = [];
    private bool _handActive;

    public MatchBoardView(Seat own, CardBackStyle opponentCardBack)
    {
        _opponentHand.BackStyle = opponentCardBack;
        _boardViews = new CardViewCache(CardView.BoardSize, view =>
        {
            view.MouseDefaultCursorShape = CursorShape.Arrow;
            view.Clicked += OnBoardCardClicked;
        });
        _handViews = new CardViewCache(CardView.HandSize, view => view.Clicked += OnHandCardClicked);

        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        this.WithGap(4);
        AddChild(_opponentHand);

        // Clicks that reach the field (gaps, weather lane) play board-wide cards; rows and cards accept their own.
        _field.WithGap(4);
        _field.GuiInput += OnFieldInput;
        foreach (var row in RowsFromCenter.Reverse())
        {
            _field.AddChild(CreateRow(own.Opponent(), row));
        }

        _clearSkies.VerticalAlignment = VerticalAlignment.Center;
        _weather.AddChild(_clearSkies);
        var weatherLane = Widgets.Panel(_weather, _weatherStyle);
        weatherLane.CustomMinimumSize = new Vector2(0, CardView.BoardSize.Y);
        weatherLane.MouseFilter = MouseFilterEnum.Ignore;
        _field.AddChild(weatherLane);

        foreach (var row in RowsFromCenter)
        {
            _field.AddChild(CreateRow(own, row));
        }

        AddChild(_field);
        AddChild(_hand);
    }

    public event Action<Seat, RowType>? RowClicked;

    /// <summary>A card on a row (unit or horn) was clicked: its id, side and row.</summary>
    public event Action<int, Seat, RowType>? CardClicked;

    /// <summary>The field outside the rows, or a weather card, was clicked.</summary>
    public event Action? FieldClicked;

    /// <summary>A hand card was clicked while the hand is active.</summary>
    public event Action<CardInstance>? HandCardClicked;

    /// <summary>
    /// Shows the rows, weather and hand of <paramref name="engine"/>'s state (not the row scores, see
    /// <see cref="ShowScores"/>). Hidden cards stay invisible in their slots.
    /// </summary>
    public void Render(MatchEngine engine, Seat own, PlayTargets targets, int? selectedCardId, bool handActive, float durationScale)
    {
        ArgumentNullException.ThrowIfNull(engine);

        var state = engine.State;
        _boardViews.Begin();
        _handViews.Begin();
        _rowOf.Clear();

        foreach (var ((side, rowType), view) in _rows)
        {
            var row = state.Player(side).Row(rowType);
            var cards = new List<CardView>(row.Cards.Count);
            foreach (var card in row.Cards)
            {
                cards.Add(_boardViews.Take(card, engine.Strength(row, card)));
                _rowOf[card.Id] = (side, rowType);
            }

            CardView? horn = null;
            if (row.HornCard is { } hornCard)
            {
                horn = _boardViews.Take(hornCard);
                _rowOf[hornCard.Id] = (side, rowType);
            }

            view.SetContent(state.IsWeatherActive(rowType), cards, horn, row.HasLeaderHorn);
        }

        _clearSkies.Visible = state.Weather.Count == 0;
        _weather.SyncChildren([_clearSkies, .. state.Weather.Select(card => _boardViews.Take(card))]);

        _handActive = handActive;
        var hand = state.Player(own).Hand.Select(card => _handViews.Take(card)).ToList();
        foreach (var view in hand)
        {
            view.MouseDefaultCursorShape = handActive ? CursorShape.PointingHand : CursorShape.Arrow;
        }

        _hand.DurationScale = durationScale;
        _hand.SetCards(hand);

        _boardViews.Sweep();
        _handViews.Sweep();
        foreach (var view in AllViews())
        {
            view.Modulate = _hidden.Contains(view.Card.Id) ? Colors.Transparent : Colors.White;
        }

        ApplyHighlights(targets, selectedCardId);
    }

    /// <summary>Outlines what the selection can act on and the selected hand card, without rebuilding anything.</summary>
    public void ApplyHighlights(PlayTargets targets, int? selectedCardId)
    {
        ArgumentNullException.ThrowIfNull(targets);

        foreach (var ((side, row), view) in _rows)
        {
            view.SetTargetable(targets.Highlights(side, row));
        }

        foreach (var view in _boardViews.Views)
        {
            var target = _rowOf.ContainsKey(view.Card.Id) && targets.AcceptsCard(view.Card.Id);
            view.SetHighlight(target ? CardHighlight.Target : CardHighlight.None);
        }

        foreach (var view in _handViews.Views)
        {
            view.SetHighlight(view.Card.Id == selectedCardId ? CardHighlight.Selected : CardHighlight.None);
        }

        _weatherStyle.BorderColor = targets.AffectsWeatherLane ? Palette.Target : new Color(0, 0, 0, 0);
    }

    public void ShowScores(MatchEngine engine)
    {
        ArgumentNullException.ThrowIfNull(engine);

        foreach (var ((side, row), view) in _rows)
        {
            view.SetScore(engine.RowScore(side, row));
        }
    }

    public void ShowOpponentHand(string ownerName, int count) => _opponentHand.Show(ownerName, count);

    /// <summary>Keeps these cards invisible in their slots until <see cref="Reveal"/> (their flights land there).</summary>
    public void Hide(IEnumerable<int> cardIds) => _hidden.UnionWith(cardIds);

    public void Reveal(int cardId)
    {
        _hidden.Remove(cardId);
        if (TryGetView(cardId, out var view))
        {
            view.Modulate = Colors.White;
        }
    }

    public void ClearHidden() => _hidden.Clear();

    /// <summary>Where each card on screen is drawn now, and the strength it shows.</summary>
    public Dictionary<int, CardOrigin> Capture() =>
        AllViews()
            .Where(view => view.IsInsideTree())
            .ToDictionary(view => view.Card.Id, view => new CardOrigin(view.GetGlobalRect(), view.CurrentStrength));

    /// <summary>The view showing card <paramref name="cardId"/> on a row, the weather lane or the hand.</summary>
    public bool TryGetView(int cardId, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out CardView? view) =>
        _boardViews.TryGet(cardId, out view) || _handViews.TryGet(cardId, out view);

    public Rect2 RowRect(Seat side, RowType row) => _rows[(side, row)].GetGlobalRect();

    public Rect2 HandRect => _hand.GetGlobalRect();

    public Rect2 OpponentHandRect => _opponentHand.GetGlobalRect();

    public Rect2 WeatherRect => _weather.GetGlobalRect();

    public Rect2 FieldRect => _field.GetGlobalRect();

    private IEnumerable<CardView> AllViews() => _boardViews.Views.Concat(_handViews.Views).Where(IsInstanceValid);

    private RowView CreateRow(Seat side, RowType row)
    {
        var view = new RowView(side, row);
        view.Clicked += v => RowClicked?.Invoke(v.Side, v.Row);
        _rows[(side, row)] = view;
        return view;
    }

    /// <summary>A click on a row card targets it (decoy) or its row; a click on a weather card counts as one on the field.</summary>
    private void OnBoardCardClicked(CardView view)
    {
        if (_rowOf.TryGetValue(view.Card.Id, out var place))
        {
            CardClicked?.Invoke(view.Card.Id, place.Side, place.Row);
        }
        else
        {
            FieldClicked?.Invoke();
        }
    }

    private void OnHandCardClicked(CardView view)
    {
        if (_handActive)
        {
            HandCardClicked?.Invoke(view.Card);
        }
    }

    private void OnFieldInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
        {
            FieldClicked?.Invoke();
        }
    }
}
