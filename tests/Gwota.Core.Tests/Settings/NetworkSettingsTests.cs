using Gwota.Core.Settings;

namespace Gwota.Core.Tests.Settings;

public class NetworkSettingsTests
{
    [Fact]
    public void Defaults_host_on_7777_with_the_default_room_name()
    {
        GameSettings.Default.Network.HostPort.ShouldBe(7777);
        GameSettings.Default.Network.RoomName.ShouldBeEmpty();
    }

    [Fact]
    public void Network_settings_survive_a_round_trip()
    {
        var settings = new GameSettings { Network = new NetworkSettings { HostPort = 7790, RoomName = "Friday night" } };

        SettingsSerializer.Deserialize(SettingsSerializer.Serialize(settings)).Network.ShouldBe(settings.Network);
    }

    [Theory]
    [InlineData(0, 7777)]
    [InlineData(80, 7777)]
    [InlineData(1023, 7777)]
    [InlineData(1024, 1024)]
    [InlineData(65535, 65535)]
    [InlineData(70000, 7777)]
    public void Out_of_range_ports_fall_back_to_the_default(int stored, int expected) =>
        SettingsSerializer.Deserialize($"{{ \"network\": {{ \"hostPort\": {stored} }} }}").Network.HostPort.ShouldBe(expected);

    [Theory]
    [InlineData("{ \"network\": null }")]
    [InlineData("{ \"audio\": { \"masterVolume\": 0.5 } }")]
    public void Missing_network_section_uses_defaults(string json) =>
        SettingsSerializer.Deserialize(json).Network.ShouldBe(new NetworkSettings());

    [Theory]
    [InlineData(null, "")]
    [InlineData("   ", "")]
    [InlineData("  Friday   night ", "Friday night")]
    [InlineData("[LAN] party", "LAN party")]
    [InlineData("A room name that is far too long", "A room name that is far")]
    public void Room_names_are_cleaned(string? input, string expected) =>
        NetworkSettings.NormalizeRoomName(input).ShouldBe(expected);

    [Theory]
    [InlineData("Friday night", "Tong", "Friday night")]
    [InlineData("", "Tong", "Tong's game")]
    [InlineData(null, "Tong", "Tong's game")]
    [InlineData("  ", "Sixteen chars xy", "Sixteen chars xy's game")]
    public void Empty_room_names_default_to_the_host_name(string? room, string host, string expected) =>
        NetworkSettings.RoomNameOrDefault(room, host).ShouldBe(expected);

    [Theory]
    [InlineData(1023, false)]
    [InlineData(1024, true)]
    [InlineData(7777, true)]
    [InlineData(65535, true)]
    [InlineData(65536, false)]
    public void Valid_ports_are_in_the_unprivileged_range(int port, bool valid) =>
        NetworkSettings.IsValidPort(port).ShouldBe(valid);
}
