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
/// Composition root: loads data and switches screens. Dev modes: <c>-- --smoke</c> (see <see cref="SmokeTest"/>)
/// and <c>-- --demo</c> (AI-vs-AI match on the real board). <c>-- --name X --port N</c> override the LAN name and
/// port, to run two copies on one computer.
/// </summary>
public partial class Main : Control, INavigator
{
    private const string DataRoot = "res://data";
    private const string SmokeArgument = "--smoke";
    private const string DemoArgument = "--demo";
    private const string NameArgument = "--name";
    private const string PortArgument = "--port";

    private SettingsStore? _settings;

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
            GetTree().Quit(SmokeTest.Run(load, Version));
            return;
        }

        Theme = UiTheme.Create();
        _settings = SettingsStore.Load();
        AddChild(new CardPreview());

        if (!load.IsValid)
        {
            ShowScreen(new DataErrorScreen(load.Errors, () => GetTree().Quit(1)));
            return;
        }

        _services = new AppServices(load.Data, _settings, DeckStore.Load(load.Data), Version);

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

    private AppServices Services => _services ?? throw new InvalidOperationException("Game data is not loaded.");

    public void ShowMainMenu(MainMenu.Page page = MainMenu.Page.Title)
    {
        LeaveLink();
        ShowScreen(new MainMenu(Services, this, page));
    }

    public void ShowDecks() => ShowScreen(new CollectionScreen(Services, this));

    public void ShowLan()
    {
        _lanIdentity ??= LanIdentity.For(Services.Data, Version);
        var lobby = new LanLobby(Services.Data, _lanIdentity, new TcpLanNetwork());
        ShowScreen(new LanScreen(Services, this, lobby, _lanOptions));
    }

    public void Quit()
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

    public void StartMatch(DeckDefinition playerDeck)
    {
        var random = new SeededRandom((ulong)System.Random.Shared.NextInt64());
        var username = Services.Settings.Current.Profile.Username;
        StartMatch(MatchFactory.VersusAiSetup(Services.Data, playerDeck, username, random, (ulong)System.Random.Shared.NextInt64()));
    }

    public void StartMatch(LanMatch link)
    {
        ArgumentNullException.ThrowIfNull(link);
        _link = link;
        var setup = link.Setup;
        var info = LogFormat.Muted($"LAN - {setup.PlayerName} ({setup.PlayerDeck.Name}) vs {setup.OpponentName} ({setup.OpponentDeck.Name})");
        ShowScreen(new MatchScreen(link, info, Services.Settings, this, () => StartMatch(link)));
    }

    private void StartMatch(MatchSetup setup)
    {
        var session = MatchFactory.VersusAi(Services.Data, setup);
        var info = LogFormat.Muted($"{setup.PlayerDeck.Name} vs {setup.OpponentDeck.Name} - seed {setup.Seed}");
        ShowScreen(new MatchScreen(session, setup, info, Services.Settings, this, () => StartMatch(setup with { Seed = setup.Seed + 1 })));
    }

    /// <summary>Spectate an AI-vs-AI match in the regular board UI (handy for UI checks and balance tuning).</summary>
    private void StartDemo()
    {
        var decks = MatchFactory.PlayableDecks(Services.Data);
        var random = new SeededRandom((ulong)System.Random.Shared.NextInt64());
        var setup = MatchFactory.AiVersusAiSetup(decks[0], decks[^1], random, (ulong)System.Random.Shared.NextInt64());
        var session = MatchFactory.AiVersusAi(Services.Data, setup);
        ShowScreen(new MatchScreen(session, setup, LogFormat.Muted($"Demo - seed {setup.Seed}"), Services.Settings, this, StartDemo));
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
}
