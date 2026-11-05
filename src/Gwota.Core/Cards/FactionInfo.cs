namespace Gwota.Core.Cards;

/// <summary>Player-facing faction names.</summary>
public static class FactionInfo
{
    /// <summary>Factions a deck can belong to, in display order.</summary>
    public static IReadOnlyList<Faction> Playable { get; } = [Faction.Realms, Faction.Abyss, Faction.Horde];

    public static string Name(Faction faction) => faction switch
    {
        Faction.Realms => "United Realms",
        Faction.Abyss => "Abyssal Legion",
        Faction.Horde => "Wild Horde",
        _ => "Neutral",
    };

    /// <summary>Short form for tight labels.</summary>
    public static string ShortName(Faction faction) => faction switch
    {
        Faction.Realms => "Realms",
        Faction.Abyss => "Abyss",
        Faction.Horde => "Horde",
        _ => "Neutral",
    };

    /// <summary>A short lore description; empty for Neutral.</summary>
    public static string Flavor(Faction faction) => faction switch
    {
        Faction.Realms => "An alliance of Stonehall and its Bronze Legion, the Claddish Navy, the kingdom of Slom and frozen Icewrack, standing together against the dark.",
        Faction.Abyss => "Demons out of Foulfell, the Wraith King's undead host and the Underlord's armies of the Abyss, marching behind rival warlords to claim the world above.",
        Faction.Horde => "The Red Mist Army, the Ursine clans, the Oglodi of Druud and the troll warbands: a horde bound by nothing but the strength of whoever leads it.",
        _ => string.Empty,
    };
}
