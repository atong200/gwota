using Godot;

namespace Gwota.Game.Ui;

/// <summary>
/// A large clickable tile for a game mode: icon, title and a short description. An unavailable mode is greyed out
/// with a ribbon but stays clickable, so it can explain itself.
/// </summary>
public sealed partial class ModeTile : Button
{
    public static readonly Vector2 TileSize = new(250, 230);

    /// <summary>Distance of the ribbon's center from the top and right edges.</summary>
    private const float RibbonInset = 48;

    public ModeTile(Texture2D icon, string title, string description, Action onPressed, string? ribbon = null)
    {
        ArgumentNullException.ThrowIfNull(onPressed);
        var available = ribbon is null;
        CustomMinimumSize = TileSize;
        ClipContents = true;
        FocusMode = FocusModeEnum.None;
        UiTheme.TintButton(this, available ? Palette.Button : Palette.ButtonDisabled, padding: 16);
        Pressed += onPressed;

        var content = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center, MouseFilter = MouseFilterEnum.Ignore };
        content.SetAnchorsPreset(LayoutPreset.FullRect);
        content.AddThemeConstantOverride("separation", 10);
        var tint = available ? Palette.Highlight : Palette.TextDisabled;
        var image = Icons.Rect(icon, 72, tint);
        image.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
        content.AddChild(image);
        content.AddChild(Widgets.Label(title, 0, available ? Palette.Text : Palette.TextDisabled, HorizontalAlignment.Center).WithBoldFont(24));

        var text = Widgets.Label(description, 15, available ? Palette.Muted : Palette.TextDisabled, HorizontalAlignment.Center);
        text.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        text.CustomMinimumSize = new Vector2(TileSize.X - 40, 0);
        text.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
        content.AddChild(text);

        var margin = new MarginContainer { MouseFilter = MouseFilterEnum.Ignore };
        margin.SetAnchorsPreset(LayoutPreset.FullRect);
        foreach (var side in new[] { "left", "right", "top", "bottom" })
        {
            margin.AddThemeConstantOverride($"margin_{side}", 14);
        }

        margin.AddChild(content);
        AddChild(margin);

        if (ribbon is not null)
        {
            AddChild(Ribbon(ribbon));
        }
    }

    /// <summary>A gold band across the top-right corner, longer than the corner so the tile clips both ends.</summary>
    private static PanelContainer Ribbon(string text)
    {
        var size = new Vector2(TileSize.X, 30);
        var label = Widgets.Label(text, 0, Palette.BadgeBackground, HorizontalAlignment.Center).WithBoldFont(15);
        label.VerticalAlignment = VerticalAlignment.Center;
        var band = Widgets.Panel(label, Widgets.Box(Palette.Highlight, radius: 0, margin: 0));
        band.MouseFilter = MouseFilterEnum.Ignore;
        band.CustomMinimumSize = size;
        band.Size = size;
        band.PivotOffset = size / 2;
        band.Position = new Vector2(TileSize.X - RibbonInset, RibbonInset) - (size / 2);
        band.RotationDegrees = 45;
        return band;
    }
}
