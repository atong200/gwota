using Godot;
using Gwota.Core.Cards;
using Gwota.Core.Data;
using Gwota.Core.DeckBuilding;
using Gwota.Game.Infrastructure;

namespace Gwota.Game.Ui;

/// <summary>
/// Title screen with simple pages: Title (Play / Decks / Options / Quit) -> Play (PvP / Computer) -> PvP (local
/// network / online) or deck select (the player's own decks; incomplete ones cannot be picked). Escape and the Back
/// buttons return to the previous page.
/// </summary>
public sealed partial class MainMenu : Control
{
    /// <summary>Pages a caller can open the menu on.</summary>
    public enum Page
    {
        Title,
        Pvp,
    }

    private const float ButtonWidth = 360;

    private readonly GameData _data;
    private readonly SettingsStore _settings;
    private readonly DeckStore _decks;
    private readonly Action<DeckDefinition> _onPlayComputer;
    private readonly Action _onPlayLan;
    private readonly Action _onDecks;
    private readonly Action _onQuit;
    private readonly VBoxContainer _page = new();
    private Action? _back;

    public MainMenu(AppServices context, Action<DeckDefinition> onPlayComputer, Action onPlayLan, Action onDecks, Action onQuit, Page startPage = Page.Title)
    {
        ArgumentNullException.ThrowIfNull(context);
        _data = context.Data;
        _settings = context.Settings;
        _decks = context.Decks;
        _onPlayComputer = onPlayComputer ?? throw new ArgumentNullException(nameof(onPlayComputer));
        _onPlayLan = onPlayLan ?? throw new ArgumentNullException(nameof(onPlayLan));
        _onDecks = onDecks ?? throw new ArgumentNullException(nameof(onDecks));
        _onQuit = onQuit ?? throw new ArgumentNullException(nameof(onQuit));
        var version = context.Version;

        SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(new ColorRect { Color = Palette.Background, AnchorRight = 1, AnchorBottom = 1 });

        var layout = new VBoxContainer { CustomMinimumSize = new Vector2(520, 0) };
        layout.AddThemeConstantOverride("separation", 14);
        layout.AddChild(Widgets.Label("GWOTA", 0, Palette.Highlight, HorizontalAlignment.Center).AsDisplay(88));
        layout.AddChild(Widgets.Label("Inspired by GWENT and DOTA 2", 20, Palette.Muted, HorizontalAlignment.Center));
        layout.AddChild(new Control { CustomMinimumSize = new Vector2(0, 30) });
        _page.AddThemeConstantOverride("separation", 14);
        layout.AddChild(_page);

        var center = new CenterContainer();
        center.SetAnchorsPreset(LayoutPreset.FullRect);
        center.AddChild(layout);
        AddChild(center);

        var versionLabel = Widgets.Label($"v{version}", 13, Palette.Muted);
        versionLabel.GrowVertical = GrowDirection.Begin;
        AddChild(versionLabel);
        versionLabel.SetAnchorsAndOffsetsPreset(LayoutPreset.BottomLeft, LayoutPresetMode.KeepSize, 12);

        if (startPage == Page.Pvp)
        {
            ShowPvpPage();
        }
        else
        {
            ShowTitlePage();
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (_back is not null && @event.IsActionPressed("ui_cancel"))
        {
            GetViewport().SetInputAsHandled();
            _back();
        }
    }

    private void ShowTitlePage() => ShowPage(
        back: null,
        MenuButton("Play", ShowPlayPage),
        MenuButton("Decks", _onDecks),
        MenuButton("Options", OpenOptions),
        MenuButton("Quit", _onQuit));

    private void ShowPlayPage() => ShowPage(
        back: ShowTitlePage,
        Heading("Play"),
        MenuButton("PvP", ShowPvpPage),
        MenuButton("Computer", ShowDeckSelectPage),
        Gap(),
        MenuButton("Back", ShowTitlePage));

    private void ShowPvpPage()
    {
        var tiles = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        tiles.AddThemeConstantOverride("separation", 24);
        tiles.AddChild(new ModeTile(Icons.NetworkIcon, "Local network", "Host or join a game on the same network.", _onPlayLan));
        tiles.AddChild(new ModeTile(
            Icons.GlobeIcon,
            "Online",
            "Play over the internet: join a lobby or invite a friend.",
            () => MessageDialog.ComingSoon(this, "Online play"),
            ribbon: "COMING SOON"));

        ShowPage(
            back: ShowPlayPage,
            Heading("Player versus player"),
            tiles,
            Gap(),
            MenuButton("Back", ShowPlayPage));
    }

    private void ShowDeckSelectPage()
    {
        var controls = new List<Control> { Heading("Choose your deck") };
        var list = new VBoxContainer();
        list.AddThemeConstantOverride("separation", 10);
        foreach (var deck in _decks.Collection.Decks)
        {
            var editor = new DeckEditor(deck, _data);
            var button = Widgets.Button($"{deck.Name}   ({FactionInfo.Name(deck.Faction)}, leader {editor.Leader.Name})", () => _onPlayComputer(editor.Deck), 22);
            button.CustomMinimumSize = new Vector2(0, 52);
            UiTheme.TintButton(button, Palette.ForFaction(deck.Faction));
            if (!editor.IsPlayable)
            {
                button.Disabled = true;
                button.Text += "   - incomplete";
                button.TooltipText = $"Finish this deck in Decks first:\n{string.Join('\n', editor.Problems)}";
            }

            list.AddChild(button);
        }

        var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, CustomMinimumSize = new Vector2(0, Math.Min(5, _decks.Collection.Count) * 62) };
        list.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        scroll.AddChild(list);
        controls.Add(scroll);

        controls.Add(Gap());
        controls.Add(MenuButton("Back", ShowPlayPage));
        ShowPage(ShowPlayPage, [.. controls]);
    }

    private void ShowPage(Action? back, params Control[] controls)
    {
        _back = back;
        _page.ClearChildren();
        foreach (var control in controls)
        {
            _page.AddChild(control);
        }
    }

    private void OpenOptions()
    {
        OptionsMenu? options = null;
        options = new OptionsMenu(_settings, () => options!.QueueFree());
        AddChild(options);
    }

    private static Button MenuButton(string text, Action onPressed)
    {
        var button = Widgets.Button(text, onPressed, 23);
        button.CustomMinimumSize = new Vector2(ButtonWidth, 54);
        button.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
        return button;
    }

    private static Label Heading(string text) => Widgets.Label(text, 0, Palette.Text, HorizontalAlignment.Center).WithBoldFont(26);

    private static Control Gap() => new() { CustomMinimumSize = new Vector2(0, 12), MouseFilter = MouseFilterEnum.Ignore };
}
