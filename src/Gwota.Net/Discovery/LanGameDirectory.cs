using System.Net;

namespace Gwota.Net.Discovery;

public sealed record LanGame(LanBeacon Beacon, IPAddress Address, bool IsCompatible)
{
    public string Endpoint => $"{Address}:{Beacon.Port}";
}

/// <summary>The games heard on the network recently; a game disappears when its beacon stops.</summary>
public sealed class LanGameDirectory
{
    private readonly Dictionary<string, (LanGame Game, long SeenAt)> _games = [];
    private readonly TimeProvider _time;
    private readonly string _gameVersion;

    public LanGameDirectory(string gameVersion, TimeProvider? time = null)
    {
        _gameVersion = gameVersion;
        _time = time ?? TimeProvider.System;
    }

    public static TimeSpan Expiry { get; } = TimeSpan.FromSeconds(3);

    /// <summary>Beacons from this instance id are ignored (the game's own broadcast).</summary>
    public string? OwnInstanceId { get; set; }

    public IReadOnlyList<LanGame> Games
    {
        get
        {
            Prune();
            return [.. _games.Values.Select(e => e.Game).OrderBy(g => g.Beacon.RoomName, StringComparer.OrdinalIgnoreCase).ThenBy(g => g.Beacon.InstanceId, StringComparer.Ordinal)];
        }
    }

    /// <summary>Records a beacon; returns true when the list of games changed.</summary>
    public bool Observe(LanBeacon beacon, IPAddress address)
    {
        ArgumentNullException.ThrowIfNull(beacon);
        ArgumentNullException.ThrowIfNull(address);
        if (beacon.InstanceId == OwnInstanceId)
        {
            return false;
        }

        var now = _time.GetTimestamp();
        var compatible = beacon.Protocol == NetProtocol.Version && beacon.GameVersion == _gameVersion;
        if (_games.TryGetValue(beacon.InstanceId, out var known))
        {
            // The same game is often heard twice on one machine; a LAN address beats the loopback one.
            var keepAddress = IPAddress.IsLoopback(address) && !IPAddress.IsLoopback(known.Game.Address);
            var updated = known.Game with { Beacon = beacon, Address = keepAddress ? known.Game.Address : address, IsCompatible = compatible };
            _games[beacon.InstanceId] = (updated, now);
            return updated != known.Game;
        }

        _games[beacon.InstanceId] = (new LanGame(beacon, address, compatible), now);
        return true;
    }

    /// <summary>Forgets every game; games still announcing come back with their next beacon.</summary>
    public void Clear() => _games.Clear();

    /// <summary>Drops expired games; returns true when any was removed.</summary>
    public bool Prune()
    {
        var expired = _games.Where(e => _time.GetElapsedTime(e.Value.SeenAt) > Expiry).Select(e => e.Key).ToList();
        foreach (var id in expired)
        {
            _games.Remove(id);
        }

        return expired.Count > 0;
    }
}