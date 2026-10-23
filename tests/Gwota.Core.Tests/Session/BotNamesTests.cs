using Gwota.Core.Session;

namespace Gwota.Core.Tests.Session;

public class BotNamesTests
{
    [Fact]
    public void Names_are_prefixed_like_old_school_bots()
    {
        BotNames.Default.All.ShouldNotBeEmpty();
        BotNames.Default.All.ShouldAllBe(n => n.StartsWith(BotNames.Prefix, StringComparison.Ordinal));
        BotNames.Default.All.ShouldContain("BOT Gabe");
    }

    [Fact]
    public void Pool_names_are_unique() =>
        BotNames.Default.All.Distinct(StringComparer.OrdinalIgnoreCase).Count().ShouldBe(BotNames.Default.All.Count);

    [Fact]
    public void Picking_is_reproducible_from_a_seed() =>
        BotNames.Default.Pick(new SeededRandom(5)).ShouldBe(BotNames.Default.Pick(new SeededRandom(5)));

    [Fact]
    public void Picking_avoids_taken_names_case_insensitively()
    {
        var pool = new BotNames(["Gabe", "Erik"]);

        for (ulong seed = 1; seed <= 20; seed++)
        {
            pool.Pick(new SeededRandom(seed), ["bot gabe"]).ShouldBe("BOT Erik");
        }
    }

    [Fact]
    public void A_fully_taken_pool_still_returns_a_name() =>
        new BotNames(["Gabe"]).Pick(new SeededRandom(1), ["BOT Gabe"]).ShouldBe("BOT Gabe");

    [Fact]
    public void Pools_ignore_blank_and_duplicate_names() =>
        new BotNames([" Gabe ", "gabe", "", "  "]).All.ShouldBe(["BOT Gabe"]);

    [Fact]
    public void An_empty_pool_is_rejected() =>
        Should.Throw<ArgumentException>(() => new BotNames([" "]));
}
