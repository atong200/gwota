using Godot;
using Gwota.Game.Infrastructure;

namespace Gwota.Game.Ui;

/// <summary>
/// The match log on the right of the board: a thin always-visible strip with the collapse arrow, next to the log body
/// (title, copy button and the lines) that collapses. Whether it is shown is remembered in the settings.
/// </summary>
public sealed partial class MatchLogPanel : HBoxContainer
{
    private const double CopiedFeedbackDuration = 1.5;
    private const string CopyTooltip = "Copy the match log to the clipboard";

    private readonly SettingsStore _settings;
    private readonly RichTextLabel _log = new() { BbcodeEnabled = true, ScrollFollowing = true, SizeFlagsVertical = SizeFlags.ExpandFill };
    private readonly VBoxContainer _body = new() { CustomMinimumSize = new Vector2(300, 0) };
    private readonly Button _toggle;
    private readonly Button _copy;

    public MatchLogPanel(SettingsStore settings)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _toggle = Widgets.Button(string.Empty, Toggle, 18);
        _copy = new Button { Icon = Icons.CopyIcon, TooltipText = CopyTooltip, FocusMode = FocusModeEnum.None };
        _copy.Pressed += Copy;

        var strip = new VBoxContainer();
        strip.AddChild(_toggle);

        var header = new HBoxContainer().WithGap(6);
        var title = Widgets.Label("Match log", 16, Palette.Muted);
        title.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        title.VerticalAlignment = VerticalAlignment.Center;
        header.AddChild(title);
        header.AddChild(_copy);

        _log.SelectionEnabled = true;
        var logPanel = Widgets.Panel(_log, Widgets.Box(Palette.Panel, Palette.PanelBorder, 1, margin: 8));
        logPanel.SizeFlagsVertical = SizeFlags.ExpandFill;

        _body.WithGap(8);
        _body.AddChild(header);
        _body.AddChild(logPanel);

        this.WithGap(4);
        AddChild(strip);
        AddChild(_body);
        ShowBody(_settings.Current.Interface.ShowMatchLog);
    }

    public void Append(string line) => _log.AppendText(line + "\n");

    public void AppendError(string text) => Append(LogFormat.Error(text));

    private void ShowBody(bool visible)
    {
        _body.Visible = visible;
        _toggle.Text = visible ? "»" : "«";
        _toggle.TooltipText = visible ? "Hide the match log" : "Show the match log";
    }

    private void Toggle()
    {
        var visible = !_body.Visible;
        ShowBody(visible);
        _settings.Update(s => s with { Interface = s.Interface with { ShowMatchLog = visible } });
        _settings.Flush();
    }

    private void Copy()
    {
        DisplayServer.ClipboardSet(_log.GetParsedText());
        _copy.Icon = Icons.CheckIcon;
        _copy.TooltipText = "Copied!";
        GetTree().CreateTimer(CopiedFeedbackDuration).Timeout += () =>
        {
            if (IsInstanceValid(_copy))
            {
                _copy.Icon = Icons.CopyIcon;
                _copy.TooltipText = CopyTooltip;
            }
        };
    }
}
