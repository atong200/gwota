using Godot;
using Gwota.Core.Cards;
using Gwota.Core.Common;
using Gwota.Core.Match;
using Gwota.Core.Presentation;
using Gwota.Core.Session;
using Gwota.Game.Infrastructure;
using Timer = Godot.Timer;

namespace Gwota.Game.Ui;

/// <summary>
/// The match board: renders from the session state, turns clicks into commands, paces the AI with a timer
/// and animates card moves.
/// </summary>
public sealed partial class MatchScreen : Control
{
    private const double AiDelay = 0.7;
    private const double AiDelayAfterRound = 2.2;
    private const double BannerDuration = 2.2;
    private const double CopiedFeedbackDuration = 1.5;
    private const double RevealHold = 0.7;
    private const double RoundEndHold = 1.1;
    private const float RevealScale = 1.5f;
    private const string CopyLogTooltip = "Copy the match log to the clipboard";

    private static readonly RowType[] RowsFromCenter = [RowType.Melee, RowType.Ranged, RowType.Siege];
    private static readonly Vector2 OwnPileCardSize = new(70, 98);
    private static readonly Vector2 OpponentPileCardSize = new(56, 78);

    private readonly MatchSession _session;
    private readonly IMatchLink? _link;
    private readonly Seat _local;
    private readonly Seat _opponent;
    private readonly SettingsStore _settings;
    private readonly MatchScreenActions _actions;
    private readonly Dictionary<(Seat, RowType), RowView> _rows = [];
    private readonly PlayerPanel _humanPanel = new();
    private readonly PlayerPanel _aiPanel = new();
    private readonly PileView _humanDraw = new(PileKind.Draw, OwnPileCardSize);
    private readonly PileView _humanDiscard = new(PileKind.Discard, OwnPileCardSize);
    private readonly PileView _aiDraw = new(PileKind.Draw, OpponentPileCardSize);
    private readonly PileView _aiDiscard = new(PileKind.Discard, OpponentPileCardSize);
    private readonly OpponentHandView _aiHand = new();
    private readonly HBoxContainer _hand = new() { Alignment = BoxContainer.AlignmentMode.Center };
    private readonly HBoxContainer _weather = new() { Alignment = BoxContainer.AlignmentMode.Center, MouseFilter = MouseFilterEnum.Ignore };
    private readonly StyleBoxFlat _weatherStyle = Widgets.Box(new Color(0, 0, 0, 0.15f), new Color(0, 0, 0, 0), 2, radius: 4, margin: 0);
    private readonly RichTextLabel _log = new() { BbcodeEnabled = true, ScrollFollowing = true, SizeFlagsVertical = SizeFlags.ExpandFill };
    private readonly Label _status = Widgets.Label(string.Empty, 0, Palette.Highlight, HorizontalAlignment.Center).WithDisplayFont(20);
    private readonly Label _hint = Widgets.Label(string.Empty, 17, Palette.Text, HorizontalAlignment.Center);
    private readonly Label _banner = Widgets.Label(string.Empty, 0, Palette.Highlight, HorizontalAlignment.Center).WithDisplayFont(32);
    private readonly Button _passButton;
    private readonly Overlay _overlay = new();
    private readonly CardFlights _flights = new();
    private readonly Dictionary<int, CardView> _views = [];
    private readonly HashSet<int> _hiddenIds = [];
    private readonly List<GameEvent> _commandEvents = [];
    private readonly VBoxContainer _field = new() { SizeFlagsVertical = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Stop };
    private readonly Timer _aiTimer = new() { OneShot = true };
    private readonly Timer _bannerTimer = new() { OneShot = true, WaitTime = BannerDuration };
    private readonly VBoxContainer _logBody = new() { CustomMinimumSize = new Vector2(300, 0) };
    private readonly Button _logToggle;
    private readonly Button _copyLog;

    private int? _selectedCardId;
    private PlayTargets _targets = PlayTargets.None;
    private DraftSelection? _draft;
    private DraftFinished? _humanDraftResult;
    private PauseMenu? _pauseMenu;
    private bool _animating;
    private bool _rematchStarted;
    private double _nextAiDelay = AiDelay;
    private DraftSelection? _shownDraft;
    private (bool CanSend, bool HasPending, string? CloseReason, RematchStatus Rematch) _linkState;

    /// <summary>A match on this computer: seat one is played here, controllers play the other seats.</summary>
    public MatchScreen(MatchSession session, string seedInfo, SettingsStore settings, MatchScreenActions actions)
        : this(session ?? throw new ArgumentNullException(nameof(session)), null, MatchFactory.HumanSeat, seedInfo, settings, actions)
    {
    }

