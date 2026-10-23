using System.Globalization;
using Godot;

namespace Gwota.Game.Ui;

/// <summary>
/// The opponent's hand as a thin row of card backs above the board. Hovering enlarges it and tells the exact
/// count. The row is a child of a plain <see cref="Control"/> (not a container) so its scale is not reset by layout.
/// </summary>
public sealed partial class OpponentHandView : Control
{
    private const float HoverScale = 1.8f;
    private static readonly Vector2 BackSize = new(20, 28);

    private readonly HBoxContainer _strip = new() { MouseFilter = MouseFilterEnum.Ignore };
    private readonly Label _label = Widgets.Label(string.Empty, 14, Palette.Muted).Outlined();

    public OpponentHandView()
    {
        CustomMinimumSize = new Vector2(0, BackSize.Y);
        MouseFilter = MouseFilterEnum.Stop;
        _strip.AddThemeConstantOverride("separation", 2);
        _label.VerticalAlignment = VerticalAlignment.Center;
        _strip.AddChild(_label);
        AddChild(_strip);

        MouseEntered += () => SetEnlarged(true);
        MouseExited += () => SetEnlarged(false);
        Resized += Center;
    }

    public int Count { get; private set; }

    public void Show(string ownerName, int count)
    {
        Count = count;
        _label.Text = count == 1 ? $"{ownerName} has 1 card" : $"{ownerName} has {count.ToString(CultureInfo.InvariantCulture)} cards";

        if (_label.GetParent() == _strip)
        {
            _strip.RemoveChild(_label);
        }

        _strip.ClearChildren();
        for (var i = 0; i < count; i++)
        {
            _strip.AddChild(new CardBack(BackSize));
        }

        _strip.AddChild(_label);
        _label.Visible = count == 0 || IsEnlarged;
        Center();
    }

    private bool IsEnlarged => _strip.Scale.X > 1;

    private void SetEnlarged(bool enlarged)
    {
        _strip.Scale = enlarged ? new Vector2(HoverScale, HoverScale) : Vector2.One;
        _label.Visible = enlarged || Count == 0;
        ZIndex = enlarged ? 10 : 0;
        Center();
    }

    /// <summary>Keeps the row centered horizontally, scaling downward from its top edge.</summary>
    private void Center()
    {
        _strip.ResetSize();
        _strip.PivotOffset = new Vector2(_strip.Size.X / 2, 0);
        _strip.Position = new Vector2((Size.X - _strip.Size.X) / 2, 0);
    }
}
