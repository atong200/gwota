using Godot;

namespace Gwota.Game.Ui;

/// <summary>Asks for one line of text (e.g. a deck name). Enter confirms, Escape cancels.</summary>
public sealed partial class TextInputDialog : Modal
{
    private readonly LineEdit _input;
    private readonly Func<string, bool> _onConfirm;

    /// <param name="onConfirm">Applies the text; returns false to keep the dialog open (e.g. a blank name).</param>
    private TextInputDialog(string title, string initialText, int maxLength, Func<string, bool> onConfirm)
        : base(440, ModalStyle.Game)
    {
        _onConfirm = onConfirm;
        _input = new LineEdit { Text = initialText, MaxLength = maxLength, SelectAllOnFocus = true, CustomMinimumSize = new Vector2(0, 42) };
        _input.AddThemeFontSizeOverride("font_size", 20);
        _input.TextSubmitted += _ => Confirm();

        Content.AddChild(Title(title, 26));
        Content.AddChild(_input);

        var buttons = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        buttons.AddThemeConstantOverride("separation", 16);
        foreach (var button in new[] { MenuButton("Cancel", Cancel), MenuButton("OK", Confirm) })
        {
            button.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            buttons.AddChild(button);
        }

        Content.AddChild(buttons);
    }

    public static void Show(Node parent, string title, string initialText, int maxLength, Func<string, bool> onConfirm)
    {
        ArgumentNullException.ThrowIfNull(parent);
        ArgumentNullException.ThrowIfNull(onConfirm);
        parent.AddChild(new TextInputDialog(title, initialText, maxLength, onConfirm));
    }

    public override void _Ready()
    {
        _input.GrabFocus();
        _input.SelectAll();
    }

    protected override void Cancel() => QueueFree();

    private void Confirm()
    {
        if (_onConfirm(_input.Text))
        {
            QueueFree();
        }
    }
}
