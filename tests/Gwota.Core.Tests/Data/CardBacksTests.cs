using Gwota.Core.Data;

namespace Gwota.Core.Tests.Data;

public class CardBacksTests
{
    [Theory]
    [InlineData(null, CardBacks.DefaultId)]
    [InlineData("", CardBacks.DefaultId)]
    [InlineData("   ", CardBacks.DefaultId)]
    [InlineData(" red_mist ", "red_mist")]
    [InlineData("aegis", "aegis")]
    public void Normalize_trims_and_defaults_blank_ids(string? id, string expected) =>
        CardBacks.Normalize(id).ShouldBe(expected);

    [Theory]
    [InlineData("aegis_seal", "aegis")]
    [InlineData(" Gilded_Delta ", "gilded")]
    [InlineData("crimson_filigree", "crimson")]
    public void Normalize_maps_renamed_backs_to_their_current_id(string id, string expected) =>
        CardBacks.Normalize(id).ShouldBe(expected);
}
