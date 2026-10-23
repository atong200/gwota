namespace Gwota.Core.Tests.Common;

public class SeededRandomTests
{
    [Fact]
    public void Same_seed_produces_same_sequence()
    {
        var a = new SeededRandom(42);
        var b = new SeededRandom(42);

        Enumerable.Range(0, 100).Select(_ => a.Next(1000)).ToList()
            .ShouldBe(Enumerable.Range(0, 100).Select(_ => b.Next(1000)).ToList());
    }

    [Fact]
    public void Different_seeds_produce_different_sequences()
    {
        var a = new SeededRandom(1);
        var b = new SeededRandom(2);

        Enumerable.Range(0, 20).Select(_ => a.Next(1000)).ToList()
            .ShouldNotBe(Enumerable.Range(0, 20).Select(_ => b.Next(1000)).ToList());
    }

    [Fact]
    public void Clone_continues_the_same_sequence_independently()
    {
        var original = new SeededRandom(7);
        original.Next(10);
        var clone = original.Clone();

        var fromClone = Enumerable.Range(0, 10).Select(_ => clone.Next(100)).ToList();
        var fromOriginal = Enumerable.Range(0, 10).Select(_ => original.Next(100)).ToList();

        fromClone.ShouldBe(fromOriginal);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(7)]
    [InlineData(1000)]
    public void Next_stays_in_range(int max)
    {
        var random = new SeededRandom(3);

        for (var i = 0; i < 1000; i++)
        {
            random.Next(max).ShouldBeInRange(0, max - 1);
        }
    }

    [Fact]
    public void Next_is_roughly_uniform()
    {
        var random = new SeededRandom(11);
        var counts = new int[4];
        for (var i = 0; i < 40_000; i++)
        {
            counts[random.Next(4)]++;
        }

        counts.ShouldAllBe(c => c > 9_000 && c < 11_000);
    }

    [Fact]
    public void Next_rejects_non_positive_bounds() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new SeededRandom(1).Next(0));

    [Fact]
    public void Shuffle_is_a_permutation()
    {
        var items = Enumerable.Range(0, 30).ToList();

        new SeededRandom(5).Shuffle(items);

        items.Order().ToList().ShouldBe(Enumerable.Range(0, 30).ToList());
        items.ShouldNotBe(Enumerable.Range(0, 30).ToList());
    }

    [Fact]
    public void Pick_rejects_empty_lists() =>
        Should.Throw<ArgumentException>(() => new SeededRandom(1).Pick(Array.Empty<int>()));
}
