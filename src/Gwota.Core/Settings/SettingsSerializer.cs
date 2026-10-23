using System.Text.Json;
using System.Text.Json.Serialization;

namespace Gwota.Core.Settings;

/// <summary>
/// Reads and writes <see cref="GameSettings"/> as JSON. Reading never fails: missing values use defaults, unknown ones
/// are ignored, out-of-range ones are clamped and unreadable content yields <see cref="GameSettings.Default"/>.
/// </summary>
public static class SettingsSerializer
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public static string Serialize(GameSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return JsonSerializer.Serialize(settings.Normalized(), Options);
    }

    public static GameSettings Deserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return GameSettings.Default;
        }

        try
        {
            return JsonSerializer.Deserialize<GameSettings>(json, Options)?.Normalized() ?? GameSettings.Default;
        }
        catch (JsonException)
        {
            return GameSettings.Default;
        }
    }
}
