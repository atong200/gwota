using Godot;

namespace Gwota.Game.Ui;

/// <summary>Font family of a modal's title and buttons.</summary>
public enum ModalStyle
{
    /// <summary>Body font: menus, settings and other app dialogs.</summary>
    System,

    /// <summary>Display font: dialogs about game elements (decks, leaders, in-match choices).</summary>
    Game,
}

/// <summary>
/// Base for full-screen modals: dims the background, swallows clicks, centers a framed panel and maps Escape to
/// <see cref="Cancel"/>. Being added last, a modal gets Escape before its parent.
/// </summary>
public abstract partial class Modal : Control
{
    private readonly ModalStyle _style;

    protected Modal(int width, ModalStyle style = ModalStyle.System)
    {
        _style = style;
        // Top level: covers the whole screen even when opened from inside a container (containers skip top-level children).
        TopLevel = true;
        SetAnchorsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;

        var dim = new ColorRect { Color = Palette.Dim, MouseFilter = MouseFilterEnum.Ignore };
        dim.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(dim);

        Content = new VBoxContainer { CustomMinimumSize = new Vector2(width, 0) };
        Content.AddThemeConstantOverride("separation", 14);

        var center = new CenterContainer { MouseFilter = MouseFilterEnum.Ignore };
        center.SetAnchorsPreset(LayoutPreset.FullRect);
        center.AddChild(Widgets.Panel(Content, Widgets.Box(Palette.Panel, Palette.Highlight, 2, radius: 10, margin: 24)));
        AddChild(center);
    }

    protected VBoxContainer Content { get; }

    /// <summary>The modal's title, styled after <see cref="ModalStyle"/>.</summary>
    protected Label Title(string text, int size = 28)
    {
        var label = Widgets.Label(text, 0, Palette.Highlight, HorizontalAlignment.Center);
        return _style == ModalStyle.Game ? label.AsDisplay(size) : label.WithBoldFont(size);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (Visible && @event.IsActionPressed("ui_cancel"))
        {
            GetViewport().SetInputAsHandled();
            Cancel();
        }
    }

    /// <summary>What Escape does; typically the same as the modal's Back/Resume button.</summary>
    protected abstract void Cancel();

    protected Button MenuButton(string text, Action onPressed)
    {
        var button = _style == ModalStyle.Game ? Widgets.Button(text, onPressed).AsDisplay(20) : Widgets.Button(text, onPressed, 21);
        button.CustomMinimumSize = new Vector2(0, 48);
        return button;
    }
}
