using Godot;
using Gwota.Core.Common;
using Gwota.Core.Match;
using Gwota.Core.Presentation;
using Gwota.Game.Infrastructure;

namespace Gwota.Game.Ui;

/// <summary>Side panel for one player: name, total score medallion, round crystals, pass state and the leader (piles are <see cref="PileView"/>s).</summary>
public sealed partial class PlayerPanel : PanelContainer
{
    private readonly StyleBoxFlat _style;
    private readonly Label _name;
    private readonly ScoreBadge _score = new(58, 26, ring: 3);
    private readonly GemView _gems = new();
    private readonly Label _passed;
    private readonly TextureRect _leaderArt;
    private readonly Button _leader;

    public PlayerPanel()
    {
        _style = Widgets.Box(Palette.Panel, Palette.PanelBorder, 2, radius: 8, margin: 10);
        AddThemeStyleboxOverride("panel", _style);

        _name = Widgets.Label(string.Empty, 22);
        _score.TooltipText = "Total strength on the board. The highest total at the end of the round wins it.";
        _gems.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        _passed = Widgets.Label("PASSED", 18, Palette.Debuff);
        _leaderArt = new TextureRect
        {
            CustomMinimumSize = new Vector2(0, 72),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
        };
        _leader = Widgets.Button(string.Empty, () => LeaderPressed?.Invoke(), 16);
        _leader.AutowrapMode = TextServer.AutowrapMode.WordSmart;

        _passed.VerticalAlignment = VerticalAlignment.Center;
        var header = new HBoxContainer();
        header.AddThemeConstantOverride("separation", 12);
        header.AddChild(_score);
        header.AddChild(_gems);
        header.AddChild(Widgets.Spacer(vertical: false));
        header.AddChild(_passed);

        var layout = new VBoxContainer();
        layout.AddThemeConstantOverride("separation", 4);
        layout.AddChild(_name);
        layout.AddChild(header);
        layout.AddChild(_leaderArt);
        layout.AddChild(_leader);
        AddChild(layout);
    }

    public event Action? LeaderPressed;

    /// <param name="durationScale">Animation speed for a lost crystal (0 = no animation).</param>
    public void Show(MatchEngine engine, Seat seat, bool isActive, bool canUseLeader, float durationScale)
    {
        ArgumentNullException.ThrowIfNull(engine);

        var player = engine.State.Player(seat);

        _name.Text = player.Name;

        var score = engine.Score(seat);
        var leading = score > engine.Score(seat.Opponent());
        _score.SetScore(score, leading ? Palette.Hero : Palette.ButtonBorder, leading ? Palette.Hero : Palette.Text, glow: leading);
        _gems.Show(player.Gems, engine.Rules.StartingGems, durationScale);

        _passed.Visible = player.HasPassed;

        var leader = player.Leader;
        _leaderArt.Texture = CardArt.ForLeader(leader.Id);
        _leaderArt.Visible = _leaderArt.Texture is not null;
        _leader.Text = $"Leader: {leader.Name}{(player.LeaderUsed ? " (used)" : string.Empty)}";
        _leader.TooltipText = CardText.Leader(leader) + (leader.Description is null ? string.Empty : "\n" + leader.Description);
        _leader.Disabled = !canUseLeader;

        _style.BorderColor = isActive ? Palette.Highlight : Palette.PanelBorder;
    }
}
