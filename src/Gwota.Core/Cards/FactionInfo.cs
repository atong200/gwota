namespace Gwota.Core.Cards;

/// <summary>Player-facing faction names.</summary>
public static class FactionInfo
{
    public static string Name(Faction faction) => faction switch
    {
        Faction.Realms => "Realms of Men",
        Faction.Hells => "The Seven Hells",
        _ => "Neutral",
    };

    /// <summary>Short form for tight labels.</summary>
    public static string ShortName(Faction faction) => faction switch
    {
        Faction.Realms => "Realms",
        Faction.Hells => "Hells",
        _ => "Neutral",
    };
}
