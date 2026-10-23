using System.Text.Json;
using System.Text.Json.Serialization;

namespace Gwota.Net;

/// <summary>Turns messages into single JSON lines and back.</summary>
public static class NetCodec
{
    private static readonly JsonSerializerOptions Options = new()
    {
        Converters = { new JsonStringEnumConverter() },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static string Encode(NetMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        return JsonSerializer.Serialize(message, Options);
    }

    /// <summary>The decoded message, or null when the line is not a valid message.</summary>
    public static NetMessage? Decode(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<NetMessage>(line, Options);
        }
        catch (JsonException)
        {
            return null;
        }
        catch (NotSupportedException)
        {
            return null;
        }
    }
}