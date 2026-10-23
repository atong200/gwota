using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Gwota.Net.Discovery;

/// <summary>Announces a hosted game while it is polled.</summary>
public interface ILanAnnouncer : IDisposable
{
    void Poll();
}

/// <summary>Announces a hosted game once per <see cref="Interval"/> while it is polled.</summary>
public sealed class LanBroadcaster : ILanAnnouncer
{
    private readonly UdpClient _udp;
    private readonly byte[] _payload;
    private readonly int _port;
    private readonly TimeProvider _time;
    private readonly IReadOnlyList<IPAddress> _targets;
    private long? _lastSent;

    public LanBroadcaster(LanBeacon beacon, int port = NetProtocol.DiscoveryPort, TimeProvider? time = null)
    {
        ArgumentNullException.ThrowIfNull(beacon);
        _payload = Encoding.UTF8.GetBytes(beacon.Encode());
        _port = port;
        _time = time ?? TimeProvider.System;
        _udp = new UdpClient(AddressFamily.InterNetwork) { EnableBroadcast = true };
        _targets = [.. LocalNetwork.BroadcastAddresses(), IPAddress.Broadcast, IPAddress.Loopback];
    }

    public static TimeSpan Interval { get; } = TimeSpan.FromSeconds(1);

    public void Poll()
    {
        if (_lastSent is { } last && _time.GetElapsedTime(last) < Interval)
        {
            return;
        }

        _lastSent = _time.GetTimestamp();
        foreach (var target in _targets)
        {
            try
            {
                _udp.Send(_payload, _payload.Length, new IPEndPoint(target, _port));
            }
            catch (SocketException)
            {
                // An interface that cannot broadcast is skipped; the others still announce the game.
            }
        }
    }

    public void Dispose() => _udp.Dispose();
}

/// <summary>Keeps a <see cref="LanGameDirectory"/> up to date while it is polled.</summary>
public interface ILanScanner : IDisposable
{
    bool IsListening { get; }

    /// <summary>Reads waiting beacons; returns true when the list of games changed.</summary>
    bool Poll();
}

/// <summary>Listens for beacons and keeps <see cref="Directory"/> up to date while it is polled.</summary>
public sealed class LanDiscoveryListener : ILanScanner
{
    private readonly UdpClient? _udp;

    public LanDiscoveryListener(LanGameDirectory directory, int port = NetProtocol.DiscoveryPort, IPAddress? address = null)
    {
        Directory = directory ?? throw new ArgumentNullException(nameof(directory));
        try
        {
            var udp = new UdpClient(AddressFamily.InterNetwork);
            udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            udp.Client.Bind(new IPEndPoint(address ?? IPAddress.Any, port));
            _udp = udp;
        }
        catch (SocketException)
        {
            _udp = null;
        }
    }

    public LanGameDirectory Directory { get; }

    /// <summary>False when the discovery port could not be opened; games can still be joined by address.</summary>
    public bool IsListening => _udp is not null;

    /// <summary>Reads waiting beacons; returns true when the list of games changed.</summary>
    public bool Poll()
    {
        var changed = Directory.Prune();
        if (_udp is null)
        {
            return changed;
        }

        try
        {
            while (_udp.Available > 0)
            {
                var sender = new IPEndPoint(IPAddress.Any, 0);
                var data = _udp.Receive(ref sender);
                if (LanBeacon.Decode(Encoding.UTF8.GetString(data)) is { } beacon)
                {
                    changed |= Directory.Observe(beacon, sender.Address);
                }
            }
        }
        catch (SocketException)
        {
            // A broken datagram (e.g. ICMP port unreachable on Windows) is dropped.
        }

        return changed;
    }

    public void Dispose() => _udp?.Dispose();
}