using Godot;
using Gwota.Core.Cards;
using Gwota.Core.Common;
using Gwota.Core.DeckBuilding;
using Gwota.Core.Presentation;

namespace Gwota.Game.Ui;

/// <summary>
/// The deck builder's card book: faction tabs, row filters, a 5 x 2 card grid and a pager. The owner decides what each
/// slot offers (<see cref="Open"/>).
/// </summary>
public sealed partial class BookView : VBoxContainer
{
    private const float CounterHeight = 30;
    private const int TabRadius = 8;

    private readonly HBoxContainer _tabs = new();
    private readonly Dictionary<Faction, Button> _tabButtons = [];
    private readonly Dictionary<BookFilter, Button> _filters = [];
    private readonly StyleBoxFlat _pageStyle = Widgets.Box(Palette.Panel, Palette.PanelBorder, 1, radius: 10, margin: 16);
    private readonly Control _grid = new() { SizeFlagsVertical = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Pass, ClipContents = false };
    private readonly Label _hint = Widgets.Label(string.Empty, 18, Palette.Muted, HorizontalAlignment.Center);
    private readonly Label _page = Widgets.Label(string.Empty, 18, Palette.Text, HorizontalAlignment.Center);
    private readonly Button _previous;
    private readonly Button _next;
    private readonly List<BookSlot> _slots = [];

    private CardBook? _book;
    private Func<CardDefinition, SlotState> _stateOf = _ => new SlotState(0, false);
    private BookGrid? _layout;
    private int _nextInstanceId = 1;

    public BookView()
    {
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        AddThemeConstantOverride("separation", 0);
        _pageStyle.CornerRadiusTopLeft = 0;

        _previous = PagerButton(Icons.PreviousIcon, "Previous page", () => Flip(_book?.PreviousPage() ?? false));
        _next = PagerButton(Icons.NextIcon, "Next page", () => Flip(_book?.NextPage() ?? false));

        var header = new HBoxContainer();
        _tabs.AddThemeConstantOverride("separation", 4);
        header.AddChild(_tabs);
        header.AddChild(Widgets.Spacer(vertical: false));
        header.AddChild(BuildFilters());
        AddChild(header);

        _grid.Resized += OnGridResized;
        var page = new VBoxContainer();
        page.AddThemeConstantOverride("separation", 10);
        page.AddChild(_grid);
        page.AddChild(BuildFooter());
        var frame = Widgets.Panel(page, _pageStyle);
        frame.SizeFlagsVertical = SizeFlags.ExpandFill;
        AddChild(frame);
    }

    /// <summary>A slot was clicked (the owner decides: add a copy, or explain why not).</summary>
    public event Action<BookSlot>? SlotClicked;

    public event Action? BackPressed;

    public CardBook? Book => _book;

    /// <summary>Shows <paramref name="book"/>; <paramref name="stateOf"/> says how many copies each card offers.</summary>
    public void Open(CardBook book, Func<CardDefinition, SlotState> stateOf)
    {
        _book = book ?? throw new ArgumentNullException(nameof(book));
        _stateOf = stateOf ?? throw new ArgumentNullException(nameof(stateOf));
        _hint.Text = string.Empty;
        BuildTabs(book);
        Render();
    }

    /// <summary>Re-reads every visible slot's copies (after a card was added or removed).</summary>
    public void RefreshSlots()
    {
        foreach (var slot in _slots)
        {
            slot.Show(_stateOf(slot.Card));
        }
    }

    /// <summary>A line under the page: guidance in muted text, refusals in red. Cleared when the page changes.</summary>
    public void ShowHint(string text, bool isError = false)
    {
        _hint.Text = text;
        _hint.AddThemeColorOverride("font_color", isError ? Palette.Debuff : Palette.Muted);
    }

