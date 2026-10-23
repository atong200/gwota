using Godot;

namespace Gwota.Game.Ui;

/// <summary>A wait that can be cancelled (hosting, connecting): a pulsing title, details, a notice line and Cancel.</summary>
public sealed partial class WaitingDialog : Modal
{
    private const double PulseDuration = 0.9;

    private readonly Action _onCancel;
    private readonly Label _title;
    private readonly Label _notice = Widgets.Label(string.Empty, 16, Palette.Highlight, HorizontalAlignment.Center);

    public WaitingDialog(string title, string details, Action onCancel)
        : base(480)
    {
        _onCancel = onCancel ?? throw new ArgumentNullException(nameof(onCancel));
        _title = Title(title, 26);
        Content.AddChild(_title);

        var text = Widgets.Label(details, 18, Palette.Text, HorizontalAlignment.Center);
        text.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        Content.AddChild(text);

        _notice.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _notice.Visible = false;
        Content.AddChild(_notice);
        Content.AddChild(MenuButton("Cancel", Cancel));
    }

    public override void _Ready()
    {
        var pulse = _title.CreateTween().SetLoops().SetTrans(Tween.TransitionType.Sine);
        pulse.TweenProperty(_title, "modulate:a", 0.45f, PulseDuration);
        pulse.TweenProperty(_title, "modulate:a", 1f, PulseDuration);
    }

    public void SetNotice(string? notice)
    {
        _notice.Text = notice ?? string.Empty;
        _notice.Visible = !string.IsNullOrEmpty(notice);
    }

    protected override void Cancel() => _onCancel();
}
