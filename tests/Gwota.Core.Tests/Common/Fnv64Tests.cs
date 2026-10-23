namespace Gwota.Core.Tests.Common;

public class Fnv64Tests
{
    [Fact]
    public void An_empty_hash_is_the_offset_basis() =>
        new Fnv64().Value.ShouldBe(14695981039346656037UL);

    [Fact]
    public void The_same_input_gives_the_same_value()
    {
        var first = new Fnv64();
        first.Add(42);
        first.Add("abc");
        first.Add(true);
        var second = new Fnv64();
        second.Add(42);
        second.Add("abc");
        second.Add(true);

        first.Value.ShouldBe(second.Value);
    }

    [Fact]
    public void Order_matters()
    {
        var first = new Fnv64();
        first.Add(1);
        first.Add(2);
        var second = new Fnv64();
        second.Add(2);
        second.Add(1);

        first.Value.ShouldNotBe(second.Value);
    }

    [Fact]
    public void Null_and_empty_strings_differ()
    {
        var empty = new Fnv64();
        empty.Add(string.Empty);
        var none = new Fnv64();
        none.Add((string?)null);

        empty.Value.ShouldNotBe(none.Value);
    }

    [Fact]
    public void A_single_byte_matches_the_reference_algorithm()
    {
        var hash = new Fnv64();
        hash.Add(true);

        hash.Value.ShouldBe(unchecked((14695981039346656037UL ^ 1UL) * 1099511628211UL));
    }
}