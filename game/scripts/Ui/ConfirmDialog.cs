using Godot;

namespace Gwota.Game.Ui;

/// <summary>A yes/no question (e.g. "Forfeit the match?"). Escape and the cancel button dismiss it.</summary>
public sealed partial class ConfirmDialog : Modal
{
    private ConfirmDialog(string title, string message, string confirmText, Action onConfirm, ModalStyle style)
        : base(420, style)
    {
        Content.AddChild(Title(title, 26));
        var text = Widgets.Label(message, 18, Palette.Text, HorizontalAlignment.Center);
        text.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        Content.AddChild(text);

        var buttons = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        buttons.AddThemeConstantOverride("separation", 16);
        var cancel = MenuButton("Cancel", Cancel);
        var confirm = MenuButton(confirmText, () =>
        {
            QueueFree();
            onConfirm();
        });
        UiTheme.TintButton(confirm, Palette.Danger);
        foreach (var button in new[] { cancel, confirm })
        {
            button.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            buttons.AddChild(button);
        }

        Content.AddChild(buttons);
    }

    public static void Show(Node parent, string title, string message, string confirmText, Action onConfirm, ModalStyle style = ModalStyle.System)
    {
        ArgumentNullException.ThrowIfNull(parent);
        ArgumentNullException.ThrowIfNull(onConfirm);
        parent.AddChild(new ConfirmDialog(title, message, confirmText, onConfirm, style));
    }

    protected override void Cancel() => QueueFree();
}
