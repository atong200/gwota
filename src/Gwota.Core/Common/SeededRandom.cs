namespace Gwota.Core.Common;

/// <summary>
/// xoshiro128** generator seeded through SplitMix64. Unlike <see cref="System.Random"/>, its output
/// is fully specified, so a seed yields the same match on every platform and runtime version.
/// </summary>
public sealed class SeededRandom : IRandom
{
    private uint _s0;
    private uint _s1;
    private uint _s2;
    private uint _s3;

    public SeededRandom(ulong seed)
    {
        var sm = seed;
        var a = SplitMix64(ref sm);
        var b = SplitMix64(ref sm);
        _s0 = (uint)a;
        _s1 = (uint)(a >> 32);
        _s2 = (uint)b;
        _s3 = (uint)(b >> 32);

        if ((_s0 | _s1 | _s2 | _s3) == 0)
        {
            _s0 = 1;
        }
    }

    private SeededRandom(uint s0, uint s1, uint s2, uint s3)
    {
        _s0 = s0;
        _s1 = s1;
        _s2 = s2;
        _s3 = s3;
    }

    public static SeededRandom FromTime() => new((ulong)DateTime.UtcNow.Ticks);

    public int Next(int maxExclusive)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxExclusive);

        // Lemire's bounded random with rejection to avoid modulo bias.
        var bound = (uint)maxExclusive;
        var product = (ulong)NextUInt() * bound;
        var low = (uint)product;
        if (low < bound)
        {
            var threshold = (0u - bound) % bound;
            while (low < threshold)
            {
                product = (ulong)NextUInt() * bound;
                low = (uint)product;
            }
        }

        return (int)(product >> 32);
    }

    public IRandom Clone() => new SeededRandom(_s0, _s1, _s2, _s3);

    private uint NextUInt()
    {
        var result = RotateLeft(_s1 * 5, 7) * 9;
        var t = _s1 << 9;

        _s2 ^= _s0;
        _s3 ^= _s1;
        _s1 ^= _s2;
        _s0 ^= _s3;
        _s2 ^= t;
        _s3 = RotateLeft(_s3, 11);

        return result;
    }

    private static uint RotateLeft(uint x, int k) => (x << k) | (x >> (32 - k));

    private static ulong SplitMix64(ref ulong state)
    {
        var z = state += 0x9E3779B97F4A7C15UL;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }
}
