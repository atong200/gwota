using Godot;
using Gwota.Core.Cards;
using Gwota.Core.Common;
using Gwota.Core.Data;
using Gwota.Core.Match;
using Gwota.Core.Presentation;

namespace Gwota.Game.Ui;

/// <summary>
/// The column left of the board: the opponent's panel and piles on top, the player's at the bottom, the turn status,
/// hint and pass button in between, and the menu button in the corner.
/// </summary>
public sealed partial class MatchSidePanel : VBoxContainer
{
    private static readonly Vector2 OwnPileCardSize = CardView.OfHeight(98);
    private static readonly Vector2 OpponentPileCardSize = CardView.OfHeight(78);

    private readonly PlayerPanel _ownPanel = new();
    private readonly PlayerPanel _opponentPanel = new();
    private readonly PileView _ownDraw = new(PileKind.Draw, OwnPileCardSize);
    private readonly PileView _ownDiscard = new(PileKind.Discard, OwnPileCardSize);
    private readonly PileView _opponentDraw = new(PileKind.Draw, OpponentPileCardSize);
    private readonly PileView _opponentDiscard = new(PileKind.Discard, OpponentPileCardSize);
    private readonly Label _status = Widgets.Label(string.Empty, 0, Palette.Highlight, HorizontalAlignment.Center).WithDisplayFont(20);
    private readonly Label _hint = Widgets.Label(string.Empty, 17, Palette.Text, HorizontalAlignment.Center);
    private readonly Button _pass;

    public MatchSidePanel(CardBackStyle ownCardBack, CardBackStyle opponentCardBack)
    {
        _ownDraw.BackStyle = ownCardBack;
        _opponentDraw.BackStyle = opponentCardBack;
        _ownPanel.LeaderClicked += () => LeaderClicked?.Invoke();
        _ownDiscard.Clicked += () => DiscardClicked?.Invoke(true);
        _opponentDiscard.Clicked += () => DiscardClicked?.Invoke(false);
        _pass = Widgets.Button("Pass round", () => PassPressed?.Invoke()).AsDisplay(22);
        _pass.TooltipText = "You won't play again until the next round.";
        _status.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _hint.AutowrapMode = TextServer.AutowrapMode.WordSmart;

        CustomMinimumSize = new Vector2(250, 0);
        this.WithGap(10);
        AddChild(_opponentPanel);
        AddChild(Piles(_opponentDraw, _opponentDiscard));
        AddChild(Widgets.Spacer());
        AddChild(_status);
        AddChild(_hint);
        AddChild(_pass);
        AddChild(Widgets.Spacer());
        AddChild(Piles(_ownDraw, _ownDiscard));
        AddChild(_ownPanel);
        AddChild(GearButton());
    }

    public event Action? LeaderClicked;

    /// <summary>A click on a discard pile; true for the player's own pile.</summary>
    public event Action<bool>? DiscardClicked;

    public event Action? PassPressed;

    public event Action? MenuPressed;

    public void ShowStatus(MatchStatus status)
    {
        _status.Text = status.Phase;
        ShowHint(status.Hint);
    }

    public void ShowHint(string hint)
    {
        _hint.Text = hint;
        _hint.Visible = hint.Length > 0;
    }

    /// <summary>What the player here can do now: pass, use the leader ability, play on the discard pile.</summary>
    public void SetActions(bool canPass, bool canUseLeader, bool leaderSelected, bool discardTargeted)
    {
        _pass.Disabled = !canPass;
        _ownPanel.SetLeaderState(canUseLeader, leaderSelected);
        _ownDiscard.SetHighlight(discardTargeted);
    }

    public void ShowPlayers(MatchEngine engine, Seat own, float durationScale)
    {
        ArgumentNullException.ThrowIfNull(engine);

        var toAct = engine.State.SeatToAct;
        _ownPanel.Show(engine, own, toAct == own, durationScale);
        _opponentPanel.Show(engine, own.Opponent(), toAct == own.Opponent(), durationScale);
    }

    public void ShowPiles(MatchState state, Seat own)
    {
        ArgumentNullException.ThrowIfNull(state);

        var player = state.Player(own);
        var opponent = state.Player(own.Opponent());
        _ownDraw.Show(player.Deck.Count);
        _ownDiscard.Show(player.Discard.Count, TopOf(player.Discard));
        _opponentDraw.Show(opponent.Deck.Count);
        _opponentDiscard.Show(opponent.Discard.Count, TopOf(opponent.Discard));
    }

    /// <summary>Where the top card of a pile is drawn, in global coordinates.</summary>
    public Rect2 PileRect(bool own, PileKind kind)
    {
        var pile = (own, kind) switch
        {
            (true, PileKind.Draw) => _ownDraw,
            (true, _) => _ownDiscard,
            (false, PileKind.Draw) => _opponentDraw,
            (false, _) => _opponentDiscard,
        };
        return new Rect2(pile.TopGlobalPosition, own ? OwnPileCardSize : OpponentPileCardSize);
    }

    private static CardInstance? TopOf(IReadOnlyList<CardInstance> pile) => pile.Count == 0 ? null : pile[^1];

    private static HBoxContainer Piles(PileView draw, PileView discard)
    {
        var piles = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center }.WithGap(24);
        piles.AddChild(draw);
        piles.AddChild(discard);
        return piles;
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
        gear.Pressed += () => MenuPressed?.Invoke();
        return gear;
    }
}
