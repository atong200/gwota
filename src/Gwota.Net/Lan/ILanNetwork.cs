using Gwota.Net.Discovery;
using Gwota.Net.Transport;

namespace Gwota.Net.Lan;

/// <summary>The sockets the lobby needs, so it can run over TCP/UDP in the game and in memory in tests.</summary>
public interface ILanNetwork
{
    /// <summary>Opens the port guests connect to; throws <see cref="System.Net.Sockets.SocketException"/> when it is taken.</summary>
    IConnectionListener Listen(int port);

    Task<IConnection> ConnectAsync(LanAddress address, CancellationToken cancellation);

    /// <summary>Starts announcing a hosted game; poll the result every frame.</summary>
    ILanAnnouncer Announce(LanBeacon beacon);

    /// <summary>Starts listening for announced games into <paramref name="directory"/>.</summary>
    ILanScanner Scan(LanGameDirectory directory);
}

/// <summary>TCP for the game connection, UDP broadcast for announcements.</summary>
public sealed class TcpLanNetwork : ILanNetwork
{
    public static TimeSpan ConnectTimeout { get; } = TimeSpan.FromSeconds(6);

    public IConnectionListener Listen(int port) => new TcpConnectionListener(port);

    public async Task<IConnection> ConnectAsync(LanAddress address, CancellationToken cancellation)
    {
        ArgumentNullException.ThrowIfNull(address);
        return await TcpConnection.ConnectAsync(address.Host, address.Port, ConnectTimeout, cancellation).ConfigureAwait(false);
    }

    public ILanAnnouncer Announce(LanBeacon beacon) => new LanBroadcaster(beacon);

    public ILanScanner Scan(LanGameDirectory directory) => new LanDiscoveryListener(directory);
}
