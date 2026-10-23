namespace Gwota.Core.Settings;

/// <summary>
/// Player preferences persisted between launches. Grouped by Options tab so new tabs add a section
/// without touching existing ones. Instances are immutable: use <c>with</c> to change a value.
/// </summary>
public sealed record GameSettings
{
    public static GameSettings Default { get; } = new();

    public AudioSettings Audio { get; init; } = new();

    public InterfaceSettings Interface { get; init; } = new();

    public ProfileSettings Profile { get; init; } = new();

    public GameplaySettings Gameplay { get; init; } = new();

    public NetworkSettings Network { get; init; } = new();

    /// <summary>Returns a copy with every value brought back into its valid range (and missing sections restored).</summary>
    public GameSettings Normalized() => this with
    {
        Audio = (Audio ?? new AudioSettings()).Normalized(),
        Interface = Interface ?? new InterfaceSettings(),
        Profile = (Profile ?? new ProfileSettings()).Normalized(),
        Gameplay = (Gameplay ?? new GameplaySettings()).Normalized(),
        Network = (Network ?? new NetworkSettings()).Normalized(),
    };
}

public sealed record NetworkSettings
{
    public const int DefaultHostPort = 7777;
    public const int MinHostPort = 1024;
    public const int MaxHostPort = 65535;
    public const int MaxRoomNameLength = 24;

    /// <summary>TCP port a hosted game listens on.</summary>
    public int HostPort { get; init; } = DefaultHostPort;

    /// <summary>Name of the hosted room; empty means the default based on the player's name.</summary>
    public string RoomName { get; init; } = string.Empty;

    public NetworkSettings Normalized() => this with
    {
        HostPort = IsValidPort(HostPort) ? HostPort : DefaultHostPort,
        RoomName = NormalizeRoomName(RoomName),
    };

    public static bool IsValidPort(int port) => port is >= MinHostPort and <= MaxHostPort;

    /// <summary>A cleaned room name (see <see cref="ProfileSettings.NormalizeUsername"/>), empty when nothing is left.</summary>
    public static string NormalizeRoomName(string? roomName) => DisplayText.Clean(roomName, MaxRoomNameLength);

    /// <summary>The room name to announce: the cleaned name, or "<paramref name="hostName"/>'s game".</summary>
    public static string RoomNameOrDefault(string? roomName, string hostName)
    {
        var cleaned = NormalizeRoomName(roomName);
        return cleaned.Length > 0 ? cleaned : NormalizeRoomName($"{hostName}'s game");
    }
}

public enum AnimationSpeed
{
    Normal,
    Fast,

    /// <summary>Cards jump straight to their new place.</summary>
    Off,
}

public sealed record GameplaySettings
{
    /// <summary>How fast cards move between hand, board, piles and discard.</summary>
    public AnimationSpeed AnimationSpeed { get; init; } = AnimationSpeed.Normal;

    public GameplaySettings Normalized() => this with
    {
        AnimationSpeed = Enum.IsDefined(AnimationSpeed) ? AnimationSpeed : AnimationSpeed.Normal,
    };

    /// <summary>Multiplier applied to every animation duration: 1 at normal speed, 0 when animations are off.</summary>
    public double DurationScale => AnimationSpeed switch
    {
        AnimationSpeed.Fast => 0.5,
        AnimationSpeed.Off => 0,
        _ => 1,
    };
}

public sealed record AudioSettings
{
    public const double DefaultMasterVolume = 0.8;

    /// <summary>Linear master volume, from 0 (muted) to 1 (full).</summary>
    public double MasterVolume { get; init; } = DefaultMasterVolume;

    public AudioSettings Normalized() => this with
    {
        MasterVolume = double.IsFinite(MasterVolume) ? Math.Clamp(MasterVolume, 0, 1) : DefaultMasterVolume,
    };
}

public sealed record InterfaceSettings
{
    /// <summary>Whether the match log panel is shown on the board.</summary>
    public bool ShowMatchLog { get; init; } = true;
}

public sealed record ProfileSettings
{
    public const string DefaultUsername = "Player";
    public const int MaxUsernameLength = 16;

    /// <summary>Name shown for the local player in matches and the match log.</summary>
    public string Username { get; init; } = DefaultUsername;

    public ProfileSettings Normalized() => this with { Username = NormalizeUsername(Username) };

    /// <summary>
    /// Makes any input a safe display name: control characters and square brackets (markup in the match log) are
    /// removed, whitespace is collapsed, the length is capped and an empty result falls back to the default.
    /// </summary>
    public static string NormalizeUsername(string? username)
    {
        var name = DisplayText.Clean(username, MaxUsernameLength);
        return name.Length == 0 ? DefaultUsername : name;
    }
}

internal static class DisplayText
{
    /// <summary>Drops control characters and square brackets, collapses whitespace and caps the length.</summary>
    public static string Clean(string? text, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var kept = text
            .Select(c => char.IsWhiteSpace(c) ? ' ' : c)
            .Where(c => !char.IsControl(c) && c is not '[' and not ']')
            .ToArray();
        var words = new string(kept).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        var cleaned = string.Join(' ', words);
        return cleaned.Length > maxLength ? cleaned[..maxLength].TrimEnd() : cleaned;
    }
}
