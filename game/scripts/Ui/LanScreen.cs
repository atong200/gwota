using Godot;
using Gwota.Core.Cards;
using Gwota.Core.DeckBuilding;
using Gwota.Core.Settings;
using Gwota.Game.Infrastructure;
using Gwota.Net;
using Gwota.Net.Discovery;
using Gwota.Net.Lan;

namespace Gwota.Game.Ui;

/// <summary>Command-line overrides for the LAN screen, so two copies can run side by side on one computer.</summary>
public sealed record LanLaunchOptions(string? Username, int? Port);

/// <summary>
/// Local network play: pick a deck, then host a named game or join one heard on the network (or typed by address).
/// Polls the <see cref="LanLobby"/> every frame and hands over the match once both players are connected.
/// </summary>
public sealed partial class LanScreen : Control
{
    private const double SearchDuration = 1.5;
    private const double SearchPulseDuration = 0.7;
    private const int TextSize = 20;
    private const int FieldSize = 21;

    private readonly AppServices _services;
    private readonly LanLobby _lobby;
    private readonly LanLaunchOptions _options;
    private readonly Action<LanMatch> _onStart;
    private readonly Action _onBack;
    private readonly List<DeckEditor> _decks;
    private readonly OptionButton _deckPicker = new() { FocusMode = FocusModeEnum.None, CustomMinimumSize = new Vector2(0, 46) };
    private readonly LineEdit _roomName = new() { MaxLength = NetworkSettings.MaxRoomNameLength, CustomMinimumSize = new Vector2(0, 46) };
    private readonly LineEdit _port = new() { MaxLength = 5, CustomMinimumSize = new Vector2(120, 46) };
    private readonly LineEdit _address = new() { PlaceholderText = "e.g. 192.168.1.20 or 192.168.1.20:7777", CustomMinimumSize = new Vector2(0, 46) };
    private readonly VBoxContainer _games = new();
    private readonly Label _error = Widgets.Label(string.Empty, 19, Palette.Debuff, HorizontalAlignment.Center);
    private readonly Button _hostButton;
    private readonly Button _joinButton;
    private WaitingDialog? _waiting;
    private LanLobbyState? _waitingFor;
    private string? _joiningRoom;
    private string? _portError;
    private double _searchTimeLeft = SearchDuration;
    private bool _started;

    public LanScreen(AppServices services, LanLobby lobby, LanLaunchOptions options, Action<LanMatch> onStart, Action onBack)
    {
        _services = services ?? throw new ArgumentNullException(nameof(services));
        _lobby = lobby ?? throw new ArgumentNullException(nameof(lobby));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _onStart = onStart ?? throw new ArgumentNullException(nameof(onStart));
        _onBack = onBack ?? throw new ArgumentNullException(nameof(onBack));
        _decks = [.. services.Decks.Collection.Decks.Select(deck => new DeckEditor(deck, services.Data))];
        _hostButton = Widgets.Button("Host game", Host, 23);
        _joinButton = Widgets.Button("Join", JoinTypedAddress, FieldSize);

        SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(new ColorRect { Color = Palette.Background, AnchorRight = 1, AnchorBottom = 1, MouseFilter = MouseFilterEnum.Ignore });
        BuildLayout();
        RefreshGames();
        RefreshStatus();
    }

    private string PlayerName => _options.Username ?? _services.Settings.Current.Profile.Username;

    private bool IsSearching => _searchTimeLeft > 0;

    public override void _Process(double delta)
    {
        var changed = _lobby.Poll();
        if (IsSearching)
        {
            _searchTimeLeft -= delta;
            changed |= !IsSearching;
        }

        if (changed)
        {
            RefreshGames();
            RefreshStatus();
        }
    }

