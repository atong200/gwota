using System.Net;
using Gwota.Net.Discovery;

namespace Gwota.Net.Tests.Discovery;

public class LocalNetworkTests
{
    [Theory]
    [InlineData("192.168.1.20", "255.255.255.0", "192.168.1.255")]
    [InlineData("10.1.2.3", "255.0.0.0", "10.255.255.255")]
    [InlineData("172.16.5.9", "255.255.240.0", "172.16.15.255")]
    public void The_broadcast_address_sets_every_host_bit(string address, string mask, string expected) =>
        LocalNetwork.BroadcastFor(IPAddress.Parse(address), IPAddress.Parse(mask)).ShouldBe(IPAddress.Parse(expected));

    [Fact]
    public void Ipv6_has_no_broadcast_address() =>
        Should.Throw<ArgumentException>(() => LocalNetwork.BroadcastFor(IPAddress.IPv6Loopback, IPAddress.Parse("255.255.255.0")));

    [Fact]
    public void Listed_addresses_are_ipv4_and_not_loopback() =>
        LocalNetwork.Addresses().ShouldAllBe(a => a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork && !IPAddress.IsLoopback(a));

    [Fact]
    public void Addresses_with_a_gateway_come_first_without_duplicates()
    {
        var wsl = IPAddress.Parse("172.27.32.1");
        var lan = IPAddress.Parse("192.168.1.36");
        var wifi = IPAddress.Parse("10.0.0.5");

        LocalNetwork.Ordered([(wsl, false), (lan, true), (wsl, false), (wifi, true)]).ShouldBe([lan, wifi, wsl]);
    }
}