    /// <summary>The mouse wheel turns pages anywhere over the page, cards included (they stop mouse events).</summary>
    public override void _Input(InputEvent @event)
    {
        if (@event is not InputEventMouseButton { Pressed: true } wheel
            || (wheel.ButtonIndex != MouseButton.WheelDown && wheel.ButtonIndex != MouseButton.WheelUp)
            || GetViewport().GuiGetHoveredControl() is not { } hovered
            || (hovered != _grid && !_grid.IsAncestorOf(hovered)))
        {
            return;
        }

        Flip(wheel.ButtonIndex == MouseButton.WheelDown ? _book?.NextPage() ?? false : _book?.PreviousPage() ?? false);
        GetViewport().SetInputAsHandled();
    }

    private HBoxContainer BuildFilters()
    {
        var filters = new HBoxContainer { SizeFlagsVertical = SizeFlags.ShrinkEnd };
        filters.AddThemeConstantOverride("separation", 6);
        var group = new ButtonGroup();
        foreach (var filter in Enum.GetValues<BookFilter>())
        {
            var button = FilterButton(filter);
            button.ToggleMode = true;
            button.ButtonGroup = group;
            _filters[filter] = button;
            filters.AddChild(button);
        }

        var margin = new MarginContainer { SizeFlagsVertical = SizeFlags.ShrinkEnd };
        margin.AddThemeConstantOverride("margin_bottom", 6);
        margin.AddChild(filters);
        var wrapper = new HBoxContainer { SizeFlagsVertical = SizeFlags.Fill };
        wrapper.AddChild(margin);
        return wrapper;
    }

    private Button FilterButton(BookFilter filter)
    {
        Button button;
        if (filter == BookFilter.All)
        {
            button = Widgets.Button("All", () => SetFilter(filter), 18);
        }
        else
        {
            var icon = filter switch
            {
                BookFilter.Melee => Icons.ForRow(RowType.Melee),
                BookFilter.Ranged => Icons.ForRow(RowType.Ranged),
                BookFilter.Siege => Icons.ForRow(RowType.Siege),
                _ => Icons.SpecialIcon,
            };
            button = new Button { Icon = icon, ExpandIcon = true, FocusMode = FocusModeEnum.None, IconAlignment = HorizontalAlignment.Center };
            button.Pressed += () => SetFilter(filter);
        }

        button.CustomMinimumSize = new Vector2(52, 38);
        button.TooltipText = filter switch
        {
            BookFilter.All => "All cards",
            BookFilter.Special => "Special cards",
            _ => $"{filter} units",
        };
        return button;
    }

    private HBoxContainer BuildFooter()
    {
        var back = Widgets.Button("Back", () => BackPressed?.Invoke(), 21);
        back.CustomMinimumSize = new Vector2(130, 40);
        back.TooltipText = "Back (Esc). Changes are saved automatically.";

        _hint.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _hint.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        _page.CustomMinimumSize = new Vector2(64, 0);

        var pager = new HBoxContainer();
        pager.AddThemeConstantOverride("separation", 4);
        pager.AddChild(_previous);
        pager.AddChild(_page);
        pager.AddChild(_next);

        var footer = new HBoxContainer();
        footer.AddThemeConstantOverride("separation", 12);
        footer.AddChild(back);
        footer.AddChild(_hint);
        footer.AddChild(pager);
        return footer;
    }

    private static Button PagerButton(Texture2D icon, string tooltip, Action onPressed) =>
        Widgets.IconButton(icon, tooltip, onPressed, size: 40);

    private void SetTab(Faction tab)
    {
        if (_book is null)
        {
            return;
        }

        _book.Tab = tab;
        _hint.Text = string.Empty;
        Render();
    }

    private void SetFilter(BookFilter filter)
    {
        if (_book is null)
        {
            return;
        }

        _book.Filter = filter;
        _hint.Text = string.Empty;
        Render();
    }

    private void Flip(bool moved)
    {
        if (moved)
        {
            _hint.Text = string.Empty;
            Render();
        }
    }

