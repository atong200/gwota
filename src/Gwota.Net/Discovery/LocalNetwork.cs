using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace Gwota.Net.Discovery;

/// <summary>The machine's IPv4 addresses, to show the host's address and aim broadcasts.</summary>
public static class LocalNetwork
{
    /// <summary>Addresses others can reach this machine at, those of adapters with a default gateway first.</summary>
    public static IReadOnlyList<IPAddress> Addresses() => Ordered(Interfaces().Select(i => (i.Address, i.HasGateway)));

    /// <summary>Adapters without a gateway (virtual machines, WSL) come last: they rarely lead to another computer.</summary>
    internal static IReadOnlyList<IPAddress> Ordered(IEnumerable<(IPAddress Address, bool HasGateway)> addresses) =>
        [.. addresses.OrderBy(a => a.HasGateway ? 0 : 1).Select(a => a.Address).Distinct()];

    public static IReadOnlyList<IPAddress> BroadcastAddresses() =>
        [.. Interfaces().Select(i => BroadcastFor(i.Address, i.Mask)).Distinct()];

    public static IPAddress BroadcastFor(IPAddress address, IPAddress mask)
    {
        ArgumentNullException.ThrowIfNull(address);
        ArgumentNullException.ThrowIfNull(mask);
        var ip = address.GetAddressBytes();
        var bits = mask.GetAddressBytes();
        if (ip.Length != 4 || bits.Length != 4)
        {
            throw new ArgumentException("Only IPv4 addresses have a broadcast address.", nameof(address));
        }

        var result = new byte[4];
        for (var i = 0; i < 4; i++)
        {
            result[i] = (byte)(ip[i] | ~bits[i]);
        }

        return new IPAddress(result);
    }

    private static IEnumerable<(IPAddress Address, IPAddress Mask, bool HasGateway)> Interfaces()
    {
        NetworkInterface[] interfaces;
        try
        {
            interfaces = NetworkInterface.GetAllNetworkInterfaces();
        }
        catch (NetworkInformationException)
        {
            yield break;
        }

        foreach (var nic in interfaces)
        {
            if (nic.OperationalStatus != OperationalStatus.Up || nic.NetworkInterfaceType == NetworkInterfaceType.Loopback)
            {
                continue;
            }

            var properties = nic.GetIPProperties();
            var hasGateway = properties.GatewayAddresses.Any(g => g.Address.AddressFamily == AddressFamily.InterNetwork && !g.Address.Equals(IPAddress.Any));
            foreach (var unicast in properties.UnicastAddresses)
            {
                if (unicast.Address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(unicast.Address))
                {
                    yield return (unicast.Address, unicast.IPv4Mask, hasGateway);
                }
            }
        }
    }
}