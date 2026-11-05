using Godot;
using Gwota.Core.Common;
using Gwota.Core.Match;
using Gwota.Core.Presentation;
using Gwota.Core.Session;
using Gwota.Game.Infrastructure;
using Timer = Godot.Timer;

namespace Gwota.Game.Ui;

/// <summary>
/// The match: lays out the side panel, board and log, renders them from the session state, and runs the pause menu
/// and the round banner. Commands go through <see cref="MatchDriver"/>, card moves through <see cref="MoveAnimator"/>.
/// </summary>
public sealed partial class MatchScreen : Control, IMatchDriverHost, IMoveAnimationHost
{
    private const double BannerDuration = 2.2;

    private readonly MatchSession _session;
    private readonly IMatchLink? _link;
    private readonly Seat _local;
    private readonly SettingsStore _settings;
    private readonly INavigator _navigator;
    private readonly Action _rematch;
    private readonly MatchSidePanel _side;
    private readonly MatchBoardView _board;
    private readonly MatchLogPanel _log;
    private readonly Overlay _overlay = new();
    private readonly CardFlights _flights = new();
    private readonly Label _banner = Widgets.Label(string.Empty, 0, Palette.Highlight, HorizontalAlignment.Center).WithDisplayFont(32);
    private readonly Timer _bannerTimer = new() { OneShot = true, WaitTime = BannerDuration };
    private readonly SelectionController _selection;
    private readonly MatchDriver _driver;
    private readonly MoveAnimator _animator;
    private readonly MatchOverlayFlow _overlayFlow;
    private IReadOnlyList<GameCommand> _legal = [];
    private PauseMenu? _pauseMenu;

    /// <summary>
    /// A match on this computer: seat one is played here, controllers play the other seats. <paramref name="rematch"/>
    /// starts the next match from the end screen.
    /// </summary>
    public MatchScreen(MatchSession session, MatchSetup setup, string seedInfo, SettingsStore settings, INavigator navigator, Action rematch)
        : this(session ?? throw new ArgumentNullException(nameof(session)), setup, null, MatchFactory.HumanSeat, seedInfo, settings, navigator, rematch)
    {
    }

    /// <summary>
    /// A match against a remote player: commands go through <paramref name="link"/>. When the remote match starts a
    /// rematch, <paramref name="rematch"/> is called so a new screen can be built for the new session.
    /// </summary>
    public MatchScreen(IMatchLink link, string seedInfo, SettingsStore settings, INavigator navigator, Action rematch)
        : this((link ?? throw new ArgumentNullException(nameof(link))).Session, link.Setup, link, link.LocalSeat, seedInfo, settings, navigator, rematch)
    {
    }

    private MatchScreen(MatchSession session, MatchSetup setup, IMatchLink? link, Seat local, string seedInfo, SettingsStore settings, INavigator navigator, Action rematch)
    {
        ArgumentNullException.ThrowIfNull(setup);
        _session = session;
        _link = link;
        _local = local;
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _navigator = navigator ?? throw new ArgumentNullException(nameof(navigator));
        _rematch = rematch ?? throw new ArgumentNullException(nameof(rematch));

        var opponentCardBack = setup.CardBackFor(local.Opponent());
        _side = new MatchSidePanel(setup.CardBackFor(local), opponentCardBack);
        _board = new MatchBoardView(local, opponentCardBack);
        _log = new MatchLogPanel(settings);
        _flights.CardBackOf = setup.CardBackFor;

        _selection = new SelectionController(session, local);
        _driver = new MatchDriver(session, link, local, this);
        _animator = new MoveAnimator(_flights, _overlay, _board, _side, local, this);
        _overlayFlow = new MatchOverlayFlow(_overlay, session, link, local, navigator, rematch, _driver);
        Wire();

        SetAnchorsPreset(LayoutPreset.FullRect);
        BuildLayout();
        foreach (var line in session.History.Select(Describe).OfType<string>().Prepend(seedInfo))
        {
            _log.Append(line);
        }
    }

    public bool IsAnimating => _animator.IsRunning;

    private float DurationScale => (float)_settings.Current.Gameplay.DurationScale;

    public override void _Ready()
    {
        AddChild(_bannerTimer);
        _bannerTimer.Timeout += () => _banner.Visible = false;
        _session.EventsApplied += OnEventsApplied;
        Refresh();
    }

    public override void _ExitTree() => _session.EventsApplied -= OnEventsApplied;

