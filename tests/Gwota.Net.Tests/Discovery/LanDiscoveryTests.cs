using System.Net;
using System.Net.Sockets;
using Gwota.Net.Discovery;

namespace Gwota.Net.Tests.Discovery;

public class LanDiscoveryTests
{
    [Fact]
    public async Task A_beacon_is_heard_on_this_machine()
    {
        var port = FreeUdpPort();
        var directory = new LanGameDirectory("1.0");
        using var listener = new LanDiscoveryListener(directory, port, IPAddress.Loopback);
        using var broadcaster = new LanBroadcaster(new LanBeacon("host-1", "Room", "Alice", 7777, NetProtocol.Version, "1.0"), port);
        listener.IsListening.ShouldBeTrue();

        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (directory.Games.Count == 0 && DateTime.UtcNow < deadline)
        {
            broadcaster.Poll();
            listener.Poll();
            await Task.Delay(20, TestContext.Current.CancellationToken);
        }

        directory.Games.Single().Beacon.HostName.ShouldBe("Alice");
    }

    private static int FreeUdpPort()
    {
        using var probe = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        return ((IPEndPoint)probe.Client.LocalEndPoint!).Port;
    }
}