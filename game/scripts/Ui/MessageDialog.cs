using Godot;

namespace Gwota.Game.Ui;

/// <summary>A one-button information popup that removes itself when dismissed.</summary>
public sealed partial class MessageDialog : Modal
{
    private MessageDialog(string title, string message)
        : base(420)
    {
        Content.AddChild(Title(title, 26));
        var text = Widgets.Label(message, 18, Palette.Text, HorizontalAlignment.Center);
        text.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        Content.AddChild(text);
        Content.AddChild(MenuButton("OK", Cancel));
    }

    public static void Show(Node parent, string title, string message)
    {
        ArgumentNullException.ThrowIfNull(parent);
        parent.AddChild(new MessageDialog(title, message));
    }

    public static void ComingSoon(Node parent, string feature) =>
        Show(parent, "Coming soon", $"{feature} is not available yet.");

    protected override void Cancel() => QueueFree();
}
