namespace Gwota.Net.Lan;

/// <summary>A player taking part in a LAN match: display name and chosen deck.</summary>
public sealed record LanPlayer(string Name, DeckDefinition Deck)
{
    public LanPlayer Normalized() => this with { Name = ProfileSettings.NormalizeUsername(Name) };
}

/// <summary>What both sides must share to play together.</summary>
public sealed record LanIdentity(string GameVersion, string Fingerprint)
{
    public static LanIdentity For(GameData data, string gameVersion) => new(gameVersion, ContentFingerprint.Compute(data));
}