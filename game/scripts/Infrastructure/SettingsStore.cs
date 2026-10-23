using Godot;
using Gwota.Core.Settings;
using FileAccess = Godot.FileAccess;

namespace Gwota.Game.Infrastructure;

/// <summary>
/// Loads, applies (audio buses) and saves the player's <see cref="GameSettings"/> in <c>user://settings.json</c>.
/// Changes apply at once; <see cref="Flush"/> persists them (e.g. once when a slider is released).
/// </summary>
public sealed class SettingsStore
{
    private const string FilePath = "user://settings.json";
    private const string MasterBus = "Master";

    private bool _dirty;

    private SettingsStore(GameSettings settings)
    {
        Current = settings;
        Apply();
    }

    public GameSettings Current { get; private set; }

    public static SettingsStore Load()
    {
        string? json = null;
        if (FileAccess.FileExists(FilePath))
        {
            using var file = FileAccess.Open(FilePath, FileAccess.ModeFlags.Read);
            json = file?.GetAsText();
        }

        return new SettingsStore(SettingsSerializer.Deserialize(json));
    }

    /// <summary>Applies a change immediately; call <see cref="Flush"/> to persist it.</summary>
    public void Update(Func<GameSettings, GameSettings> change)
    {
        ArgumentNullException.ThrowIfNull(change);

        var updated = change(Current).Normalized();
        if (updated == Current)
        {
            return;
        }

        Current = updated;
        _dirty = true;
        Apply();
    }

    /// <summary>Writes pending changes to disk. Failures are logged, never thrown: losing a preference must not crash the game.</summary>
    public void Flush()
    {
        if (!_dirty)
        {
            return;
        }

        using var file = FileAccess.Open(FilePath, FileAccess.ModeFlags.Write);
        if (file is null)
        {
            GD.PushWarning($"Cannot save settings to {FilePath}: {FileAccess.GetOpenError()}");
            return;
        }

        file.StoreString(SettingsSerializer.Serialize(Current));
        _dirty = false;
    }

    private void Apply()
    {
        var bus = AudioServer.GetBusIndex(MasterBus);
        var volume = (float)Current.Audio.MasterVolume;
        AudioServer.SetBusMute(bus, volume <= 0);
        AudioServer.SetBusVolumeDb(bus, Mathf.LinearToDb(Mathf.Max(volume, 0.0001f)));
    }
}
