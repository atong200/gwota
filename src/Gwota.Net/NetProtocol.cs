namespace Gwota.Net;

public static class NetProtocol
{
    /// <summary>Bumped whenever messages change in a way older builds cannot read.</summary>
    public const int Version = 2;

    public const int DefaultGamePort = 7777;

    public const int DiscoveryPort = 7778;
}