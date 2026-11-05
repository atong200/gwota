using Godot;
using Gwota.Core.Cards;
using Gwota.Core.Data;

namespace Gwota.Game.Ui;

/// <summary>
/// A clickable card back in a frame: gold at rest, brighter on hover, wrapped in a silk band of the deck's faction color
/// when selected (the deck's current back).
/// </summary>
public sealed partial class CardBackTile : PanelContainer
{
    private const int FrameWidth = 3;
    private readonly StyleBoxFlat _frame = Widgets.Box(new Color(0, 0, 0, 0), Palette.Hero, 2, radius: 8, margin: FrameWidth);
    private readonly Faction _faction;
    private SelectionFrame? _mark;
    private bool _hovered;

    public CardBackTile(CardBackStyle style, Vector2 size)
    {
        BackId = style.Id ?? throw new ArgumentException("A card back tile needs a back id.", nameof(style));
        _faction = style.Faction;
        CustomMinimumSize = size;
        MouseFilter = MouseFilterEnum.Stop;
        MouseDefaultCursorShape = CursorShape.PointingHand;
        _frame.AntiAliasing = true;
        AddThemeStyleboxOverride("panel", _frame);
        AddChild(new CardBack(size - new Vector2(2 * FrameWidth, 2 * FrameWidth), style));
        MouseEntered += () => SetHovered(true);
        MouseExited += () => SetHovered(false);
        Style();
    }

    public event Action<CardBackTile>? Clicked;

    public string BackId { get; }

    public void SetSelected(bool selected)
    {
        if (selected == _mark is not null)
        {
            return;
        }

        if (selected)
        {
            _mark = new SelectionFrame(_faction);
            AddChild(_mark);
        }
        else
        {
            _mark!.QueueFree();
            _mark = null;
        }
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
        {
            Clicked?.Invoke(this);
            AcceptEvent();
        }
    }

    private void SetHovered(bool hovered)
    {
        _hovered = hovered;
        Style();
    }

    private void Style()
    {
        var color = _hovered ? Palette.Hero.Lightened(0.3f) : Palette.Hero;
        _frame.BorderColor = color;
        _frame.ShadowColor = new Color(color, 0.5f);
        _frame.ShadowSize = _hovered ? 5 : 2;
    }
}