    private void Render()
    {
        if (_book is null)
        {
            return;
        }

        foreach (var (faction, button) in _tabButtons)
        {
            StyleTab(button, faction, faction == _book.Tab);
        }

        foreach (var (filter, button) in _filters)
        {
            // Set every button: SetPressedNoSignal does not release the other buttons of a ButtonGroup.
            button.SetPressedNoSignal(filter == _book.Filter);
        }

        _pageStyle.BgColor = Palette.PageTint(_book.Tab);
        _page.Text = $"{_book.Page + 1}/{_book.PageCount}";
        _previous.Disabled = !_book.HasPreviousPage;
        _next.Disabled = !_book.HasNextPage;
        RenderSlots();
    }

    private void BuildTabs(CardBook book)
    {
        _tabs.ClearChildren();
        _tabButtons.Clear();
        foreach (var faction in book.Tabs)
        {
            var button = Widgets.Button(FactionInfo.ShortName(faction), () => SetTab(faction)).AsDisplay(18);
            button.SizeFlagsVertical = SizeFlags.ShrinkEnd;
            button.TooltipText = faction == Faction.Neutral ? "Neutral cards, usable in any deck" : $"{FactionInfo.Name(faction)} cards";
            _tabButtons[faction] = button;
            _tabs.AddChild(button);
        }
    }

    /// <summary>Faction tabs in their page color; the open tab is taller and merges into the page.</summary>
    private static void StyleTab(Button button, Faction faction, bool isOpen)
    {
        var color = Palette.PageTint(faction);
        button.CustomMinimumSize = new Vector2(150, isOpen ? 46 : 40);
        foreach (var state in new[] { "normal", "hover", "pressed", "hover_pressed" })
        {
            var background = isOpen ? color : state == "hover" ? color.Darkened(0.15f) : color.Darkened(0.4f);
            button.AddThemeStyleboxOverride(state, TabBox(background, isOpen));
        }

        button.AddThemeColorOverride("font_color", isOpen ? Palette.Highlight : Palette.Muted);
        button.AddThemeColorOverride("font_hover_color", isOpen ? Palette.Highlight : Palette.Text);
        button.AddThemeColorOverride("font_pressed_color", isOpen ? Palette.Highlight : Palette.Text);
    }

    private static StyleBoxFlat TabBox(Color background, bool isOpen)
    {
        var box = Widgets.Box(background, Palette.PanelBorder, 1, radius: 0, margin: 8);
        box.CornerRadiusTopLeft = box.CornerRadiusTopRight = TabRadius;
        box.BorderWidthBottom = isOpen ? 0 : 1;
        box.ContentMarginLeft = box.ContentMarginRight = 18;
        box.BorderColor = isOpen ? Palette.PanelBorder.Lightened(0.15f) : Palette.PanelBorder;
        return box;
    }

    private void OnGridResized()
    {
        var layout = BookLayout.Fit(_grid.Size.X, _grid.Size.Y, CounterHeight);
        if (_layout is { } current && current.CardWidth == layout.CardWidth && current.Left == layout.Left)
        {
            return;
        }

        _layout = layout;
        RenderSlots();
    }

    private void RenderSlots()
    {
        _grid.ClearChildren();
        _slots.Clear();
        if (_book is null || _layout is not { } layout)
        {
            return;
        }

        var cards = _book.CurrentPage;
        for (var i = 0; i < cards.Count; i++)
        {
            var slot = new BookSlot(new CardInstance(_nextInstanceId++, cards[i], Seat.One), layout);
            var (x, y) = layout.SlotPosition(i);
            slot.Position = new Vector2(x, y);
            slot.Clicked += s => SlotClicked?.Invoke(s);
            slot.Show(_stateOf(cards[i]));
            _slots.Add(slot);
            _grid.AddChild(slot);
        }

        if (cards.Count == 0)
        {
            var empty = Widgets.Label("No card matches this filter.", 20, Palette.Muted, HorizontalAlignment.Center);
            empty.SetAnchorsAndOffsetsPreset(LayoutPreset.CenterTop);
            empty.GrowHorizontal = GrowDirection.Both;
            empty.OffsetTop = 40;
            _grid.AddChild(empty);
        }
    }
}