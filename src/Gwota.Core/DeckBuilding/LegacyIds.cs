namespace Gwota.Core.DeckBuilding;

/// <summary>Id renames applied to older deck files, by the file version they apply to.</summary>
internal static class LegacyIds
{
    /// <summary>Faction renames for files older than version 3 (version 1 files also go through these).</summary>
    public static readonly IReadOnlyDictionary<string, string> Factions = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["radiant"] = "realms",
        ["dire"] = "abyss",
        ["hells"] = "abyss",
    };

    /// <summary>Leader renames for files older than version 2.</summary>
    public static readonly IReadOnlyDictionary<string, string> Leaders = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["elder_titan"] = "legion_commander",
        ["zeus"] = "luna",
        ["lich"] = "underlord",
    };

    /// <summary>Card renames for files older than version 2.</summary>
    public static readonly IReadOnlyDictionary<string, string> Cards = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["earthshaker"] = "dragon_knight",
        ["mirana"] = "beastmaster",
        ["ursa"] = "marci",
        ["dazzle"] = "chen",
        ["tiny"] = "invoker",
        ["techies"] = "claddish_cannoneer",
        ["treant_protector"] = "bronze_legion_standard_bearer",
        ["axe"] = "terrorblade",
        ["abaddon"] = "wraith_king",
        ["necrophos"] = "visage",
        ["bane"] = "queen_of_pain",
        ["nyx_assassin"] = "shadow_demon",
        ["phantom_assassin"] = "clinkz",
        ["meepo"] = "hellbear_smasher",
        ["viper"] = "death_prophet",
        ["sand_king"] = "infernal_golem",
    };
}
