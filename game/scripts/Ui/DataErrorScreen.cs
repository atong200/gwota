using Godot;

namespace Gwota.Game.Ui;

/// <summary>Shown instead of the menu when the files under game/data do not load; lists every error.</summary>
public sealed partial class DataErrorScreen : Control
{
    public DataErrorScreen(IReadOnlyList<string> errors, Action onQuit)
    {
        ArgumentNullException.ThrowIfNull(errors);
        ArgumentNullException.ThrowIfNull(onQuit);

        var text = new RichTextLabel { SizeFlagsVertical = SizeFlags.ExpandFill, SelectionEnabled = true };
        text.AddThemeColorOverride("default_color", Palette.Text);
        text.Text = string.Join("\n", errors);

        var layout = new VBoxContainer();
        layout.SetAnchorsPreset(LayoutPreset.FullRect);
        layout.AddChild(Widgets.Label($"Game data is invalid ({errors.Count} error(s)) - fix the files under game/data:", 24, Palette.Error));
        layout.AddChild(text);
        layout.AddChild(Widgets.Button("Quit", onQuit));

        SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(new ColorRect { Color = Palette.Background, AnchorRight = 1, AnchorBottom = 1 });
        AddChild(Widgets.Margins(layout, left: 24, top: 24, right: 0, bottom: 0));

    }
}
