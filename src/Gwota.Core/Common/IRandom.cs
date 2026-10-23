namespace Gwota.Core.Common;

/// <summary>
/// Source of randomness for the rules engine. Every random decision goes through this
/// abstraction so matches are reproducible from a seed (tests, replays).
/// </summary>
public interface IRandom
{
    /// <summary>Returns a uniformly distributed integer in [0, <paramref name="maxExclusive"/>).</summary>
    int Next(int maxExclusive);

    /// <summary>Returns an independent copy that will produce the same sequence from now on.</summary>
    IRandom Clone();
}

public static class RandomExtensions
{
    /// <summary>In-place Fisher–Yates shuffle.</summary>
    public static void Shuffle<T>(this IRandom random, IList<T> items)
    {
        ArgumentNullException.ThrowIfNull(random);
        ArgumentNullException.ThrowIfNull(items);

        for (var i = items.Count - 1; i > 0; i--)
        {
            var j = random.Next(i + 1);
            (items[i], items[j]) = (items[j], items[i]);
        }
    }

    public static T Pick<T>(this IRandom random, IReadOnlyList<T> items)
    {
        ArgumentNullException.ThrowIfNull(random);
        ArgumentNullException.ThrowIfNull(items);
        if (items.Count == 0)
        {
            throw new ArgumentException("Cannot pick from an empty list.", nameof(items));
        }

        return items[random.Next(items.Count)];
    }
}
