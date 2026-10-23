using Godot;
using Gwota.Core.Settings;
using Gwota.Game.Infrastructure;

namespace Gwota.Game.Ui;

/// <summary>
/// The Options screen, shared by the main menu and the in-match pause menu. One tab per settings section; changes
/// apply immediately and are saved when a slider is released, a field is confirmed and when the screen closes.
/// </summary>
public sealed partial class OptionsMenu : Modal
{
    private readonly SettingsStore _settings;
    private readonly Action _onBack;
    private readonly LineEdit _username = new();

    public OptionsMenu(SettingsStore settings, Action onBack)
        : base(560)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _onBack = onBack ?? throw new ArgumentNullException(nameof(onBack));

        var tabs = new TabContainer { CustomMinimumSize = new Vector2(0, 240) };
        tabs.AddChild(BuildSoundTab());
        tabs.AddChild(BuildGameplayTab());
        tabs.AddChild(BuildProfileTab());

        Content.AddChild(Title("Options", 30));
        Content.AddChild(tabs);
        Content.AddChild(MenuButton("Back", Cancel));
    }

    public override void _ExitTree()
    {
        CommitUsername();
        _settings.Flush();
    }

    protected override void Cancel()
    {
        CommitUsername();
        _settings.Flush();
        _onBack();
    }

    /// <summary>The name shown in matches (banners, log). Saved when confirmed, when the field loses focus and on close.</summary>
    private VBoxContainer BuildProfileTab()
    {
        _username.Text = _settings.Current.Profile.Username;
        _username.MaxLength = ProfileSettings.MaxUsernameLength;
        _username.PlaceholderText = ProfileSettings.DefaultUsername;
        _username.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _username.TooltipText = $"Up to {ProfileSettings.MaxUsernameLength} characters. Applies from the next match.";
        _username.TextSubmitted += _ =>
        {
            CommitUsername();
            _settings.Flush();
            _username.ReleaseFocus();
        };
        _username.FocusExited += () =>
        {
            CommitUsername();
            _settings.Flush();
        };

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 12);
        var label = Widgets.Label("Username", 18);
        label.CustomMinimumSize = new Vector2(140, 0);
        label.VerticalAlignment = VerticalAlignment.Center;
        row.AddChild(label);
        row.AddChild(_username);

        var tab = new VBoxContainer { Name = "Profile" };
        tab.AddThemeConstantOverride("separation", 12);
        tab.AddChild(row);
        return tab;
    }

    private void CommitUsername()
    {
        _settings.Update(s => s with { Profile = s.Profile with { Username = _username.Text } });
        _username.Text = _settings.Current.Profile.Username;
    }

    private VBoxContainer BuildSoundTab()
    {
        var volume = _settings.Current.Audio.MasterVolume;
        var percent = Widgets.Label(Percent(volume), 18, Palette.Text, HorizontalAlignment.Right);
        percent.CustomMinimumSize = new Vector2(56, 0);

        var slider = new HSlider
        {
            MinValue = 0,
            MaxValue = 1,
            Step = 0.01,
            Value = volume,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
            TooltipText = "Volume of every sound in the game",
        };
        slider.ValueChanged += value =>
        {
            percent.Text = Percent(value);
            _settings.Update(s => s with { Audio = s.Audio with { MasterVolume = value } });
        };
        slider.DragEnded += _ => _settings.Flush();

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 12);
        var label = Widgets.Label("Master volume", 18);
        label.CustomMinimumSize = new Vector2(140, 0);
        row.AddChild(label);
        row.AddChild(slider);
        row.AddChild(percent);

        var tab = new VBoxContainer { Name = "Sound" };
        tab.AddThemeConstantOverride("separation", 12);
        tab.AddChild(row);
        return tab;
    }

    /// <summary>Card motion speed: Normal, Fast (half the duration) or Off (cards jump instantly).</summary>
    private VBoxContainer BuildGameplayTab()
    {
        var speeds = Enum.GetValues<AnimationSpeed>();
        var picker = new OptionButton
        {
            FocusMode = FocusModeEnum.None,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            TooltipText = "How fast cards move between the deck, hand, board and discard pile",
        };
        foreach (var speed in speeds)
        {
            picker.AddItem(speed.ToString());
        }

        picker.Selected = Array.IndexOf(speeds, _settings.Current.Gameplay.AnimationSpeed);
        picker.ItemSelected += index =>
        {
            _settings.Update(s => s with { Gameplay = s.Gameplay with { AnimationSpeed = speeds[index] } });
            _settings.Flush();
        };

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 12);
        var label = Widgets.Label("Animations", 18);
        label.CustomMinimumSize = new Vector2(140, 0);
        label.VerticalAlignment = VerticalAlignment.Center;
        row.AddChild(label);
        row.AddChild(picker);

        var tab = new VBoxContainer { Name = "Gameplay" };
        tab.AddThemeConstantOverride("separation", 12);
        tab.AddChild(row);
        return tab;
    }

    private static string Percent(double value) => $"{Math.Round(value * 100)}%";
}