    /// <summary>
    /// A match against a remote player: commands go through <paramref name="link"/>. When a rematch starts,
    /// <see cref="MatchScreenActions.Rematch"/> is called so a new screen can be built for the new session.
    /// </summary>
    public MatchScreen(IMatchLink link, string seedInfo, SettingsStore settings, MatchScreenActions actions)
        : this((link ?? throw new ArgumentNullException(nameof(link))).Session, link, link.LocalSeat, seedInfo, settings, actions)
    {
    }

    private MatchScreen(MatchSession session, IMatchLink? link, Seat local, string seedInfo, SettingsStore settings, MatchScreenActions actions)
    {
        _session = session;
        _link = link;
        _local = local;
        _opponent = local.Opponent();
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _actions = actions ?? throw new ArgumentNullException(nameof(actions));
        _logToggle = Widgets.Button(string.Empty, ToggleLog, 18);
        _copyLog = new Button { Icon = Icons.CopyIcon, TooltipText = CopyLogTooltip, FocusMode = FocusModeEnum.None };
        _copyLog.Pressed += CopyLog;
        _passButton = Widgets.Button("Pass round", () => Submit(new PassCommand(_local))).AsDisplay(22);
        _passButton.TooltipText = "You won't play again until the next round.";

        SetAnchorsPreset(LayoutPreset.FullRect);
        BuildLayout();
        foreach (var line in session.History.Select(Describe).OfType<string>().Prepend(seedInfo))
        {
            AppendLog(line);
        }
    }

    public override void _Ready()
    {
        AddChild(_aiTimer);
        AddChild(_bannerTimer);
        _aiTimer.Timeout += OnAiTimer;
        _bannerTimer.Timeout += () => _banner.Visible = false;
        _session.EventsApplied += OnEventsApplied;
        Refresh();
    }

    public override void _ExitTree() => _session.EventsApplied -= OnEventsApplied;

    /// <summary>Reads the link, applies agreed commands between animations and hands over to a new screen on rematch.</summary>
    public override void _Process(double delta)
    {
        if (_link is null || _rematchStarted)
        {
            return;
        }

        _link.Poll();
        if (_link.Session != _session)
        {
            _rematchStarted = true;
            _actions.Rematch();
            return;
        }

        if (!_animating && _link.HasPending)
        {
            ApplyLinked();
        }

        var state = (_link.CanSend, _link.HasPending, _link.CloseReason, _link.Rematch);
        if (state != _linkState)
        {
            _linkState = state;
            Refresh();
        }
    }

    /// <summary>True when the player here may send a command now.</summary>
    private bool LocalCanAct => _link is null
        ? _session.HumanToAct == _local
        : _link.CanSend && _session.CanAct(_local);

    private string OpponentName => _session.State.Player(_opponent).Name;

    public override void _UnhandledInput(InputEvent @event)
    {
        if (_animating)
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
            if (!ClearSelection())
            {
                OpenPauseMenu();
            }

            GetViewport().SetInputAsHandled();
            return;
        }

