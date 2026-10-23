using Gwota.Core.Data;

namespace Gwota.Core.Tests.DeckBuilding;

/// <summary>A small, fully known card pool for deck-building tests.</summary>
internal static class DeckBuildingData
{
    public static GameData Create(int realmsUnits = 3, int neutralUnits = 2)
    {
        var cards = new List<CardDefinition>();
        for (var i = 1; i <= realmsUnits; i++)
        {
            cards.Add(TestCards.Unit(i, RowType.Melee, $"realms_{i}", Faction.Realms));
        }

        for (var i = 1; i <= neutralUnits; i++)
        {
            cards.Add(TestCards.Unit(i, RowType.Ranged, $"neutral_{i}"));
        }

        cards.Add(TestCards.Unit(10, RowType.Siege, "realms_hero", Faction.Realms, hero: true));
        cards.Add(TestCards.Unit(4, RowType.Melee, "hells_1", Faction.Hells));
        cards.Add(TestCards.Agile(5, "neutral_agile", RowType.Melee, RowType.Siege));
        cards.Add(TestCards.Horn(RowType.Melee));
        cards.Add(TestCards.Scorch());

        var leaders = new[]
        {
            TestCards.Leader(LeaderAbility.ClearWeather) with { Id = "legion_commander", Name = "Legion Commander" },
            TestCards.Leader(LeaderAbility.ClearWeather) with { Id = "abaddon", Name = "Abaddon" },
            TestCards.Leader(LeaderAbility.ClearWeather, faction: Faction.Hells) with { Id = "underlord", Name = "Underlord" },
        };

        var starter = new DeckDefinition
        {
            Id = "realms_starter",
            Name = "Realms Vanguard",
            Faction = Faction.Realms,
            LeaderId = "legion_commander",
            Cards = [new DeckEntry("realms_1", 2)],
        };

        return new GameData(
            cards.ToDictionary(c => c.Id),
            leaders.ToDictionary(l => l.Id),
            new Dictionary<string, DeckDefinition> { [starter.Id] = starter });
    }

    public static DeckDefinition EmptyDeck(Faction faction = Faction.Realms, string leader = "legion_commander") => new()
    {
        Id = "deck_1",
        Name = "Test deck",
        Faction = faction,
        LeaderId = leader,
        Cards = [],
    };
}
