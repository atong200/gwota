using Godot;
using Gwota.Core.Cards;
using Gwota.Core.Data;
using Gwota.Core.DeckBuilding;
using Gwota.Game.Infrastructure;

namespace Gwota.Game.Ui;

/// <summary>
/// Card collection and deck builder: the book on the left, the decks or the open deck on the right. An open deck
/// narrows the book to its faction and Neutral; clicking cards and deck lines adds and removes copies.
/// </summary>
public sealed partial class CollectionScreen : Control
{
    private const float PaneWidth = 360;
    private const double AddFlightDuration = 0.38;
    private const double PulseDuration = 0.35;

    private readonly GameData _data;
    private readonly DeckStore _store;
    private readonly SettingsStore _settings;
    private readonly Action _onBack;
    private readonly BookView _book = new();
    private readonly CardBook _collection;
    private readonly PanelContainer _pane;
    private readonly Control _effects = new() { MouseFilter = MouseFilterEnum.Ignore };

    private DeckEditPane? _editing;

    public CollectionScreen(GameData data, DeckStore store, SettingsStore settings, Action onBack)
    {
        _data = data ?? throw new ArgumentNullException(nameof(data));
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _onBack = onBack ?? throw new ArgumentNullException(nameof(onBack));
        _collection = CardBook.ForCollection(data.Cards.Values);

        SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(new ColorRect { Color = Palette.Background, AnchorRight = 1, AnchorBottom = 1, MouseFilter = MouseFilterEnum.Ignore });

        _book.SlotClicked += OnSlotClicked;
        _book.BackPressed += Back;
        _pane = Widgets.Panel(new Control(), Widgets.Box(Palette.Panel, Palette.PanelBorder, 1, radius: 10, margin: 14));
        _pane.CustomMinimumSize = new Vector2(PaneWidth, 0);

        var body = new HBoxContainer();
        body.SetAnchorsPreset(LayoutPreset.FullRect);
        body.AddThemeConstantOverride("separation", 18);
        body.AddChild(_book);
        body.AddChild(_pane);

        var margin = new MarginContainer();
        margin.SetAnchorsPreset(LayoutPreset.FullRect);
        foreach (var side in new[] { "left", "right", "top", "bottom" })
        {
            margin.AddThemeConstantOverride($"margin_{side}", 18);
        }

        margin.AddChild(body);
        AddChild(margin);
        _effects.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(_effects);

        Browse();
    }

    private double DurationScale => _settings.Current.Gameplay.DurationScale;

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed("ui_cancel"))
        {
            GetViewport().SetInputAsHandled();
            Back();
        }
    }

    /// <summary>One level up: an open deck closes, otherwise back to the main menu.</summary>
    private void Back()
    {
        if (_editing is null)
        {
            _onBack();
        }
        else
        {
            Browse();
        }
    }

    /// <summary>The whole collection, with the deck list on the right. Slots show how many copies a deck may hold.</summary>
    private void Browse()
    {
        if (_editing is { } closing)
        {
            // Land on the closed deck's faction rather than wherever the collection was left.
            _collection.Tab = closing.Editor.Faction;
        }

        _editing = null;
        var list = new DeckListPane(_data, _store);
        list.EditRequested += Edit;
        list.NewDeckRequested += NewDeck;
        SetPane(list);
        _book.Open(_collection, card => new SlotState(DeckRules.Classic.MaxCopiesOf(card), CanAdd: true));
    }

    /// <summary>Opens a deck: the pane shows it and the book narrows to its faction and Neutral.</summary>
    private void Edit(string deckId)
    {
        if (_store.Collection.Find(deckId) is not { } deck)
        {
            return;
        }

        var pane = new DeckEditPane(_data, _store, deck, () => DurationScale);
        pane.DoneRequested += Browse;
        pane.Deleted += () =>
        {
            _editing = null;
            Browse();
        };
        pane.CardsChanged += _book.RefreshSlots;
        _editing = pane;
        SetPane(pane);

        var editor = pane.Editor;
        _book.Open(CardBook.ForDeck(_data.Cards.Values, deck.Faction), card => new SlotState(editor.Remaining(card), editor.AddRefusal(card) is null));
        _book.ShowHint("Click a card to add it to the deck.");
    }

    private void SetPane(Control content)
    {
        _pane.ClearChildren();
        _pane.AddChild(content);
    }

    private void NewDeck() => ChoiceDialog.Show(
        this,
        "New deck",
        "Choose the deck's faction. It cannot be changed later.",
        FactionChoice(Faction.Realms),
        FactionChoice(Faction.Hells));

    private Choice FactionChoice(Faction faction) =>
        new(FactionInfo.Name(faction), () => Edit(_store.Create(faction, _data).Id), Palette.ForFaction(faction));

    private void OnSlotClicked(BookSlot slot)
    {
        if (_editing is not { } pane)
        {
            _book.ShowHint("Open or create a deck to add cards.");
            return;
        }

        var card = slot.Card;
        if (pane.Editor.AddRefusal(card) is { } refusal)
        {
            _book.ShowHint(refusal, isError: true);
            return;
        }

        pane.Editor.TryAdd(card.Id);
        pane.Save();
        pane.Render();
        _book.ShowHint(string.Empty);
        FlyToDeck(pane, slot.View, card.Id);
        _book.RefreshSlots();
    }

    /// <summary>A copy of the card shrinks from the book into its line of the deck list, which then pulses.</summary>
    private void FlyToDeck(DeckEditPane pane, CardView source, string cardId)
    {
        var scale = DurationScale;
        if (scale <= 0 || pane.RowFor(cardId) is not { } row)
        {
            return;
        }

        pane.LinesScroll.EnsureControlVisible(row);
        var ghost = new CardView(source.Card, source.Size) { ShowsPreview = false, MouseFilter = MouseFilterEnum.Ignore };
        _effects.AddChild(ghost);
        ghost.GlobalPosition = source.GlobalPosition;

        // The deck list lays its new line out at the end of this frame (and may scroll): aim once that is done.
        GetTree().Connect(SceneTree.SignalName.ProcessFrame, Callable.From(() =>
        {
            if (!IsInstanceValid(ghost))
            {
                return;
            }

            var duration = AddFlightDuration * scale;
            var end = IsInstanceValid(row) && row.IsInsideTree() ? row.GetGlobalRect() : pane.LinesScroll.GetGlobalRect();
            var shrink = Math.Max(0.12f, end.Size.Y / ghost.Size.Y);
            var target = new Vector2(end.Position.X, end.GetCenter().Y - (ghost.Size.Y * shrink / 2));

            var tween = ghost.CreateTween().SetParallel().SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.In);
            tween.TweenProperty(ghost, "global_position", target, duration);
            tween.TweenProperty(ghost, "scale", new Vector2(shrink, shrink), duration);
            tween.TweenProperty(ghost, "modulate:a", 0.2f, duration);
            tween.Chain().TweenCallback(Callable.From(() =>
            {
                ghost.QueueFree();
                if (IsInstanceValid(row))
                {
                    row.Pulse(PulseDuration * scale);
                }
            }));
        }), (uint)ConnectFlags.OneShot);
    }
}