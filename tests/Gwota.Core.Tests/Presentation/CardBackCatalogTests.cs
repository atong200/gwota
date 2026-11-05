using Gwota.Core.Data;
using Gwota.Core.Presentation;

namespace Gwota.Core.Tests.Presentation;

public class CardBackCatalogTests
{
    [Fact]
    public void The_default_back_comes_first_even_without_an_image()
    {
        var catalog = new CardBackCatalog(["red_mist.png", "aegis_seal.png"]);

        catalog.Entries.Select(e => e.Id).ShouldBe([CardBacks.DefaultId, "aegis_seal", "red_mist"]);
        catalog.Default.ShouldBe(new CardBackEntry(CardBacks.DefaultId, "Default", null));
    }

    [Fact]
    public void A_default_image_replaces_the_placeholder()
    {
        var catalog = new CardBackCatalog(["default.webp", "zeta.png"]);

        catalog.Entries.Count.ShouldBe(2);
        catalog.Default.File.ShouldBe("default.webp");
    }

    [Fact]
    public void Non_images_folders_and_import_metadata_are_ignored()
    {
        var catalog = new CardBackCatalog(["README.md", "sub/", ".hidden.png", "", "notes.txt", "dire_moon.png.import", "arcane.jpg.remap"]);

        catalog.Entries.Select(e => e.File).ShouldBe([null, "arcane.jpg", "dire_moon.png"]);
    }

    [Fact]
    public void The_same_back_in_several_formats_keeps_the_png()
    {
        var catalog = new CardBackCatalog(["sun.jpg", "sun.webp", "sun.png", "sun.png.import"]);

        catalog.Entries.Single(e => e.Id == "sun").File.ShouldBe("sun.png");
        catalog.Entries.Count.ShouldBe(2);
    }

    [Fact]
    public void Backs_are_ordered_by_display_name()
    {
        var catalog = new CardBackCatalog(["obsidian_crown.png", "Bronze_Legion.png", "arcane_rune.png"]);

        catalog.Entries.Select(e => e.Name).ShouldBe(["Default", "Arcane Rune", "Bronze Legion", "Obsidian Crown"]);
    }

    [Theory]
    [InlineData("aegis_seal", "Aegis Seal")]
    [InlineData("red-mist", "Red Mist")]
    [InlineData("dire__moon", "Dire Moon")]
    [InlineData("crown", "Crown")]
    [InlineData("v2_back", "V2 Back")]
    public void Display_names_come_from_the_id(string id, string name) =>
        CardBackCatalog.DisplayName(id).ShouldBe(name);

    [Theory]
    [InlineData("aegis_seal", "aegis_seal")]
    [InlineData(" AEGIS_SEAL ", "aegis_seal")]
    [InlineData("missing", "default")]
    [InlineData("", "default")]
    [InlineData(null, "default")]
    public void Unknown_ids_resolve_to_the_default_back(string? id, string expected) =>
        new CardBackCatalog(["aegis_seal.png"]).Resolve(id).Id.ShouldBe(expected);

    [Fact]
    public void Contains_only_knows_listed_backs()
    {
        var catalog = new CardBackCatalog(["aegis_seal.png"]);

        catalog.Contains("aegis_seal").ShouldBeTrue();
        catalog.Contains(CardBacks.DefaultId).ShouldBeTrue();
        catalog.Contains("red_mist").ShouldBeFalse();
        catalog.Contains(null).ShouldBeFalse();
    }
}
