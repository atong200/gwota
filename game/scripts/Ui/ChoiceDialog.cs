using Godot;

namespace Gwota.Game.Ui;

/// <summary>One option of a <see cref="ChoiceDialog"/>; <see cref="Tint"/> colors its button (e.g. a faction).</summary>
public sealed record Choice(string Text, Action OnChosen, Color? Tint = null);

/// <summary>Picks one of a few options (e.g. the faction of a new deck). Escape or Cancel dismisses it.</summary>
public sealed partial class ChoiceDialog : Modal
{
    private ChoiceDialog(string title, string message, IReadOnlyList<Choice> choices)
        : base(420, ModalStyle.Game)
    {
        Content.AddChild(Title(title, 26));
        if (message.Length > 0)
        {
            var text = Widgets.Label(message, 18, Palette.Text, HorizontalAlignment.Center);
            text.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            Content.AddChild(text);
        }

        foreach (var choice in choices)
        {
            var button = MenuButton(choice.Text, () =>
            {
                QueueFree();
                choice.OnChosen();
            });
            if (choice.Tint is { } tint)
            {
                UiTheme.TintButton(button, tint);
            }

            Content.AddChild(button);
        }

        Content.AddChild(new Control { CustomMinimumSize = new Vector2(0, 4) });
        Content.AddChild(MenuButton("Cancel", Cancel));
    }

    public static void Show(Node parent, string title, string message, params Choice[] choices)
    {
        ArgumentNullException.ThrowIfNull(parent);
        ArgumentNullException.ThrowIfNull(choices);
        parent.AddChild(new ChoiceDialog(title, message, choices));
    }

    protected override void Cancel() => QueueFree();
}
