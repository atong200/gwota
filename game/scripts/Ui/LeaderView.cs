using Godot;
using Gwota.Core.Cards;
using Gwota.Game.Infrastructure;

namespace Gwota.Game.Ui;

/// <summary>
/// A leader drawn like a card: the portrait (<see cref="CardArt.ForLeader"/>, initials until art exists) in a gold
/// frame on the faction color, with the name banner. Clickable while <see cref="Interactive"/>; <see cref="SetSelected"/>
/// highlights an armed leader, <see cref="SetMarked"/> marks the chosen one of a picker and <see cref="SetUsed"/> dims a spent one.
/// </summary>
public sealed partial class LeaderView : PanelContainer
{
    private readonly StyleBoxFlat _frame;
    private readonly float _scale;
    private bool _interactive = true;
    private SelectionFrame? _mark;

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

    /// <summary>Whether clicks raise <see cref="Clicked"/> (pointer cursor). The tooltip shows either way.</summary>
    public bool Interactive
    {
        get => _interactive;
        set
        {
            _interactive = value;
            MouseDefaultCursorShape = value ? CursorShape.PointingHand : CursorShape.Arrow;
        }
    }

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

    /// <summary>Wraps the view in a silk band of the leader's faction color (the chosen leader of a picker).</summary>
    public void SetMarked(bool marked)
    {
        if (marked == _mark is not null)
        {
            return;
        }

        if (marked)
        {
            _mark = new SelectionFrame(Leader.Faction, Px(6));
            AddChild(_mark);
        }
        else
        {
            _mark!.QueueFree();
            _mark = null;
        }
    }

    /// <summary>Greys the portrait out once the ability has been spent.</summary>
    public void SetUsed(bool used) => Modulate = used ? new Color(0.5f, 0.5f, 0.5f) : Colors.White;

    public override void _GuiInput(InputEvent @event)
    {
        if (_interactive && @event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
        {
            Clicked?.Invoke(this);
            AcceptEvent();
        }
    }

    private int Px(float baseSize, int min = 1) => Math.Max(min, (int)MathF.Round(baseSize * _scale));
}
