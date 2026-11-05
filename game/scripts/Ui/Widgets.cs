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
    /// Its tooltip works (the tag stops the mouse); <paramref name="richTooltip"/>, when set, replaces the plain one.
    /// </summary>
    public static PanelContainer Tag(string text, Color color, string tooltip = "", int size = 15, Func<Control>? richTooltip = null)
    {
        var label = Label(text, size, color);
        label.AddThemeFontOverride("font", Fonts.Bold);
        var tag = new RichTooltipPanel
        {
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
            MouseFilter = Control.MouseFilterEnum.Stop,
            TooltipText = tooltip,
            TooltipContent = richTooltip,
        };
        tag.AddThemeStyleboxOverride("panel", Box(new Color(color, 0.14f), color, 1, radius: 4, margin: 4));
        tag.AddChild(label);
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

    /// <summary>Sets the space a box container leaves between its children.</summary>
    public static T WithGap<T>(this T box, int gap)
        where T : BoxContainer
    {
        box.AddThemeConstantOverride("separation", gap);
        return box;
    }

    /// <summary>
    /// Wraps <paramref name="content"/> in a <see cref="MarginContainer"/>; with <paramref name="fillParent"/> it is
    /// anchored to fill a non-container parent.
    /// </summary>
    public static MarginContainer Margins(Control content, int left, int top, int right, int bottom, bool fillParent = true)
    {
        var margin = new MarginContainer();
        if (fillParent)
        {
            margin.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        }

        margin.AddThemeConstantOverride("margin_left", left);
        margin.AddThemeConstantOverride("margin_top", top);
        margin.AddThemeConstantOverride("margin_right", right);
        margin.AddThemeConstantOverride("margin_bottom", bottom);
        margin.AddChild(content);
        return margin;
    }

    public static MarginContainer Margins(Control content, int all) => Margins(content, all, all, all, all);

    /// <summary>
    /// Makes <paramref name="wanted"/> the children of <paramref name="parent"/>, in order. Other children are detached
    /// but not freed (their owner frees them); children already in place stay in the tree, so their hover is kept.
    /// </summary>
    public static void SyncChildren(this Node parent, IReadOnlyList<Node> wanted)
    {
        ArgumentNullException.ThrowIfNull(wanted);

        var keep = wanted.ToHashSet();
        foreach (var child in parent.GetChildren())
        {
            if (!keep.Contains(child))
            {
                parent.RemoveChild(child);
            }
        }

        for (var i = 0; i < wanted.Count; i++)
        {
            var node = wanted[i];
            if (node.GetParent() != parent)
            {
                node.GetParent()?.RemoveChild(node);
                parent.AddChild(node);
            }

            parent.MoveChild(node, i);
        }
    }

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