    public override void _UnhandledInput(InputEvent @event)
    {
        if (_animator.IsRunning)
        {
            if (@event.IsActionPressed("ui_cancel"))
            {
                OpenPauseMenu();
                GetViewport().SetInputAsHandled();
            }

            return;
        }

        if (@event.IsActionPressed("ui_cancel"))
        {
            // An open pause menu handles Escape itself (it receives input first), so here it cancels or opens.
            if (!_selection.Clear())
            {
                OpenPauseMenu();
            }

            GetViewport().SetInputAsHandled();
            return;
        }

        if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Right } && _selection.Clear())
        {
            GetViewport().SetInputAsHandled();
        }
    }

    public void Refresh()
    {
        if (_animator.IsRunning)
        {
            return;
        }

        Render(animating: false);
        _driver.ScheduleController();
    }

    /// <summary>Applies a command during play, then animates the cards it moved.</summary>
    public void Execute(Func<CommandResult?> command)
    {
        ArgumentNullException.ThrowIfNull(command);

        var animate = _session.State.Phase == MatchPhase.Playing && DurationScale > 0 && IsInsideTree();
        var before = animate ? CardMoves.Snapshot(_session.State) : null;
        var origins = animate ? _board.Capture() : null;

        var result = command();

        var plan = before is not null && result is { IsSuccess: true }
            ? CardMoves.Plan(before, CardMoves.Snapshot(_session.State), result.Events)
            : MovePlan.Empty;
        if (plan.IsEmpty || origins is null)
        {
            Refresh();
            return;
        }

        _animator.Play(plan, origins, DurationScale);
    }

    /// <summary>Applies a draft command, then plays the redraw animation (if the player here redrew) before re-rendering.</summary>
    public void ExecuteDraft(Func<CommandResult?> command)
    {
        ArgumentNullException.ThrowIfNull(command);

        var drawPile = _side.PileRect(own: true, PileKind.Draw);
        var result = command();
        var finished = result is { IsSuccess: true }
            ? result.Events.OfType<DraftFinished>().FirstOrDefault(e => e.Seat == _local)
            : null;
        if (finished is null || !_animator.PlayRedraw(finished, _session.State, drawPile, DurationScale))
        {
            Refresh();
        }
    }

    public void ReportError(string message) => _log.AppendError(message);

    public void StartRematch() => _rematch();

    public void RenderMoving(int pendingOpponentDraws) => Render(animating: true, pendingOpponentDraws);

    public void RenderLanded() => RenderNumbers();

    public void AnimationFinished() => Refresh();

    private void Wire()
    {
        _side.LeaderClicked += () =>
        {
            if (!_animator.IsRunning)
            {
                _selection.ClickLeader();
            }
        };
        _side.DiscardClicked += own =>
        {
            if (!own || !_selection.ClickDiscard())
            {
                DiscardPileDialog.Show(this, MatchTexts.DiscardPile(_session.State, own ? _local : _local.Opponent(), _local));
            }
        };
        _side.PassPressed += () => _driver.Submit(new PassCommand(_local));
        _side.MenuPressed += OpenPauseMenu;

        _board.RowClicked += _selection.ClickRow;
        _board.CardClicked += _selection.ClickCard;
        _board.FieldClicked += _selection.ClickField;
        _board.HandCardClicked += _selection.ClickHandCard;

        _selection.Changed += () =>
        {
            RenderSelection();
            _board.ApplyHighlights(_selection.Targets, _selection.CardId);
        };
        _selection.Play += _driver.Submit;
        _selection.Rejected += _side.ShowHint;

        _driver.Sending += () =>
        {
            _selection.Reset();
            _overlayFlow.ResetDraft();
        };
    }

    private void BuildLayout()
    {
        AddChild(new ColorRect { Color = Palette.Background, AnchorRight = 1, AnchorBottom = 1, MouseFilter = MouseFilterEnum.Ignore });

        var root = new HBoxContainer();
        root.SetAnchorsPreset(LayoutPreset.FullRect);
        root.WithGap(8);
        root.AddChild(_side);
        root.AddChild(_board);
        root.AddChild(_log);

        AddChild(Widgets.Margins(root, 8));

        AddChild(_flights);

        _banner.Visible = false;
        _banner.AddThemeColorOverride("font_outline_color", Colors.Black);
        _banner.AddThemeConstantOverride("outline_size", 8);
        var bannerCenter = new CenterContainer { MouseFilter = MouseFilterEnum.Ignore };
        bannerCenter.SetAnchorsPreset(LayoutPreset.FullRect);
        bannerCenter.AddChild(_banner);
        AddChild(bannerCenter);

        AddChild(_overlay);
        AddChild(_driver);
    }

    /// <summary>
    /// Renders the current state. While <paramref name="animating"/>, nothing is clickable, the moving cards stay
    /// invisible in their slots and scores, gems and piles keep their values until <see cref="RenderNumbers"/>.
    /// </summary>
    private void Render(bool animating, int pendingOpponentDraws = 0)
    {
        var state = _session.State;
        var canAct = !animating && _driver.LocalCanAct && state.Phase == MatchPhase.Playing && state.PendingChoice is null;
        _legal = canAct ? _session.GetLegalCommands(_local) : [];

        _selection.Refresh(_legal, canAct);
        RenderSelection();

        if (animating)
        {
            var opponent = state.Player(_local.Opponent());
            _board.ShowOpponentHand(opponent.Name, Math.Max(0, opponent.Hand.Count - pendingOpponentDraws));
            _overlay.Close();
        }
        else
        {
            RenderNumbers();
        }

        _board.Render(_session.Engine, _local, _selection.Targets, _selection.CardId, canAct, DurationScale);

        if (!animating)
        {
            _overlayFlow.Show(_driver.LocalCanAct);
        }
    }

    /// <summary>Pass, leader and discard states, and the status and hint for the current selection.</summary>
    private void RenderSelection()
    {
        _side.SetActions(
            canPass: _legal.Contains(new PassCommand(_local)),
            canUseLeader: _legal.OfType<UseLeaderCommand>().Any(),
            leaderSelected: _selection.IsLeader,
            discardTargeted: _selection.Targets.AffectsOwnDiscard);
        _side.ShowStatus(MatchTexts.Status(_session, _local, _selection.Selection));
    }

    /// <summary>Scores, gems, pass state, leader and piles: what changes once the moving cards have landed.</summary>
    private void RenderNumbers()
    {
        var engine = _session.Engine;
        var opponent = _session.State.Player(_local.Opponent());
        _side.ShowPlayers(engine, _local, DurationScale);
        _side.ShowPiles(_session.State, _local);
        _board.ShowScores(engine);
        _board.ShowOpponentHand(opponent.Name, opponent.Hand.Count);
    }

    private void OpenPauseMenu()
    {
        if (_pauseMenu is not null)
        {
            return;
        }

        // A remote match keeps running behind the menu.
        if (_link is null)
        {
            SetPaused(true);
        }

        Action? forfeit = _session.IsControlled(_local) || _session.IsFinished || _link?.CloseReason is not null ? null : Forfeit;
        _pauseMenu = new PauseMenu(_settings, forfeit, () => _navigator.ShowMainMenu(), _navigator.Quit);
        _pauseMenu.Closed += OnPauseMenuClosed;
        AddChild(_pauseMenu);
    }

    private void ClosePauseMenu() => _pauseMenu?.Close();

    private void OnPauseMenuClosed()
    {
        _pauseMenu = null;
        SetPaused(false);
    }

    private void SetPaused(bool paused)
    {
        _driver.SetPaused(paused);
        _bannerTimer.Paused = paused;
        _flights.SetPaused(paused);
    }

    /// <summary>Concedes the match: any running animation is dropped and the defeat screen shows right away.</summary>
    private void Forfeit()
    {
        ClosePauseMenu();
        var forfeit = new ForfeitCommand(_local);
        if (_link is not null)
        {
            _driver.SubmitNow(forfeit);
            return;
        }

        _driver.StopController();
        _animator.Cancel();
        _driver.SubmitNow(forfeit);
        Refresh();
    }

    private void OnEventsApplied(object? sender, IReadOnlyList<GameEvent> events)
    {
        foreach (var gameEvent in events)
        {
            if (Describe(gameEvent) is { } line)
            {
                _log.Append(line);
            }

            if (gameEvent is RoundEnded { Result: var result } && !_session.IsFinished)
            {
                ShowBanner(MatchTexts.RoundBanner(_session.State, result));
                _driver.DelayNextController();
            }
        }
    }

    private string? Describe(GameEvent gameEvent) => EventDescriber.Describe(gameEvent, _session.State, _local);

    private void ShowBanner(string text)
    {
        _banner.Text = text.ToUpperInvariant();
        _banner.Visible = true;
        _bannerTimer.Start();
    }
}
