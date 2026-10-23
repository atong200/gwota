using Godot;
using Gwota.Core.Cards;
using Gwota.Game.Infrastructure;

namespace Gwota.Game.Ui;

/// <summary>
/// A leader drawn like a card: the portrait (<see cref="CardArt.ForLeader"/>, initials until art exists) in a gold
/// frame on the faction color, with the name banner. Clickable; <see cref="SetSelected"/> marks the current leader.
/// </summary>
public sealed partial class LeaderView : PanelContainer
{
    private readonly StyleBoxFlat _frame;
    private readonly float _scale;

    public LeaderView(LeaderDefinition leader, Vector2 size)
    {
        Leader = leader ?? throw new ArgumentNullException(nameof(leader));
        CustomMinimumSize = size;
        MouseFilter = MouseFilterEnum.Stop;
        MouseDefaultCursorShape = CursorShape.PointingHand;
        _scale = size.X / CardView.HandSize.X;

        _frame = Widgets.Box(Palette.ForFaction(leader.Faction).Darkened(0.25f), radius: Px(6), margin: 0);
        _frame.AntiAliasing = true;
        SetSelected(false);
        AddThemeStyleboxOverride("panel", _frame);

        AddChild(CardFace.Art(CardArt.ForLeader(leader.Id), leader.Name, Px(28, min: 16)));
        var overlay = new Control { MouseFilter = MouseFilterEnum.Ignore };
        overlay.AddChild(CardFace.NameBanner(leader.Name, Px(10, min: 12), Px(2)));
        AddChild(overlay);
    }

    public event Action<LeaderView>? Clicked;

    public LeaderDefinition Leader { get; }

    /// <summary>Gold frame at rest; highlighted with a glow when selected (the deck's current leader).</summary>
    public void SetSelected(bool selected)
    {
        var color = selected ? Palette.Highlight : Palette.Hero;
        var scale = Math.Min(_scale, 1.6f);
        _frame.BorderColor = color;
        _frame.SetBorderWidthAll(Math.Max(1, (int)MathF.Round((selected ? 3f : 2f) * scale)));
        _frame.ShadowColor = new Color(color, 0.5f);
        _frame.ShadowSize = (int)MathF.Round((selected ? 7f : 3f) * scale);
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
        {
            Clicked?.Invoke(this);
            AcceptEvent();
        }
    }

    private int Px(float baseSize, int min = 1) => Math.Max(min, (int)MathF.Round(baseSize * _scale));
}