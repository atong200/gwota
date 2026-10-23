using System.Globalization;
using Godot;

namespace Gwota.Game.Ui;

/// <summary>A round score medallion (dark disc, colored ring, Cinzel number).</summary>
public sealed partial class ScoreBadge : PanelContainer
{
    private readonly StyleBoxFlat _style;
    private readonly Label _value;
    private readonly int _ring;

    public ScoreBadge(int diameter, int fontSize, int ring = 2)
    {
        _ring = ring;
        CustomMinimumSize = new Vector2(diameter, diameter);
        SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
        SizeFlagsVertical = SizeFlags.ShrinkCenter;
        MouseFilter = MouseFilterEnum.Pass;

        _style = Widgets.Box(Palette.BadgeBackground, Palette.PanelBorder, ring, radius: diameter, margin: 0);
        _style.AntiAliasing = true;
        AddThemeStyleboxOverride("panel", _style);

        _value = Widgets.Label("0", 0, Palette.Text, HorizontalAlignment.Center).WithDisplayFont(fontSize);
        _value.VerticalAlignment = VerticalAlignment.Center;
        AddChild(_value);
    }

    public void SetScore(int score, Color ring, Color? text = null, bool glow = false)
    {
        _value.Text = score.ToString(CultureInfo.InvariantCulture);
        _value.AddThemeColorOverride("font_color", text ?? Palette.Text);
        _style.BorderColor = ring;
        _style.SetBorderWidthAll(glow ? _ring + 1 : _ring);
        _style.ShadowColor = new Color(ring, 0.5f);
        _style.ShadowSize = glow ? 6 : 0;
    }
}
