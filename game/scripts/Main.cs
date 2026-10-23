using Godot;
using Gwota.Core.Cards;
using Gwota.Core.Common;
using Gwota.Core.Data;
using Gwota.Core.Session;
using Gwota.Core.Settings;
using Gwota.Game.Infrastructure;
using Gwota.Game.Ui;
using Gwota.Net;
using Gwota.Net.Lan;

namespace Gwota.Game;

/// <summary>
/// Composition root: loads data and switches screens. Dev modes: <c>-- --smoke</c> (headless load-and-play check
/// used by CI) and <c>-- --demo</c> (AI-vs-AI match on the real board). <c>-- --name X --port N</c> override the
/// LAN name and port, to run two copies on one computer.
/// </summary>
public partial class Main : Control
{
    private const string DataRoot = "res://data";
    private const string SmokeArgument = "--smoke";
    private const string DemoArgument = "--demo";
    private const string NameArgument = "--name";
    private const string PortArgument = "--port";
    private const int SmokeSeedsPerPairing = 3;

    private GameData? _data;
    private SettingsStore? _settings;
    private DeckStore? _decks;
    private Control? _screen;
    private AppServices? _services;
    private LanIdentity? _lanIdentity;
    private LanLaunchOptions _lanOptions = new(null, null);
    private LanMatch? _link;

    public override void _Ready()
    {
        var load = LoadData();
        var args = OS.GetCmdlineUserArgs();

        if (args.Contains(SmokeArgument))
        {
            GetTree().Quit(RunSmoke(load));
            return;
        }

        Theme = UiTheme.Create();
        _settings = SettingsStore.Load();
        AddChild(new CardPreview());

        if (!load.IsValid)
        {
            ShowScreen(DataErrorScreen(load.Errors));
            return;
        }

        _data = load.Data;
        _decks = DeckStore.Load(_data);
        _services = new AppServices(_data, _settings, _decks, Version);
        _lanOptions = ParseLanOptions(args);
        if (args.Contains(DemoArgument))
        {
            StartDemo();
            return;
        }

        ShowMainMenu();
    }

    public override void _Notification(int what)
    {
        if (what == NotificationWMCloseRequest)
        {
            LeaveLink();
            _settings?.Flush();
        }
    }

    public override void _ExitTree() => CardArt.ClearCache();

    private static GameDataLoadResult LoadData()
    {
        try
        {
            return GameDataLoader.TryLoad(new GodotDataSource(DataRoot));
        }
        catch (IOException ex)
        {
            return new GameDataLoadResult(new GameData(new Dictionary<string, CardDefinition>(), new Dictionary<string, LeaderDefinition>(), new Dictionary<string, DeckDefinition>()), [ex.Message]);
        }
    }

    private static string Version => ProjectSettings.GetSetting("application/config/version").AsString();

    private void ShowMainMenu() => ShowMainMenu(MainMenu.Page.Title);

    private void ShowMainMenu(MainMenu.Page page)
    {
        LeaveLink();
        ShowScreen(new MainMenu(_services!, StartMatch, ShowLan, ShowDecks, Quit, page));
    }

    private void ShowDecks() =>
        ShowScreen(new CollectionScreen(_data!, _decks!, _settings!, ShowMainMenu));

    private void ShowLan()
    {
        _lanIdentity ??= LanIdentity.For(_data!, Version);
        var lobby = new LanLobby(_data!, _lanIdentity, new TcpLanNetwork());
        ShowScreen(new LanScreen(_services!, lobby, _lanOptions, StartMatch, () => ShowMainMenu(MainMenu.Page.Pvp)));
    }

    private void Quit()
    {
        LeaveLink();
        _settings?.Flush();
        GetTree().Quit();
    }

    /// <summary>Tells a remote opponent this game is leaving (they win if the match was running).</summary>
    private void LeaveLink()
    {
        _link?.Dispose();
        _link = null;
    }

    private static LanLaunchOptions ParseLanOptions(string[] args)
    {
        string? ValueOf(string name) => Array.IndexOf(args, name) is var i && i >= 0 && i + 1 < args.Length ? args[i + 1] : null;

        var name = ValueOf(NameArgument) is { } raw ? ProfileSettings.NormalizeUsername(raw) : null;
        int? port = int.TryParse(ValueOf(PortArgument), out var parsed) && NetworkSettings.IsValidPort(parsed) ? parsed : null;
        return new LanLaunchOptions(name, port);
    }

    private void StartMatch(DeckDefinition playerDeck)
    {
        var random = new SeededRandom((ulong)System.Random.Shared.NextInt64());
        var username = _settings!.Current.Profile.Username;
        StartMatch(MatchFactory.VersusAiSetup(_data!, playerDeck, username, random, (ulong)System.Random.Shared.NextInt64()));
    }

