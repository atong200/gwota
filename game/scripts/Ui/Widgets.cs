using Godot;

namespace Gwota.Game.Ui;

/// <summary>Small factory helpers that keep code-built layouts readable.</summary>
public static class Widgets
{
    public static Label Label(string text, int size = 18, Color? color = null, HorizontalAlignment align = HorizontalAlignment.Left)
    {
        var label = new Label { Text = text, HorizontalAlignment = align, MouseFilter = Control.MouseFilterEnum.Ignore };
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", color ?? Palette.Text);
        return label;
    }

    /// <summary>Adds a dark outline so the text stays readable over card art.</summary>
    public static Label Outlined(this Label label, int size = 4)
    {
        label.AddThemeConstantOverride("outline_size", size);
        label.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.85f));
        return label;
    }

    public static Button Button(string text, Action onPressed, int size = 20)
    {
        var button = new Button { Text = text, FocusMode = Control.FocusModeEnum.None };
        button.AddThemeFontSizeOverride("font_size", size);
        button.Pressed += onPressed;
        return button;
    }

    public static StyleBoxFlat Box(Color background, Color? border = null, int borderWidth = 0, int radius = 6, int margin = 6)
    {
        var box = new StyleBoxFlat { BgColor = background, BorderColor = border ?? background };
        box.SetBorderWidthAll(borderWidth);
        box.SetCornerRadiusAll(radius);
        box.SetContentMarginAll(margin);
        return box;
    }

    public static PanelContainer Panel(Control content, StyleBox style)
    {
        var panel = new PanelContainer();
        panel.AddThemeStyleboxOverride("panel", style);
        panel.AddChild(content);
        return panel;
    }

    /// <summary>
    /// A small bold badge such as "READY": <paramref name="color"/> text and border over a faint fill.
    /// Its tooltip works (the tag stops the mouse).
    /// </summary>
    public static PanelContainer Tag(string text, Color color, string tooltip = "", int size = 15)
    {
        var label = Label(text, size, color);
        label.AddThemeFontOverride("font", Fonts.Bold);
        var tag = Panel(label, Box(new Color(color, 0.14f), color, 1, radius: 4, margin: 4));
        tag.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        tag.MouseFilter = Control.MouseFilterEnum.Stop;
        tag.TooltipText = tooltip;
        return tag;
    }

    /// <summary>
    /// A square icon-only button (rename, delete...) on <paramref name="background"/>; the icon is tinted by the button
    /// theme's icon colors.
    /// </summary>
    public static Button IconButton(Texture2D icon, string tooltip, Action onPressed, Color? background = null, float size = 38)
    {
        var button = new Button
        {
            Icon = icon,
            ExpandIcon = true,
            IconAlignment = HorizontalAlignment.Center,
            FocusMode = Control.FocusModeEnum.None,
            TooltipText = tooltip,
            CustomMinimumSize = new Vector2(size, size),
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
        };
        UiTheme.TintButton(button, background ?? Palette.Button, padding: 7);
        button.Pressed += onPressed;
        return button;
    }

    public static Control Spacer(bool vertical = true) => new()
    {
        SizeFlagsVertical = vertical ? Control.SizeFlags.ExpandFill : Control.SizeFlags.Fill,
        SizeFlagsHorizontal = vertical ? Control.SizeFlags.Fill : Control.SizeFlags.ExpandFill,
        MouseFilter = Control.MouseFilterEnum.Ignore,
    };


    /// <summary>Detaches then frees every child, so containers re-layout immediately.</summary>
    public static void ClearChildren(this Node node)
    {
        foreach (var child in node.GetChildren())
        {
            node.RemoveChild(child);
            child.QueueFree();
        }
    }
}