    public override void _ExitTree() => _lobby.Dispose();

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed("ui_cancel"))
        {
            GetViewport().SetInputAsHandled();
            _onBack();
        }
    }

    private void BuildLayout()
    {
        var layout = new VBoxContainer();
        layout.AddThemeConstantOverride("separation", 18);
        layout.AddChild(Widgets.Label("Local network", 0, Palette.Highlight, HorizontalAlignment.Center).WithBoldFont(46));
        layout.AddChild(Widgets.Label("Play against someone on the same network.", TextSize, Palette.Muted, HorizontalAlignment.Center));

        var left = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsStretchRatio = 1 };
        left.AddThemeConstantOverride("separation", 24);
        left.AddChild(BuildHostPanel());
        left.AddChild(BuildPlayerPanel());

        var columns = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        columns.AddThemeConstantOverride("separation", 24);
        columns.AddChild(left);
        columns.AddChild(BuildJoinPanel());
        layout.AddChild(columns);

        _error.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        layout.AddChild(_error);

        var back = Widgets.Button("Back", _onBack, 22);
        back.CustomMinimumSize = new Vector2(180, 50);
        back.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
        layout.AddChild(back);

        var margin = new MarginContainer();
        margin.SetAnchorsPreset(LayoutPreset.FullRect);
        foreach (var (side, size) in new[] { ("left", 60), ("right", 60), ("top", 30), ("bottom", 24) })
        {
            margin.AddThemeConstantOverride($"margin_{side}", size);
        }

        margin.AddChild(layout);
        AddChild(margin);
    }

    /// <summary>Who is playing and with which deck, shared by hosting and joining.</summary>
    private PanelContainer BuildPlayerPanel()
    {
        for (var i = 0; i < _decks.Count; i++)
        {
            var editor = _decks[i];
            var playable = editor.IsPlayable;
            _deckPicker.AddItem($"{editor.Name}  ({FactionInfo.Name(editor.Faction)}){(playable ? string.Empty : "  - incomplete")}", i);
            _deckPicker.SetItemDisabled(i, !playable);
            _deckPicker.SetItemTooltip(i, playable ? $"Leader: {editor.Leader.Name}" : string.Join('\n', editor.Problems));
        }

        _deckPicker.AddThemeFontSizeOverride("font_size", FieldSize);
        _deckPicker.ItemSelected += _ => OnDeckSelected();
        _deckPicker.Select(_decks.FindIndex(d => d.IsPlayable));
        OnDeckSelected();

        var name = Widgets.Label(PlayerName, 0, Palette.Text).WithBoldFont(23);
        name.VerticalAlignment = VerticalAlignment.Center;
        name.MouseFilter = MouseFilterEnum.Stop;
        name.TooltipText = _options.Username is null ? "Change it in Options > Profile." : "Set from the command line.";

        _deckPicker.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _deckPicker.ClipText = true;

        var form = new GridContainer { Columns = 2 };
        form.AddThemeConstantOverride("h_separation", 16);
        form.AddThemeConstantOverride("v_separation", 14);
        form.AddChild(FieldLabel("Playing as"));
        form.AddChild(name);
        form.AddChild(FieldLabel("Deck"));
        form.AddChild(_deckPicker);
        return SidePanel(form, 1);
    }

    private PanelContainer BuildHostPanel()
    {
        var network = _services.Settings.Current.Network;
        _roomName.Text = network.RoomName;
        _roomName.PlaceholderText = NetworkSettings.RoomNameOrDefault(null, PlayerName);
        _roomName.TooltipText = "Shown to the other players in their list of games.";
        _roomName.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _roomName.AddThemeFontSizeOverride("font_size", FieldSize);
        _roomName.TextSubmitted += _ => _roomName.ReleaseFocus();
        _roomName.FocusExited += CommitRoomName;

        _port.Text = (_options.Port ?? network.HostPort).ToString(System.Globalization.CultureInfo.InvariantCulture);
        _port.Editable = _options.Port is null;
        _port.TooltipText = _options.Port is null
            ? $"Network port the game listens on ({NetworkSettings.MinHostPort}-{NetworkSettings.MaxHostPort}). Players joining by address may need it."
            : "Set from the command line.";
        _port.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
        _port.AddThemeFontSizeOverride("font_size", FieldSize);
        _port.TextSubmitted += _ => _port.ReleaseFocus();
        _port.FocusExited += () => CommitPort();

        var form = new GridContainer { Columns = 2 };
        form.AddThemeConstantOverride("h_separation", 16);
        form.AddThemeConstantOverride("v_separation", 14);
        form.AddChild(FieldLabel("Room name"));
        form.AddChild(_roomName);
        form.AddChild(FieldLabel("Your address"));
        form.AddChild(AddressBlock());
        form.AddChild(FieldLabel("Port"));
        form.AddChild(_port);

        _hostButton.CustomMinimumSize = new Vector2(0, 58);

        var body = new VBoxContainer();
        body.AddThemeConstantOverride("separation", 18);
        body.AddChild(Heading("Host a game"));
        body.AddChild(Text("Open a game on this computer. Players on your network will see it in their list."));
        body.AddChild(form);
        body.AddChild(_hostButton);
        return SidePanel(body, 1);
    }

    private PanelContainer BuildJoinPanel()
    {
        _games.AddThemeConstantOverride("separation", 10);
        _games.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, SizeFlagsVertical = SizeFlags.ExpandFill };
        scroll.AddChild(_games);

        var header = new HBoxContainer();
        var heading = Heading("Join a game");
        heading.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        header.AddChild(heading);
        var refresh = Widgets.IconButton(Icons.RefreshIcon, "Search again", RefreshSearch, size: 44);
        refresh.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        header.AddChild(refresh);

        _address.AddThemeFontSizeOverride("font_size", FieldSize);
        _address.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _address.TextSubmitted += _ => JoinTypedAddress();
        _joinButton.CustomMinimumSize = new Vector2(130, 46);
        var byAddress = new HBoxContainer();
        byAddress.AddThemeConstantOverride("separation", 10);
        byAddress.AddChild(_address);
        byAddress.AddChild(_joinButton);

        var body = new VBoxContainer();
        body.AddThemeConstantOverride("separation", 14);
        body.AddChild(header);
        body.AddChild(Widgets.Label("Games on your network", TextSize, Palette.Muted));
        body.AddChild(scroll);
        body.AddChild(new HSeparator());
        body.AddChild(Widgets.Label("Join by IP address", TextSize, Palette.Muted));
        body.AddChild(byAddress);
        return SidePanel(body, 1.8f);
    }

    /// <summary>This computer's address, for an opponent who joins by address.</summary>
    private static VBoxContainer AddressBlock()
    {
        var addresses = LocalNetwork.Addresses();
        var block = new VBoxContainer();
        block.AddThemeConstantOverride("separation", 2);
        if (addresses.Count == 0)
        {
            block.AddChild(Text("No network connection found."));
            return block;
        }

        var main = Widgets.Label(addresses[0].ToString(), 0, Palette.Text).WithBoldFont(23);
        main.TooltipText = "Give this to the other player if your game does not show up in their list.";
        main.MouseFilter = MouseFilterEnum.Stop;
        block.AddChild(main);
        if (addresses.Count > 1)
        {
            var others = Widgets.Label($"Also {string.Join(",  ", addresses.Skip(1))}", 17, Palette.Muted);
            others.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            block.AddChild(others);
        }

        return block;
    }

    /// <summary>Forgets the games heard so far and listens again for a moment.</summary>
    private void RefreshSearch()
    {
        _lobby.Refresh();
        _searchTimeLeft = SearchDuration;
        RefreshGames();
        RefreshStatus();
    }

    private void RefreshGames()
    {
        _games.ClearChildren();
        var games = _lobby.Directory.Games;
        if (games.Count > 0)
        {
            foreach (var game in games)
            {
                _games.AddChild(GameRow(game));
            }

            return;
        }

        var listening = _lobby.IsScanning || _lobby.State != LanLobbyState.Browsing;
        var text = !listening ? "This computer cannot listen for games. Join by IP address below."
            : IsSearching ? "Searching..."
            : "No games found on your network.";
        var label = Widgets.Label(text, TextSize, Palette.Muted, HorizontalAlignment.Center);
        label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _games.AddChild(new Control { CustomMinimumSize = new Vector2(0, 30), MouseFilter = MouseFilterEnum.Ignore });
        _games.AddChild(label);
        if (listening && IsSearching)
        {
            var pulse = label.CreateTween().SetLoops().SetTrans(Tween.TransitionType.Sine);
            pulse.TweenProperty(label, "modulate:a", 0.35f, SearchPulseDuration);
            pulse.TweenProperty(label, "modulate:a", 1f, SearchPulseDuration);
        }
    }

    private PanelContainer GameRow(LanGame game)
    {
        var beacon = game.Beacon;
        var info = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore };
        info.AddThemeConstantOverride("separation", 2);
        info.AddChild(Widgets.Label(beacon.RoomName, 0, Palette.Text).WithBoldFont(23));
        info.AddChild(Widgets.Label($"Hosted by {beacon.HostName}  ·  {game.Endpoint}", 17, Palette.Muted));
        if (!game.IsCompatible)
        {
            info.AddChild(Widgets.Label($"Different game version ({beacon.GameVersion})", 17, Palette.Debuff));
        }

        var join = Widgets.Button("Join", () => Join(new LanAddress(game.Address.ToString(), beacon.Port), beacon.RoomName), FieldSize);
        join.CustomMinimumSize = new Vector2(120, 46);
        join.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        join.Disabled = !game.IsCompatible || SelectedDeck is null;
        if (!game.IsCompatible)
        {
            join.TooltipText = "Both players need the same version of the game.";
        }

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 12);
        row.AddChild(info);
        row.AddChild(join);
        return Widgets.Panel(row, Widgets.Box(Palette.Row, Palette.PanelBorder, 1, radius: 6, margin: 12));
    }

    /// <summary>Error line, buttons and the waiting dialog follow the lobby state; a started match is handed over.</summary>
    private void RefreshStatus()
    {
        var hasDeck = SelectedDeck is not null;
        _error.Text = _lobby.Error ?? _portError ?? (hasDeck ? string.Empty : "You need a complete deck to play. Finish one in Decks.");
        _error.Visible = _error.Text.Length > 0;
        _hostButton.Disabled = !hasDeck || _portError is not null;
        _joinButton.Disabled = !hasDeck;

        switch (_lobby.State)
        {
            case LanLobbyState.Hosting:
                ShowWaiting(LanLobbyState.Hosting, "Waiting for an opponent", HostDetails());
                _waiting!.SetNotice(_lobby.Notice);
                break;
            case LanLobbyState.Joining:
                var target = _joiningRoom is null ? $"{_lobby.JoinTarget}" : $"{_joiningRoom} ({_lobby.JoinTarget})";
                ShowWaiting(LanLobbyState.Joining, "Joining", $"Connecting to {target}...");
                break;
            case LanLobbyState.Started when !_started && _lobby.Match is { } match:
                _started = true;
                CloseWaiting();
                Callable.From(() => _onStart(match)).CallDeferred();
                break;
            default:
                CloseWaiting();
                break;
        }
    }

    private string HostDetails()
    {
        var port = _lobby.HostPort ?? NetProtocol.DefaultGamePort;
        var addresses = LocalNetwork.Addresses().Select(a => port == NetProtocol.DefaultGamePort ? a.ToString() : $"{a}:{port}").ToList();
        return addresses.Count == 0
            ? $"No network connection was found. Another game on this computer can still join at 127.0.0.1:{port}."
            : $"Players on your network will see your game, or can join at:\n{string.Join('\n', addresses)}";
    }

    private void ShowWaiting(LanLobbyState state, string title, string details)
    {
        if (_waitingFor == state)
        {
            return;
        }

        CloseWaiting();
        _waitingFor = state;
        _waiting = new WaitingDialog(title, details, CancelWaiting);
        AddChild(_waiting);
    }

    private void CloseWaiting()
    {
        _waiting?.QueueFree();
        _waiting = null;
        _waitingFor = null;
    }

    private void CancelWaiting()
    {
        _lobby.Cancel();
        RefreshGames();
        RefreshStatus();
    }

    private void Host()
    {
        CommitRoomName();
        if (CommitPort() is not { } port || Player() is not { } player || _lobby.State != LanLobbyState.Browsing)
        {
            return;
        }

        _lobby.Host(player, port, _services.Settings.Current.Network.RoomName);
        RefreshGames();
        RefreshStatus();
    }

    private void JoinTypedAddress()
    {
        if (LanAddress.Parse(_address.Text) is not { } address)
        {
            _error.Text = "Enter an address like 192.168.1.20 or 192.168.1.20:7777.";
            _error.Visible = true;
            return;
        }

        _address.ReleaseFocus();
        Join(address, roomName: null);
    }

    private void Join(LanAddress address, string? roomName)
    {
        if (Player() is not { } player || _lobby.State != LanLobbyState.Browsing)
        {
            return;
        }

        _joiningRoom = roomName;
        _lobby.Join(address, player);
        RefreshStatus();
    }

    private LanPlayer? Player() => SelectedDeck is { } deck ? new LanPlayer(PlayerName, deck.Deck) : null;

    private DeckEditor? SelectedDeck =>
        _deckPicker.Selected is var index && index >= 0 && index < _decks.Count && _decks[index].IsPlayable ? _decks[index] : null;

    private void OnDeckSelected()
    {
        var faction = SelectedDeck?.Faction ?? Faction.Neutral;
        UiTheme.TintButton(_deckPicker, Palette.ForFaction(faction));
        RefreshGames();
        RefreshStatus();
    }

    private void CommitRoomName()
    {
        _services.Settings.Update(s => s with { Network = s.Network with { RoomName = _roomName.Text } });
        _services.Settings.Flush();
        _roomName.Text = _services.Settings.Current.Network.RoomName;
    }

    /// <summary>Validates the typed port and saves it (unless it comes from the command line); null when invalid.</summary>
    private int? CommitPort()
    {
        if (_options.Port is { } fixedPort)
        {
            return fixedPort;
        }

        if (!int.TryParse(_port.Text.Trim(), out var port) || !NetworkSettings.IsValidPort(port))
        {
            _portError = $"The port must be a number between {NetworkSettings.MinHostPort} and {NetworkSettings.MaxHostPort}.";
            RefreshStatus();
            return null;
        }

        _portError = null;
        _services.Settings.Update(s => s with { Network = s.Network with { HostPort = port } });
        _services.Settings.Flush();
        RefreshStatus();
        return port;
    }

    private static Label FieldLabel(string text)
    {
        var label = Widgets.Label(text, TextSize, Palette.Muted);
        label.VerticalAlignment = VerticalAlignment.Center;
        return label;
    }

    private static Label Heading(string text) => Widgets.Label(text, 0, Palette.Highlight).WithBoldFont(30);

    private static Label Text(string text)
    {
        var label = Widgets.Label(text, TextSize, Palette.Text);
        label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        return label;
    }

    private static PanelContainer SidePanel(Control body, float ratio)
    {
        var panel = Widgets.Panel(body, Widgets.Box(Palette.Panel, Palette.PanelBorder, 1, radius: 10, margin: 24));
        panel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        panel.SizeFlagsStretchRatio = ratio;
        return panel;
    }
}
