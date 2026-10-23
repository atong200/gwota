using System.Text.Json;
using System.Text.Json.Serialization;

namespace Gwota.Net.Discovery;

/// <summary>What a hosting game announces on the local network: the room, who hosts it and where to connect.</summary>
public sealed record LanBeacon(string InstanceId, string RoomName, string HostName, int Port, int Protocol, string GameVersion)
{
    private const string Tag = "gwota";

    private static readonly JsonSerializerOptions Options = new() { Converters = { new JsonStringEnumConverter() } };

    public string Encode() => JsonSerializer.Serialize(new Envelope(Tag, this), Options);

    /// <summary>The beacon in <paramref name="text"/>, or null when it is not a well-formed Gwota beacon.</summary>
    public static LanBeacon? Decode(string text)
    {
        try
        {
            var envelope = JsonSerializer.Deserialize<Envelope>(text, Options);
            var beacon = envelope?.Game == Tag ? envelope.Beacon : null;
            return beacon is { InstanceId.Length: > 0, RoomName: not null, HostName: not null, GameVersion: not null, Port: > 0 and <= 65535 }
                ? beacon
                : null;
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

    private sealed record Envelope(string Game, LanBeacon Beacon);
}