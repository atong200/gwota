namespace Gwota.Net;

/// <summary>What both ends of a LAN game agree on: protocol version, ports, timings and limits.</summary>
public static class NetProtocol
{
    /// <summary>Bumped whenever messages change in a way older builds cannot read.</summary>
    public const int Version = 6;

    public const int DefaultGamePort = 7777;

    public const int DiscoveryPort = 7778;

    /// <summary>Longest line accepted from the other side, in UTF-8 bytes without the newline.</summary>
    public const int MaxLineLength = 64 * 1024;

    /// <summary>An empty line goes out when nothing else was sent for this long.</summary>
    public static TimeSpan Heartbeat { get; } = TimeSpan.FromSeconds(2);

    /// <summary>A connection closes when nothing at all arrived for this long.</summary>
    public static TimeSpan ReceiveTimeout { get; } = TimeSpan.FromSeconds(10);

    /// <summary>How long closing waits for lines already sent (such as a goodbye) to be written.</summary>
    public static TimeSpan CloseFlushTimeout { get; } = TimeSpan.FromSeconds(1);

    public static TimeSpan ConnectTimeout { get; } = TimeSpan.FromSeconds(6);

    /// <summary>How long a host waits for a new connection to introduce itself.</summary>
    public static TimeSpan HelloTimeout { get; } = TimeSpan.FromSeconds(5);

    /// <summary>A hosted game is announced once per interval.</summary>
    public static TimeSpan BeaconInterval { get; } = TimeSpan.FromSeconds(1);

    /// <summary>A game no longer announced for this long leaves the list.</summary>
    public static TimeSpan BeaconExpiry { get; } = TimeSpan.FromSeconds(3);
}
