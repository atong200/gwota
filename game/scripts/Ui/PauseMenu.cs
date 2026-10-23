using Godot;
using Gwota.Game.Infrastructure;

namespace Gwota.Game.Ui;

/// <summary>
/// In-match menu opened with Escape or the gear button. The match screen pauses its timers while this is open.
/// Forfeit is offered only when a human is playing (not in AI-vs-AI demos) and asks for confirmation.
/// </summary>
public sealed partial class PauseMenu : Modal
{
    private readonly SettingsStore _settings;
    private readonly Action _onResume;

    public PauseMenu(SettingsStore settings, Action onResume, Action? onForfeit, Action onExitToMenu, Action onQuit)
        : base(360)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _onResume = onResume ?? throw new ArgumentNullException(nameof(onResume));

        Content.AddChild(Title("Paused", 30));
        Content.AddChild(MenuButton("Resume", Cancel));
        Content.AddChild(MenuButton("Options", OpenOptions));
        if (onForfeit is not null)
        {
            Content.AddChild(MenuButton("Forfeit", () => ConfirmDialog.Show(
                this, "Forfeit", "Concede the match? Your opponent wins and it counts as a defeat.", "Forfeit", onForfeit)));
        }

        Content.AddChild(MenuButton("Exit to main menu", onExitToMenu));
        Content.AddChild(MenuButton("Quit", onQuit));
    }

    protected override void Cancel() => _onResume();

    private void OpenOptions()
    {
        OptionsMenu? options = null;
        options = new OptionsMenu(_settings, () => options!.QueueFree());
        AddChild(options);
    }
}
