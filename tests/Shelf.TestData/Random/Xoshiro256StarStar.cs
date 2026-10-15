using System.Numerics;

namespace Shelf.TestData.Random;

/// <summary>
/// xoshiro256** (public domain, https://prng.di.unimi.it/xoshiro256starstar.c), seeded through SplitMix64.
/// <c>System.Random</c> is deliberately not used: its seeded algorithm is not a documented contract.
/// </summary>
internal sealed class Xoshiro256StarStar
{
    private ulong _s0;
    private ulong _s1;
    private ulong _s2;
    private ulong _s3;

    public Xoshiro256StarStar(ulong seed)
    {
        var mixer = new SplitMix64(seed);
        _s0 = mixer.Next();
        _s1 = mixer.Next();
        _s2 = mixer.Next();
        _s3 = mixer.Next();
    }

    public ulong Next()
    {
        unchecked
        {
            var result = BitOperations.RotateLeft(_s1 * 5, 7) * 9;
            var t = _s1 << 17;
            _s2 ^= _s0;
            _s3 ^= _s1;
            _s1 ^= _s2;
            _s0 ^= _s3;
            _s2 ^= t;
            _s3 = BitOperations.RotateLeft(_s3, 45);
            return result;
        }
    }

    /// <summary>Uniform value in <c>[0, bound)</c> (Lemire's unbiased multiply-shift on 32 bits).</summary>
    public int NextInt(int bound)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(bound);
        var range = (uint)bound;
        var x = (uint)(Next() >> 32);
        var m = (ulong)x * range;
        var low = (uint)m;
        if (low < range)
        {
            var threshold = unchecked((uint)(0 - range)) % range;
            while (low < threshold)
            {
                x = (uint)(Next() >> 32);
                m = (ulong)x * range;
                low = (uint)m;
            }
        }

        return (int)(m >> 32);
    }

    /// <summary>Uniform value in <c>[0, bound)</c> for bounds above <see cref="int.MaxValue"/> (Lemire on 64 bits).</summary>
    public long NextInt64(long bound)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(bound);
        var range = (ulong)bound;
        var high = Math.BigMul(Next(), range, out var low);
        if (low < range)
        {
            var threshold = unchecked(0UL - range) % range;
            while (low < threshold)
            {
                high = Math.BigMul(Next(), range, out low);
            }
        }

        return (long)high;
    }

    /// <summary>True with probability <c>permille / 1000</c>; compared as integers, never via floating point.</summary>
    public bool Chance(int permille) => NextInt(1000) < permille;

    public T Pick<T>(IReadOnlyList<T> items) => items[NextInt(items.Count)];

    /// <summary>In-place Fisher-Yates shuffle.</summary>
    public void Shuffle<T>(IList<T> items)
    {
        for (var i = items.Count - 1; i > 0; i--)
        {
            var j = NextInt(i + 1);
            (items[i], items[j]) = (items[j], items[i]);
        }
    }
}