    private void StartMatch(MatchSetup setup)
    {
        var session = MatchFactory.VersusAi(_data!, setup);
        var info = $"[color=#9aa0a6]{setup.PlayerDeck.Name} vs {setup.OpponentDeck.Name} - seed {setup.Seed}[/color]";
        ShowScreen(new MatchScreen(session, info, _settings!, new MatchScreenActions(ShowMainMenu, () => StartMatch(setup with { Seed = setup.Seed + 1 }), Quit)));
    }

    private void StartMatch(LanMatch link)
    {
        _link = link;
        var setup = link.Setup;
        var info = $"[color=#9aa0a6]LAN - {setup.PlayerName} ({setup.PlayerDeck.Name}) vs {setup.OpponentName} ({setup.OpponentDeck.Name})[/color]";
        ShowScreen(new MatchScreen(link, info, _settings!, new MatchScreenActions(ShowMainMenu, () => StartMatch(link), Quit)));
    }

    /// <summary>Spectate an AI-vs-AI match in the regular board UI (handy for UI checks and balance tuning).</summary>
    private void StartDemo()
    {
        var decks = MatchFactory.PlayableDecks(_data!);
        var random = new SeededRandom((ulong)System.Random.Shared.NextInt64());
        var setup = MatchFactory.AiVersusAiSetup(decks[0], decks[^1], random, (ulong)System.Random.Shared.NextInt64());
        var session = MatchFactory.AiVersusAi(_data!, setup);
        ShowScreen(new MatchScreen(session, $"[color=#9aa0a6]Demo - seed {setup.Seed}[/color]", _settings!, new MatchScreenActions(ShowMainMenu, StartDemo, Quit)));
    }

    private void ShowScreen(Control screen)
    {
        if (_screen is not null)
        {
            RemoveChild(_screen);
            _screen.QueueFree();
        }

        _screen = screen;
        AddChild(screen);
    }

    private Control DataErrorScreen(IReadOnlyList<string> errors)
    {
        var text = new RichTextLabel { SizeFlagsVertical = SizeFlags.ExpandFill, SelectionEnabled = true };
        text.AddThemeColorOverride("default_color", Palette.Text);
        text.Text = string.Join("\n", errors);

        var layout = new VBoxContainer();
        layout.SetAnchorsPreset(LayoutPreset.FullRect);
        layout.AddChild(Widgets.Label($"Game data is invalid ({errors.Count} error(s)) - fix the files under game/data:", 24, Palette.Debuff));
        layout.AddChild(text);
        layout.AddChild(Widgets.Button("Quit", () => GetTree().Quit(1)));

        var screen = new Control();
        screen.SetAnchorsPreset(LayoutPreset.FullRect);
        screen.AddChild(new ColorRect { Color = Palette.Background, AnchorRight = 1, AnchorBottom = 1 });
        var margin = new MarginContainer();
        margin.SetAnchorsPreset(LayoutPreset.FullRect);
        margin.AddThemeConstantOverride("margin_left", 24);
        margin.AddThemeConstantOverride("margin_top", 24);
        margin.AddChild(layout);
        screen.AddChild(margin);
        return screen;
    }

    /// <summary>Plays AI-vs-AI matches for every pairing of decks. Returns the process exit code.</summary>
    private static int RunSmoke(GameDataLoadResult load)
    {
        if (!load.IsValid)
        {
            foreach (var error in load.Errors)
            {
                GD.PrintErr(error);
            }

            return 1;
        }

        try
        {
            var decks = MatchFactory.PlayableDecks(load.Data);
            var matches = 0;
            foreach (var one in decks)
            {
                foreach (var two in decks)
                {
                    for (ulong seed = 1; seed <= SmokeSeedsPerPairing; seed++)
                    {
                        var session = MatchFactory.AiVersusAi(load.Data, new MatchSetup(one, two, seed));
                        session.RunControllers();
                        if (!session.IsFinished)
                        {
                            GD.PrintErr($"Smoke: {one.Id} vs {two.Id} (seed {seed}) did not finish.");
                            return 1;
                        }

                        matches++;
                    }
                }
            }

            GD.Print($"Smoke OK: {load.Data.Cards.Count} cards, {load.Data.Leaders.Count} leaders, {decks.Count} decks, {matches} AI matches (v{Version}).");
            return 0;
        }
        catch (InvalidOperationException ex)
        {
            GD.PrintErr($"Smoke failed: {ex}");
            return 1;
        }
    }
}
