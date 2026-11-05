using Godot;
using Gwota.Game.Infrastructure;

namespace Gwota.Game.Ui;

/// <summary>
/// In-match menu opened with Escape or the gear button. The match screen pauses its timers until it is
/// <see cref="Modal.Closed"/>.
/// Forfeit is offered only when a human is playing (not in AI-vs-AI demos) and asks for confirmation.
/// </summary>
public sealed partial class PauseMenu : Modal
{
    private readonly SettingsStore _settings;

    public PauseMenu(SettingsStore settings, Action? onForfeit, Action onExitToMenu, Action onQuit)
        : base(360)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));

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


    private void OpenOptions() => AddChild(new OptionsMenu(_settings));

}
