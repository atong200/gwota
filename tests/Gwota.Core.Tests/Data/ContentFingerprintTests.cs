using Gwota.Core.Data;

namespace Gwota.Core.Tests.Data;

public class ContentFingerprintTests
{
    private static readonly GameData Shipped = GameDataLoader.Load(new FileSystemDataSource(RepoPaths.GameData));

    [Fact]
    public void Fingerprint_is_stable_across_loads() =>
        ContentFingerprint.Compute(GameDataLoader.Load(new FileSystemDataSource(RepoPaths.GameData))).ShouldBe(ContentFingerprint.Compute(Shipped));

    [Fact]
    public void Fingerprint_is_sixteen_hex_digits() =>
        ContentFingerprint.Compute(Shipped).ShouldMatch("^[0-9a-f]{16}$");

    [Fact]
    public void Fingerprint_ignores_dictionary_order()
    {
        var reversed = new GameData(
            Shipped.Cards.Reverse().ToDictionary(),
            Shipped.Leaders.Reverse().ToDictionary(),
            Shipped.Decks);

        ContentFingerprint.Compute(reversed).ShouldBe(ContentFingerprint.Compute(Shipped));
    }

    [Fact]
    public void A_strength_change_changes_the_fingerprint()
    {
        var cards = Shipped.Cards.ToDictionary();
        var first = cards.Keys.Order(StringComparer.Ordinal).First();
        cards[first] = cards[first] with { Strength = cards[first].Strength + 1 };

        ContentFingerprint.Compute(new GameData(cards, Shipped.Leaders, Shipped.Decks)).ShouldNotBe(ContentFingerprint.Compute(Shipped));
    }

    [Fact]
    public void Flavor_text_does_not_change_the_fingerprint()
    {
        var cards = Shipped.Cards.ToDictionary();
        var first = cards.Keys.Order(StringComparer.Ordinal).First();
        cards[first] = cards[first] with { Description = "Changed." };

        ContentFingerprint.Compute(new GameData(cards, Shipped.Leaders, Shipped.Decks)).ShouldBe(ContentFingerprint.Compute(Shipped));
    }
}