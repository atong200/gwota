using Gwota.Core.Data;
using Gwota.Core.Tests.Support;

namespace Gwota.Core.Tests.Data;

public class ArtPathsTests
{
    private static readonly GameData Data = new(
        new[]
        {
            TestCards.Unit(4, id: "sven", faction: Faction.Realms),
            TestCards.Unit(5, id: "doom", faction: Faction.Abyss),
            TestCards.Unit(6, id: "axe", faction: Faction.Horde),
            TestCards.Special(SpecialEffect.Weather, "eclipse"),
            TestCards.Hero(10, id: "roshan"),
            TestCards.Unit(2, id: "creep_2"),
        }.ToDictionary(c => c.Id),
        new Dictionary<string, LeaderDefinition>(),
        new Dictionary<string, DeckDefinition>());

    [Theory]
    [InlineData("sven", "cards/realms/sven")]
    [InlineData("doom", "cards/abyss/doom")]
    [InlineData("axe", "cards/horde/axe")]
    [InlineData("eclipse", "cards/neutral/eclipse")]
    public void Card_art_lives_in_its_faction_folder(string id, string expected) =>
        ArtPaths.Card(Data.Cards[id]).ShouldBe(expected);

    [Fact]
    public void Leader_art_lives_in_the_leaders_folder() =>
        ArtPaths.Leader("underlord").ShouldBe("leaders/underlord");

    [Fact]
    public void Faction_folders_match_the_card_data_folders() =>
        Enum.GetValues<Faction>().Select(ArtPaths.FactionFolder).ShouldBe(["neutral", "realms", "abyss", "horde"], ignoreOrder: true);

    [Fact]
    public void Art_in_the_right_faction_folder_is_fine() =>
        ArtPaths.FindMisplacedCardArt(Data, ["realms/sven.png", "abyss/doom.webp", "horde/axe.jpg", "neutral/eclipse.png"]).ShouldBeEmpty();

    [Fact]
    public void Files_that_are_not_images_are_ignored() =>
        ArtPaths.FindMisplacedCardArt(Data, ["README.md", "realms/.gitkeep", "realms/sven.png.import", "notes.txt"]).ShouldBeEmpty();

    [Theory]
    [InlineData("sven.png", "'cards/sven.png' must be in 'cards/realms/'.")]
    [InlineData("horde/sven.png", "'cards/horde/sven.png' must be in 'cards/realms/'.")]
    [InlineData("realms/old/sven.png", "'cards/realms/old/sven.png' must be in 'cards/realms/'.")]
    [InlineData("misc/eclipse.png", "'cards/misc/eclipse.png' must be in 'cards/neutral/'.")]
    public void Art_outside_its_card_faction_folder_is_reported(string file, string problem) =>
        ArtPaths.FindMisplacedCardArt(Data, [file]).ShouldBe([problem]);

    [Fact]
    public void Art_named_after_no_card_is_reported() =>
        ArtPaths.FindMisplacedCardArt(Data, ["realms/sven_old.png"]).ShouldBe(["'cards/realms/sven_old.png' is named after no card id."]);

    [Fact]
    public void Art_with_an_upper_case_extension_is_reported() =>
        ArtPaths.FindMisplacedCardArt(Data, ["realms/sven.PNG"])
            .ShouldBe(["'cards/realms/sven.PNG' needs a lower-case extension (.png, .webp, .jpg)."]);

    [Fact]
    public void Every_problem_is_listed_in_path_order() =>
        ArtPaths.FindMisplacedCardArt(Data, ["realms/zzz.png", "axe.png", "realms/sven.png"])
            .ShouldBe(["'cards/axe.png' must be in 'cards/horde/'.", "'cards/realms/zzz.png' is named after no card id."]);

    [Theory]
    [InlineData(1, "cards/realms/sven")]
    [InlineData(2, "cards/realms/sven_2")]
    [InlineData(3, "cards/realms/sven_3")]
    public void Card_variants_add_a_number_after_the_first_image(int variant, string expected) =>
        ArtPaths.CardVariant(Data.Cards["sven"], variant).ShouldBe(expected);

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    public void Card_variants_are_numbered_one_to_three(int variant) =>
        Should.Throw<ArgumentOutOfRangeException>(() => ArtPaths.CardVariant(Data.Cards["sven"], variant));

    [Theory]
    [InlineData(1, new[] { 1, 1, 1 })]
    [InlineData(2, new[] { 1, 2, 1 })]
    [InlineData(3, new[] { 1, 2, 3 })]
    [InlineData(0, new[] { 1, 1, 1 })]
    [InlineData(5, new[] { 1, 2, 3 })]
    public void Copies_take_the_images_in_turn(int available, int[] expected) =>
        Enumerable.Range(0, 3).Select(copy => ArtPaths.PickVariant(Data.Cards["sven"], copy, available)).ShouldBe(expected);

    [Fact]
    public void A_hero_always_shows_its_first_image() =>
        Enumerable.Range(0, 3).Select(copy => ArtPaths.PickVariant(Data.Cards["roshan"], copy, 3)).ShouldBe([1, 1, 1]);

    [Fact]
    public void Variants_in_sequence_are_fine() =>
        ArtPaths.FindMisplacedCardArt(Data, ["realms/sven.png", "realms/sven_2.webp", "realms/sven_3.png", "abyss/doom.png", "abyss/doom_2.png"])
            .ShouldBeEmpty();

    [Theory]
    [InlineData(new[] { "realms/sven_2.png" }, "'cards/realms/sven_2.png' needs 'cards/realms/sven' first.")]
    [InlineData(new[] { "realms/sven.png", "realms/sven_3.png" }, "'cards/realms/sven_3.png' needs 'cards/realms/sven_2' first.")]
    [InlineData(new[] { "neutral/roshan.png", "neutral/roshan_2.png" }, "'cards/neutral/roshan_2.png' is a second image of the hero 'roshan'; heroes have a single image.")]
    [InlineData(new[] { "horde/sven_2.png" }, "'cards/horde/sven_2.png' must be in 'cards/realms/'.")]
    [InlineData(new[] { "realms/sven_4.png" }, "'cards/realms/sven_4.png' is named after no card id.")]
    [InlineData(new[] { "realms/sven_02.png" }, "'cards/realms/sven_02.png' is named after no card id.")]
    public void Broken_variants_are_reported(string[] files, string problem) =>
        ArtPaths.FindMisplacedCardArt(Data, files).ShouldBe([problem]);

    [Fact]
    public void An_exact_card_id_wins_over_a_variant_suffix() =>
        ArtPaths.FindMisplacedCardArt(Data, ["neutral/creep_2.png"]).ShouldBeEmpty();
}
