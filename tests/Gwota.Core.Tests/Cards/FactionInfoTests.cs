namespace Gwota.Core.Tests.Cards;

public class FactionInfoTests
{
    [Theory]
    [InlineData(Faction.Realms, "Realms of Men", "Realms")]
    [InlineData(Faction.Hells, "The Seven Hells", "Hells")]
    [InlineData(Faction.Neutral, "Neutral", "Neutral")]
    public void Factions_have_display_names(Faction faction, string name, string shortName)
    {
        FactionInfo.Name(faction).ShouldBe(name);
        FactionInfo.ShortName(faction).ShouldBe(shortName);
    }
}