        if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Right } && ClearSelection())
        {
            GetViewport().SetInputAsHandled();
        }
    }

    private void BuildLayout()
    {
        AddChild(new ColorRect { Color = Palette.Background, AnchorRight = 1, AnchorBottom = 1, MouseFilter = MouseFilterEnum.Ignore });

        var root = new HBoxContainer();
        root.SetAnchorsPreset(LayoutPreset.FullRect);
        root.AddThemeConstantOverride("separation", 8);
        root.AddChild(BuildLeftColumn());
        root.AddChild(BuildBoard());
        root.AddChild(BuildRightColumn());

        var margin = new MarginContainer();
        margin.SetAnchorsPreset(LayoutPreset.FullRect);
        foreach (var side in new[] { "left", "right", "top", "bottom" })
        {
            margin.AddThemeConstantOverride($"margin_{side}", 8);
        }

        margin.AddChild(root);
        AddChild(margin);
        AddChild(_flights);

        _banner.Visible = false;
        _banner.AddThemeColorOverride("font_outline_color", Colors.Black);
        _banner.AddThemeConstantOverride("outline_size", 8);
        var bannerCenter = new CenterContainer { MouseFilter = MouseFilterEnum.Ignore };
        bannerCenter.SetAnchorsPreset(LayoutPreset.FullRect);
        bannerCenter.AddChild(_banner);
        AddChild(bannerCenter);

        AddChild(_overlay);
    }

    /// <summary>Opponent panel and piles on top, the player's at the bottom, turn status and pass in between.</summary>
    private VBoxContainer BuildLeftColumn()
    {
        _humanPanel.LeaderPressed += () => Submit(new UseLeaderCommand(_local));
        _status.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _hint.AutowrapMode = TextServer.AutowrapMode.WordSmart;

        var column = new VBoxContainer { CustomMinimumSize = new Vector2(250, 0) };
        column.AddThemeConstantOverride("separation", 10);
        column.AddChild(_aiPanel);
        column.AddChild(Piles(_aiDraw, _aiDiscard));
        column.AddChild(Widgets.Spacer());
        column.AddChild(_status);
        column.AddChild(_hint);
        column.AddChild(_passButton);
        column.AddChild(Widgets.Spacer());
        column.AddChild(Piles(_humanDraw, _humanDiscard));
        column.AddChild(_humanPanel);
        column.AddChild(GearButton());
        return column;
    }

    /// <summary>Bottom-left settings button: opens the same menu as Escape.</summary>
    private Button GearButton()
    {
        var gear = new Button
        {
            Icon = Icons.GearIcon,
            TooltipText = "Menu (Esc)",
            FocusMode = FocusModeEnum.None,
            ExpandIcon = true,
            CustomMinimumSize = new Vector2(44, 44),
            SizeFlagsHorizontal = SizeFlags.ShrinkBegin,
        };
        gear.AddThemeConstantOverride("icon_max_width", 26);
        gear.Pressed += OpenPauseMenu;
        return gear;
    }

    private static HBoxContainer Piles(PileView draw, PileView discard)
    {
        var piles = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        piles.AddThemeConstantOverride("separation", 24);
        piles.AddChild(draw);
        piles.AddChild(discard);
        return piles;
    }

    private VBoxContainer BuildBoard()
    {
        var board = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        board.AddThemeConstantOverride("separation", 4);
        board.AddChild(_aiHand);

        // Clicks that reach the field (gaps, weather lane) play board-wide cards; rows and cards accept their own.
        var field = _field;
        field.AddThemeConstantOverride("separation", 4);
        field.GuiInput += OnFieldInput;
        foreach (var row in RowsFromCenter.Reverse())
        {
            field.AddChild(CreateRow(_opponent, row));
        }

        var weatherLane = Widgets.Panel(_weather, _weatherStyle);
        weatherLane.CustomMinimumSize = new Vector2(0, CardView.BoardSize.Y);
        weatherLane.MouseFilter = MouseFilterEnum.Ignore;
        field.AddChild(weatherLane);

        foreach (var row in RowsFromCenter)
        {
            field.AddChild(CreateRow(_local, row));
        }

        board.AddChild(field);

        var handScroll = new ScrollContainer
        {
            CustomMinimumSize = new Vector2(0, CardView.HandSize.Y + 16),
            VerticalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        _hand.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _hand.AddThemeConstantOverride("separation", 4);
        handScroll.AddChild(_hand);
        board.AddChild(handScroll);
        return board;
    }

    private RowView CreateRow(Seat side, RowType row)
    {
        var view = new RowView(side, row);
        view.Clicked += v => PlayOn(_targets.ForRow(v.Side, v.Row));
        _rows[(side, row)] = view;
        return view;
    }

    /// <summary>A thin always-visible strip with the collapse arrow, next to the log body that collapses.</summary>
    private HBoxContainer BuildRightColumn()
    {
        var strip = new VBoxContainer();
        strip.AddChild(_logToggle);

        var header = new HBoxContainer();
        header.AddThemeConstantOverride("separation", 6);
        var title = Widgets.Label("Match log", 16, Palette.Muted);
        title.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        title.VerticalAlignment = VerticalAlignment.Center;
        header.AddChild(title);
        header.AddChild(_copyLog);

        _log.SelectionEnabled = true;
        var logPanel = Widgets.Panel(_log, Widgets.Box(Palette.Panel, Palette.PanelBorder, 1, margin: 8));
        logPanel.SizeFlagsVertical = SizeFlags.ExpandFill;

        _logBody.AddThemeConstantOverride("separation", 8);
        _logBody.AddChild(header);
        _logBody.AddChild(logPanel);

        var column = new HBoxContainer();
        column.AddThemeConstantOverride("separation", 4);
        column.AddChild(strip);
        column.AddChild(_logBody);
        ShowLog(_settings.Current.Interface.ShowMatchLog);
        return column;
    }

    private void ShowLog(bool visible)
    {
        _logBody.Visible = visible;
        _logToggle.Text = visible ? "»" : "«";
        _logToggle.TooltipText = visible ? "Hide the match log" : "Show the match log";
    }

    private void ToggleLog()
    {
        var visible = !_logBody.Visible;
        ShowLog(visible);
        _settings.Update(s => s with { Interface = s.Interface with { ShowMatchLog = visible } });
        _settings.Flush();
    }

    private void CopyLog()
    {
        DisplayServer.ClipboardSet(_log.GetParsedText());
        _copyLog.Icon = Icons.CheckIcon;
        _copyLog.TooltipText = "Copied!";
        GetTree().CreateTimer(CopiedFeedbackDuration).Timeout += () =>
        {
            if (IsInstanceValid(_copyLog))
            {
                _copyLog.Icon = Icons.CopyIcon;
                _copyLog.TooltipText = CopyLogTooltip;
            }
        };
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
            _aiTimer.Paused = true;
            _bannerTimer.Paused = true;
            _flights.SetPaused(true);
        }

        Action? forfeit = _session.IsControlled(_local) || _session.IsFinished || _link?.CloseReason is not null ? null : Forfeit;
        _pauseMenu = new PauseMenu(_settings, ClosePauseMenu, forfeit, _actions.ExitToMenu, _actions.Quit);
        AddChild(_pauseMenu);
    }

    /// <summary>Concedes the match: any running animation is dropped and the defeat screen shows right away.</summary>
    private void Forfeit()
    {
        ClosePauseMenu();
        if (_link is not null)
        {
            _link.Send(new ForfeitCommand(_local));
            return;
        }

        _aiTimer.Stop();
        _overlay.StopAnimations();
        _flights.Clear();
        _hiddenIds.Clear();
        _animating = false;
        SubmitNow(new ForfeitCommand(_local));
        Refresh();
    }

    private void ClosePauseMenu()
    {
        _pauseMenu?.QueueFree();
        _pauseMenu = null;
        _aiTimer.Paused = false;
        _bannerTimer.Paused = false;
        _flights.SetPaused(false);
    }

    private float DurationScale => (float)_settings.Current.Gameplay.DurationScale;

    private void Refresh()
    {
        if (_animating)
        {
            return;
        }

        Render(animating: false);

        if (_session.IsControllerTurn && _aiTimer.IsStopped())
        {
            _aiTimer.Start(_nextAiDelay);
            _nextAiDelay = AiDelay;
        }
    }

    /// <summary>
    /// Renders the current state. While <paramref name="animating"/>, nothing is clickable, <see cref="_hiddenIds"/> stay
    /// invisible in their slots and scores, gems and piles keep their values until <see cref="RenderNumbers"/>.
    /// </summary>
    private void Render(bool animating, int pendingAiDraws = 0)
    {
        var state = _session.State;
        var engine = _session.Engine;
        var humanCanAct = !animating && LocalCanAct && state.Phase == MatchPhase.Playing && state.PendingChoice is null;
        var legal = humanCanAct ? engine.GetLegalCommands(_local) : [];

        _targets = humanCanAct && _selectedCardId is { } selected ? PlayTargets.For(engine, _local, selected) : PlayTargets.None;
        if (_targets.IsEmpty)
        {
            _selectedCardId = null;
        }

        _passButton.Disabled = !legal.Contains(new PassCommand(_local));
        (_status.Text, _hint.Text) = StatusText();
        _hint.Visible = _hint.Text.Length > 0;
        _views.Clear();

        if (animating)
        {
            var ai = state.Player(_opponent);
            _aiHand.Show(ai.Name, Math.Max(0, ai.Hand.Count - pendingAiDraws));
            _overlay.Close();
        }
        else
        {
            RenderNumbers(legal);
        }

        RenderRows(showScores: !animating);
        RenderWeather();
        RenderHand(humanCanAct);

        if (!animating)
        {
            RenderOverlay();
        }
    }

    /// <summary>Scores, gems, pass state, leader and piles: what changes once the moving cards have landed.</summary>
    private void RenderNumbers(IReadOnlyCollection<GameCommand> legal)
    {
        var engine = _session.Engine;
        _humanPanel.Show(engine, _local, IsActive(_local), legal.Contains(new UseLeaderCommand(_local)), DurationScale);
        _aiPanel.Show(engine, _opponent, IsActive(_opponent), canUseLeader: false, DurationScale);
        foreach (var ((side, rowType), view) in _rows)
        {
            view.SetScore(engine.RowScore(side, rowType));
        }

        RenderPiles();
    }
    private bool IsActive(Seat seat) => _session.State.SeatToAct == seat;

    /// <summary>The phase line (display font, capitals) and an optional hint line (body font).</summary>
    private (string Phase, string Hint) StatusText()
    {
        var state = _session.State;
        if (state.Phase == MatchPhase.Finished)
        {
            return ("MATCH OVER", string.Empty);
        }

        if (state.Phase == MatchPhase.Draft)
        {
            return ("DRAFT PHASE", string.Empty);
        }

        if (state.SeatToAct is { } seat && _session.IsControlled(seat))
        {
            return ($"{state.Player(seat).Name} is thinking...".ToUpperInvariant(), string.Empty);
        }

        if (state.SeatToAct is { } other && other != _local)
        {
            return ($"ROUND {state.Round} - {state.Player(other).Name.ToUpperInvariant()}'S TURN", string.Empty);
        }

        var phase = $"ROUND {state.Round} - YOUR TURN";
        if (_selectedCardId is null)
        {
            return (phase, string.Empty);
        }

        var action = _targets.IsBoardWide ? "Click anywhere on the board to play it."
            : _targets.Rows.Count > 0 ? "Click a highlighted row to play it."
            : "Click a highlighted card to target it.";
        return (phase, $"{action}\nRight-click to cancel.");
    }

    private void RenderPiles()
    {
        var human = _session.State.Player(_local);
        var ai = _session.State.Player(_opponent);
        _humanDraw.Show(human.Deck.Count);
        _humanDiscard.Show(human.Discard.Count, TopOf(human.Discard));
        _aiDraw.Show(ai.Deck.Count);
        _aiDiscard.Show(ai.Discard.Count, TopOf(ai.Discard));
        _aiHand.Show(ai.Name, ai.Hand.Count);
    }

    private static CardInstance? TopOf(IReadOnlyList<CardInstance> pile) => pile.Count == 0 ? null : pile[^1];

    private void RenderRows(bool showScores)
    {
        foreach (var ((side, rowType), view) in _rows)
        {
            var row = _session.State.Player(side).Row(rowType);
            var cards = row.Cards.Select(card =>
            {
                var cardView = BoardCard(card, _session.Engine.Strength(row, card), side, rowType);
                cardView.SetHighlight(_targets.AcceptsCard(card.Id) ? CardHighlight.Target : CardHighlight.None);
                return cardView;
            });
            var horn = row.HornCard is { } hornCard ? BoardCard(hornCard, null, side, rowType) : null;

            view.Show(
                _session.State.IsWeatherActive(rowType),
                _targets.IsBoardWide || _targets.AcceptsRow(side, rowType),
                cards,
                horn,
                row.HasLeaderHorn);
            if (showScores)
            {
                view.SetScore(_session.Engine.RowScore(side, rowType));
            }
        }
    }

    /// <summary>A click on a board card targets it (decoy); otherwise it counts as a click on its row.</summary>
    private CardView BoardCard(CardInstance card, int? strength, Seat side, RowType row)
    {
        var view = Track(new CardView(card, CardView.BoardSize, strength) { MouseDefaultCursorShape = CursorShape.Arrow });
        view.Clicked += v => PlayOn(_targets.ForCard(v.Card.Id) ?? _targets.ForRow(side, row));
        return view;
    }

    /// <summary>Registers a rendered card (flights start and land on it) and hides it while its flight is pending.</summary>
    private CardView Track(CardView view)
    {
        _views[view.Card.Id] = view;
        if (_hiddenIds.Contains(view.Card.Id))
        {
            view.Modulate = Colors.Transparent;
        }

        return view;
    }

    private void RenderWeather()
    {
        var boardWide = _targets.IsBoardWide;
        _weatherStyle.BorderColor = boardWide ? Palette.Target : new Color(0, 0, 0, 0);

        _weather.ClearChildren();
        if (_session.State.Weather.Count == 0)
        {
            var label = Widgets.Label("Clear skies", 16, Palette.Muted);
            label.VerticalAlignment = VerticalAlignment.Center;
            _weather.AddChild(label);
            return;
        }

        foreach (var card in _session.State.Weather)
        {
            var view = Track(new CardView(card, CardView.BoardSize) { MouseDefaultCursorShape = CursorShape.Arrow });
            view.Clicked += _ => PlayOn(_targets.ForBoard());
            _weather.AddChild(view);
        }
    }

    private void RenderHand(bool humanCanAct)
    {
        _hand.ClearChildren();
        foreach (var card in _session.State.Player(_local).Hand)
        {
            var view = Track(new CardView(card, CardView.HandSize));
            if (humanCanAct)
            {
                view.Clicked += OnHandCardClicked;
            }
            else
            {
                view.MouseDefaultCursorShape = CursorShape.Arrow;
            }

            view.SetHighlight(card.Id == _selectedCardId ? CardHighlight.Selected : CardHighlight.None);
            _hand.AddChild(view);
        }
    }
    private void RenderOverlay()
    {
        var state = _session.State;
        if (state.Phase != MatchPhase.Draft)
        {
            _draft = null;
        }

        if (state.Phase == MatchPhase.Finished)
        {
            OpenEndOverlay();
        }
        else if (_link?.CloseReason is { } reason)
        {
            _overlay.Open("Match stopped", reason, [], null, ("Main menu", _actions.ExitToMenu));
        }
        else if (state.Phase == MatchPhase.Draft && LocalCanAct)
        {
            OpenDraft();
        }
        else if (state.Phase == MatchPhase.Draft && _link is not null && _session.CanAct(_local))
        {
            // The draft was sent; its redraw animation plays once it comes back.
            _overlay.SetButtons();
        }
        else if (state.Phase == MatchPhase.Draft && _link is not null)
        {
            _overlay.Open("Draft phase", $"Waiting for {OpponentName} to finish drafting...", [], null);
        }
        else if (state.PendingChoice is { } choice && choice.Seat == _local && LocalCanAct)
        {
            var title = choice.Kind == ChoiceKind.MedicRevive ? "Revive a unit" : "Return a card to your hand";
            var cards = choice.Options.Select(state.FindCard).OfType<CardInstance>();
            _overlay.Open(title, string.Empty, cards, card => Submit(new ResolveChoiceCommand(_local, card.Id)));
        }
        else
        {
            _overlay.Close();
        }
    }

    private void OpenEndOverlay()
    {
        var state = _session.State;
        var winner = state.Outcome?.Winner;
        var title = winner is null ? "Draw" : winner == _local ? "Victory" : "Defeat";
        var lines = state.RoundResults.Select(r => $"Round {r.Round}:  {r.ScoreOne} - {r.ScoreTwo}").ToList();
        if (state.Outcome?.ForfeitedBy is { } quitter)
        {
            lines.Insert(0, quitter == _local ? "You forfeited the match." : $"{state.Player(quitter).Name} forfeited the match.");
        }

        if (_link is null)
        {
            _overlay.Open(title, string.Join('\n', lines), [], null, ("Rematch", _actions.Rematch), ("Main menu", _actions.ExitToMenu));
            return;
        }

        var menu = ("Main menu", _actions.ExitToMenu);
        if (_link.CloseReason is { } reason)
        {
            if (_link.OpponentLeft)
            {
                lines.Add(reason);
            }

            _overlay.Open(title, string.Join('\n', lines), [], null, menu);
            return;
        }

        switch (_link.Rematch)
        {
            case RematchStatus.RequestedByYou:
                lines.Add($"Waiting for {OpponentName} to accept the rematch...");
                _overlay.Open(title, string.Join('\n', lines), [], null, menu);
                break;
            case RematchStatus.RequestedByOpponent:
                lines.Add($"{OpponentName} wants a rematch.");
                _overlay.Open(title, string.Join('\n', lines), [], null, ("Accept rematch", RequestRematch), menu);
                break;
            default:
                _overlay.Open(title, string.Join('\n', lines), [], null, ("Rematch", RequestRematch), menu);
                break;
        }
    }

    private void RequestRematch()
    {
        _link?.RequestRematch();
        Refresh();
    }

    /// <summary>Draft phase: clicking toggles a card's selection; the button redraws the selection (or keeps the hand).</summary>
    private void OpenDraft()
    {
        var draft = _draft ??= new DraftSelection(_session.State.Player(_local));
        if (_shownDraft == draft && _overlay.Visible)
        {
            UpdateDraft(draft);
            return;
        }

        _shownDraft = draft;
        _overlay.Open("Draft phase", draft.Instructions, _session.State.Player(_local).Hand, Overlay.DraftCardSize, card =>
        {
            if (draft.Toggle(card.Id))
            {
                UpdateDraft(draft);
            }
        });
        UpdateDraft(draft);
    }

    /// <summary>Updates highlights and the button in place, so hovering (and the card preview) is not interrupted.</summary>
    private void UpdateDraft(DraftSelection draft)
    {
        _overlay.Highlight(card => draft.IsSelected(card.Id) ? CardHighlight.Redraw : CardHighlight.None);
        _overlay.SetButtons((draft.ConfirmText, () => FinishDraft(draft)));
    }

    private void FinishDraft(DraftSelection draft)
    {
        if (_link is not null)
        {
            if (LocalCanAct)
            {
                SubmitNow(draft.ToCommand());
                Refresh();
            }

            return;
        }

        RunDraftCommand(() => SubmitNow(draft.ToCommand()));
    }

    /// <summary>Applies a draft command, then plays the redraw animation (if the player here redrew) before re-rendering.</summary>
    private void RunDraftCommand(Action command)
    {
        var drawPile = new Rect2(_humanDraw.TopGlobalPosition, OwnPileCardSize);
        _humanDraftResult = null;
        command();

        if (_humanDraftResult is { ReturnedCardIds.Count: > 0 } result && DurationScale > 0)
        {
            var swaps = new List<(int ReturnedId, CardInstance Drawn)>();
            foreach (var (returnedId, drawnId) in result.ReturnedCardIds.Zip(result.DrawnCardIds))
            {
                if (_session.State.FindCard(drawnId) is { } drawn)
                {
                    swaps.Add((returnedId, drawn));
                }
            }

            _animating = true;
            _overlay.AnimateRedraw(swaps, drawPile, DurationScale, () =>
            {
                _animating = false;
                Refresh();
            });
            return;
        }

        Refresh();
    }

    private void OnHandCardClicked(CardView view)
    {
        var cardId = view.Card.Id;
        if (_selectedCardId == cardId)
        {
            ClearSelection();
            return;
        }

        var targets = PlayTargets.For(_session.Engine, _local, cardId);
        if (targets.IsEmpty)
        {
            _hint.Text = $"{view.Card.Definition.Name} cannot be played right now.";
            _hint.Visible = true;
            return;
        }

        _selectedCardId = cardId;
        _targets = targets;
        Refresh();
    }

    private void OnFieldInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
        {
            PlayOn(_targets.ForBoard());
        }
    }

    private void PlayOn(PlayCardCommand? command)
    {
        if (command is not null)
        {
            Submit(command);
        }
    }

    /// <summary>Drops the selected hand card, if any. Returns whether there was one.</summary>
    private bool ClearSelection()
    {
        if (_selectedCardId is null)
        {
            return false;
        }

        _selectedCardId = null;
        _targets = PlayTargets.None;
        Refresh();
        return true;
    }

    private void Submit(GameCommand command)
    {
        if (_animating)
        {
            return;
        }

        if (_link is not null)
        {
            if (LocalCanAct)
            {
                SubmitNow(command);
                Refresh();
            }

            return;
        }

        Execute(() => SubmitNow(command));
    }

    /// <summary>Applies a command here, or sends it to the remote match (it is applied when it comes back agreed).</summary>
    private void SubmitNow(GameCommand command)
    {
        _selectedCardId = null;
        _targets = PlayTargets.None;
        _draft = null;
        if (_link is not null)
        {
            _link.Send(command);
            return;
        }

        ReportFailure(_session.Submit(command));
    }

    /// <summary>Applies the next agreed command of the remote match, animated like a local one.</summary>
    private void ApplyLinked()
    {
        if (_session.State.Phase == MatchPhase.Draft)
        {
            RunDraftCommand(() => ReportFailure(_link!.ApplyNext()));
            return;
        }

        Execute(() => ReportFailure(_link!.ApplyNext()));
    }

    private void ReportFailure(CommandResult? result)
    {
        if (result is { IsSuccess: false })
        {
            AppendLog($"[color=#ff7070]{result.Error}[/color]");
        }
    }

    private void OnAiTimer()
    {
        if (_animating)
        {
            _aiTimer.Start(AiDelay);
            return;
        }

        try
        {
            Execute(() => _session.StepController());
        }
        catch (InvalidOperationException ex)
        {
            GD.PushError(ex.ToString());
            AppendLog($"[color=#ff7070]AI error: {ex.Message}[/color]");
        }
    }

    /// <summary>Runs a command, then animates the cards it moved (in play only: the draft has its own animation).</summary>
    private void Execute(Action command)
    {
        var state = _session.State;
        var animate = state.Phase == MatchPhase.Playing && DurationScale > 0 && IsInsideTree();
        var before = animate ? CardMoves.Snapshot(state) : null;
        var origins = animate ? CaptureViews() : null;
        _commandEvents.Clear();

        command();

        var plan = before is null ? MovePlan.Empty : CardMoves.Plan(before, CardMoves.Snapshot(state), _commandEvents);
        if (plan.IsEmpty || origins is null)
        {
            Refresh();
            return;
        }

        Animate(plan, origins);
    }

    private Dictionary<int, (Rect2 Rect, int? Strength)> CaptureViews() =>
        _views.Values
            .Where(view => IsInstanceValid(view) && view.IsInsideTree())
            .ToDictionary(view => view.Card.Id, view => (view.GetGlobalRect(), view.CurrentStrength));

    private void Animate(MovePlan plan, Dictionary<int, (Rect2 Rect, int? Strength)> origins)
    {
        _animating = true;
        _flights.DurationScale = DurationScale;

        // Cards leaving a visible spot stay drawn there (as ghosts) until their own stage flies them away.
        foreach (var move in plan.Moves.DistinctBy(m => m.Card.Id))
        {
            if (origins.TryGetValue(move.Card.Id, out var origin))
            {
                var size = move.To.Kind == ZoneKind.Reveal ? RevealRect().Size : origin.Rect.Size;
                _flights.Place(move.Card, origin.Rect, size, origin.Strength);
            }
        }

        _hiddenIds.Clear();
        _hiddenIds.UnionWith(plan.Moves.Where(m => IsRendered(m.To)).Select(m => m.Card.Id));
        Render(animating: true, pendingAiDraws: plan.Moves.Count(m => m.To == CardZone.Hand(_opponent)));

        // Containers lay the new cards out at the end of this frame; their rects are known on the next one.
        GetTree().Connect(SceneTree.SignalName.ProcessFrame, Callable.From(() => RunStages(plan, origins)), (uint)ConnectFlags.OneShot);
    }

    /// <summary>Cards enter play (a revealed special pauses), then go to the piles, then the numbers update and cards are drawn.</summary>
    private void RunStages(MovePlan plan, Dictionary<int, (Rect2 Rect, int? Strength)> origins)
    {
        if (!IsInsideTree())
        {
            return;
        }

        var entering = plan.InStage(MoveStage.EnterPlay);
        var hold = Math.Max(
            entering.Any(m => m.To.Kind == ZoneKind.Reveal) ? RevealHold : 0,
            plan.HoldBeforePiles ? RoundEndHold : 0);

        _flights.Fly(Flights(entering, origins), hold, () =>
            _flights.Fly(Flights(plan.InStage(MoveStage.ToPile), origins), 0, () =>
            {
                RenderNumbers([]);
                _flights.Fly(Flights(plan.InStage(MoveStage.ToHand), origins), 0, FinishAnimation);
            }));
    }

    private void FinishAnimation()
    {
        _flights.Clear();
        _hiddenIds.Clear();
        _animating = false;
        Refresh();
    }

    private List<CardFlight> Flights(IReadOnlyList<CardMove> moves, Dictionary<int, (Rect2 Rect, int? Strength)> origins) =>
        moves.Select(move =>
        {
            var id = move.Card.Id;
            int? strength = origins.TryGetValue(id, out var origin) ? origin.Strength
                : _views.TryGetValue(id, out var target) ? target.CurrentStrength
                : null;
            return new CardFlight(move.Card, ZoneRect(move.From, id, origins), ZoneRect(move.To, id, null), IsFaceUp(move.From), IsFaceUp(move.To))
            {
                Strength = strength,
                KeepAtEnd = move.To.Kind == ZoneKind.Reveal,
                OnLanded = () => Reveal(id),
            };
        }).ToList();

    private void Reveal(int cardId)
    {
        _hiddenIds.Remove(cardId);
        if (_views.TryGetValue(cardId, out var view) && IsInstanceValid(view))
        {
            view.Modulate = Colors.White;
        }
    }

    /// <summary>Zones drawn as card faces on this screen (a flight lands on the real card there).</summary>
    private bool IsRendered(CardZone zone) =>
        zone.IsOnBoard || zone == CardZone.Hand(_local);

    /// <summary>The opponent's hand and the decks are face down.</summary>
    private bool IsFaceUp(CardZone zone) =>
        zone.Kind != ZoneKind.Deck && zone != CardZone.Hand(_opponent);

    /// <summary>
    /// Where a card is drawn in <paramref name="zone"/>: its captured rect (<paramref name="origins"/>, before the
    /// command) or its rendered view (after), otherwise the zone's anchor (pile, opponent hand, reveal spot).
    /// </summary>
    private Rect2 ZoneRect(CardZone zone, int cardId, Dictionary<int, (Rect2 Rect, int? Strength)>? origins)
    {
        if (IsRendered(zone))
        {
            if (origins is not null && origins.TryGetValue(cardId, out var origin))
            {
                return origin.Rect;
            }

            if (origins is null && _views.TryGetValue(cardId, out var view) && IsInstanceValid(view) && view.IsInsideTree())
            {
                return view.GetGlobalRect();
            }
        }

        var deck = zone.Seat == _local ? _humanDraw : _aiDraw;
        var discard = zone.Seat == _local ? _humanDiscard : _aiDiscard;
        var pileSize = zone.Seat == _local ? OwnPileCardSize : OpponentPileCardSize;
        return zone.Kind switch
        {
            ZoneKind.Deck => new Rect2(deck.TopGlobalPosition, pileSize),
            ZoneKind.Discard => new Rect2(discard.TopGlobalPosition, pileSize),
            ZoneKind.Reveal => RevealRect(),
            ZoneKind.Hand when zone.Seat == _opponent => Centered(_aiHand.GetGlobalRect().GetCenter(), CardView.BoardSize * 0.45f),
            ZoneKind.Hand => Centered(_hand.GetGlobalRect().GetCenter(), CardView.HandSize),
            ZoneKind.Row or ZoneKind.Horn when zone.Row is { } row => Centered(_rows[(zone.Seat, row)].GetGlobalRect().GetCenter(), CardView.BoardSize),
            _ => Centered(_weather.GetGlobalRect().GetCenter(), CardView.BoardSize),
        };
    }

    /// <summary>Where one-shot specials (scorch, clear weather) are shown before going to the discard pile.</summary>
    private Rect2 RevealRect() => Centered(_field.GetGlobalRect().GetCenter(), CardView.HandSize * RevealScale);

    private static Rect2 Centered(Vector2 center, Vector2 size) => new(center - (size / 2), size);
    private void OnEventsApplied(object? sender, IReadOnlyList<GameEvent> events)
    {
        _commandEvents.AddRange(events);
        foreach (var gameEvent in events)
        {
            if (Describe(gameEvent) is { } line)
            {
                AppendLog(line);
            }

            if (gameEvent is DraftFinished draftFinished && draftFinished.Seat == _local)
            {
                _humanDraftResult = draftFinished;
            }

            if (gameEvent is RoundEnded { Result: var result } && !_session.IsFinished)
            {
                ShowBanner(result.Winner is null ? "Round drawn" : $"{_session.State.Player(result.Winner.Value).Name} wins round {result.Round}");
                _nextAiDelay = AiDelayAfterRound;
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

    private void AppendLog(string line) => _log.AppendText(line + "\n");
}
