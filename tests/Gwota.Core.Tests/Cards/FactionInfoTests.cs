namespace Gwota.Core.Tests.Cards;

public class FactionInfoTests
{
    [Theory]
    [InlineData(Faction.Realms, "United Realms", "Realms")]
    [InlineData(Faction.Abyss, "Abyssal Legion", "Abyss")]
    [InlineData(Faction.Horde, "Wild Horde", "Horde")]
    [InlineData(Faction.Neutral, "Neutral", "Neutral")]
    public void Factions_have_display_names(Faction faction, string name, string shortName)
    {
        FactionInfo.Name(faction).ShouldBe(name);
        FactionInfo.ShortName(faction).ShouldBe(shortName);
    }

    [Fact]
    public void Playable_factions_exclude_neutral() =>
        FactionInfo.Playable.ShouldBe([Faction.Realms, Faction.Abyss, Faction.Horde]);

    [Fact]
    public void Playable_factions_have_flavor_text_and_neutral_has_none()
    {
        FactionInfo.Playable.ShouldAllBe(f => !string.IsNullOrWhiteSpace(FactionInfo.Flavor(f)));
        FactionInfo.Flavor(Faction.Neutral).ShouldBeEmpty();
    }

    [Fact]
    public void Playable_factions_have_a_perk() =>
        FactionInfo.Playable.ShouldAllBe(f => FactionPerks.Describe(f) != "No perk.");
}
