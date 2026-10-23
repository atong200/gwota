using Gwota.Net.Discovery;

namespace Gwota.Net.Tests.Discovery;

public class LanBeaconTests
{
    private static readonly LanBeacon Beacon = new("id-1", "Friday night", "Alice", 7777, NetProtocol.Version, "0.7.0");

    [Fact]
    public void A_beacon_survives_a_round_trip() => LanBeacon.Decode(Beacon.Encode()).ShouldBe(Beacon);

    [Theory]
    [InlineData("")]
    [InlineData("garbage")]
    [InlineData("{\"Game\":\"other\",\"Beacon\":{\"InstanceId\":\"x\",\"HostName\":\"a\",\"RoomName\":\"r\",\"Port\":7777,\"Protocol\":1,\"GameVersion\":\"v\"}}")]
    [InlineData("{\"Game\":\"gwota\",\"Beacon\":{\"InstanceId\":\"\",\"HostName\":\"a\",\"RoomName\":\"r\",\"Port\":7777,\"Protocol\":1,\"GameVersion\":\"v\"}}")]
    [InlineData("{\"Game\":\"gwota\",\"Beacon\":{\"InstanceId\":\"x\",\"HostName\":\"a\",\"RoomName\":\"r\",\"Port\":0,\"Protocol\":1,\"GameVersion\":\"v\"}}")]
    [InlineData("{\"Game\":\"gwota\",\"Beacon\":{\"InstanceId\":\"x\",\"RoomName\":\"r\",\"Port\":7777,\"Protocol\":1,\"GameVersion\":\"v\"}}")]
    [InlineData("{\"Game\":\"gwota\",\"Beacon\":{\"InstanceId\":\"x\",\"HostName\":\"a\",\"Port\":7777,\"Protocol\":2,\"GameVersion\":\"v\"}}")]
    public void Foreign_or_broken_beacons_are_ignored(string text) => LanBeacon.Decode(text).ShouldBeNull();

    [Fact]
    public void A_beacon_carries_no_deck_details() =>
        Beacon.Encode().ShouldSatisfyAllConditions(
            json => json.ShouldNotContain("Deck"),
            json => json.ShouldNotContain("Faction"));
}