using Godot;
using Gwota.Core.Common;
using Gwota.Core.Match;
using Gwota.Core.Presentation;

namespace Gwota.Game.Ui;

/// <summary>
/// Side panel for one player: name, total score medallion, round crystals and pass state in a column, with the leader
/// portrait on the right (piles are <see cref="PileView"/>s). The portrait is clicked like a hand card to select the
/// leader ability.
/// </summary>
public sealed partial class PlayerPanel : PanelContainer
{
    public static readonly Vector2 PortraitSize = new(96, 128);

    private readonly StyleBoxFlat _style;
    private readonly Label _name;
    private readonly ScoreBadge _score = new(58, 26, ring: 3);
    private readonly GemView _gems = new();
    private readonly Label _passed;
    private readonly CenterContainer _portraitSlot = new() { CustomMinimumSize = PortraitSize };
    private LeaderView? _portrait;
    private bool _canUseLeader;
    private bool _leaderSelected;

    public PlayerPanel()
    {
        _style = Widgets.Box(Palette.Panel, Palette.PanelBorder, 2, radius: 8, margin: 10);
        AddThemeStyleboxOverride("panel", _style);

        _name = Widgets.Label(string.Empty, 22);
        _name.ClipText = true;
        _name.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        _score.TooltipText = "Total strength on the board. The highest total at the end of the round wins it.";
        _score.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
        _gems.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
        _passed = Widgets.Label("PASSED", 18, Palette.Debuff);

        var info = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }.WithGap(4);
        info.AddChild(_name);
        info.AddChild(_score);
        info.AddChild(_gems);
        info.AddChild(_passed);

        _portraitSlot.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        var layout = new HBoxContainer().WithGap(10);
        layout.AddChild(info);
        layout.AddChild(_portraitSlot);
        AddChild(layout);
    }

    /// <summary>Raised when the leader portrait is clicked while the ability can be used.</summary>
    public event Action? LeaderClicked;

    /// <param name="durationScale">Animation speed for a lost crystal (0 = no animation).</param>
    public void Show(MatchEngine engine, Seat seat, bool isActive, float durationScale)
    {
        ArgumentNullException.ThrowIfNull(engine);

        var player = engine.State.Player(seat);

        _name.Text = player.Name;
        _name.TooltipText = player.Name;

        var score = engine.Score(seat);
        var leading = score > engine.Score(seat.Opponent());
        _score.SetScore(score, leading ? Palette.Hero : Palette.ButtonBorder, leading ? Palette.Hero : Palette.Text, glow: leading);
        _gems.Show(player.Gems, engine.Rules.StartingGems, durationScale);

        _passed.Modulate = player.HasPassed ? Colors.White : Colors.Transparent;

        var leader = player.Leader;
        if (_portrait?.Leader.Id != leader.Id)
        {
            _portrait?.QueueFree();
            _portrait = new LeaderView(leader, PortraitSize);
            _portrait.Clicked += _ => LeaderClicked?.Invoke();
            _portraitSlot.AddChild(_portrait);
        }

        ApplyLeaderState();
        _portrait.SetUsed(player.LeaderUsed);
        _portrait.TooltipText = CardText.LeaderLine(leader).Plain + (player.LeaderUsed ? "\nAlready used." : string.Empty);

        _style.BorderColor = isActive ? Palette.Highlight : Palette.PanelBorder;
    }

    /// <param name="canUse">Whether the portrait is clickable (the local player's usable leader).</param>
    /// <param name="selected">Whether the leader ability is selected and waits for confirmation.</param>
    public void SetLeaderState(bool canUse, bool selected)
    {
        _canUseLeader = canUse;
        _leaderSelected = selected;
        ApplyLeaderState();
    }

    private void ApplyLeaderState()
    {
        if (_portrait is null)
        {
            return;
        }

        _portrait.Interactive = _canUseLeader;
        _portrait.SetSelected(_leaderSelected);
    }
}
