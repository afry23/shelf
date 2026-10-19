using System.Numerics;
using Xunit;

namespace Shelf.Index.Tests;

public sealed class NearFingerprintsTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(20)]
    public void The_blocks_find_exactly_the_pairs_that_a_comparison_of_all_pairs_finds(int maximum)
    {
        // Fingerprints in families: a base and copies with a few flipped bits, so that there are pairs at every distance.
        var random = new Random(maximum + 1);
        var hashes = new List<ulong>();
        for (var family = 0; family < 40; family++)
        {
            var seed = random.NextInt64();
            hashes.Add((ulong)seed);
            for (var copy = 0; copy < 4; copy++)
            {
                var flipped = (ulong)seed;
                foreach (var bit in Enumerable.Range(0, random.Next(0, 25)).Select(_ => random.Next(64)))
                {
                    flipped ^= 1UL << bit;
                }

                hashes.Add(flipped);
            }
        }

        var expected = new HashSet<(int, int, int)>();
        for (var i = 0; i < hashes.Count; i++)
        {
            for (var j = i + 1; j < hashes.Count; j++)
            {
                var distance = BitOperations.PopCount(hashes[i] ^ hashes[j]);
                if (distance <= maximum)
                {
                    expected.Add((i, j, distance));
                }
            }
        }

        var found = LibraryDuplicates.NearFingerprints([.. hashes], maximum).ToList();

        Assert.NotEmpty(expected);
        Assert.Equal(found.Count, found.Distinct().Count());
        Assert.Equal(expected.Order(), found.Order());
    }

    [Fact]
    public void Nothing_is_near_in_an_empty_or_a_single_list()
    {
        Assert.Empty(LibraryDuplicates.NearFingerprints([], 3));
        Assert.Empty(LibraryDuplicates.NearFingerprints([42UL], 3));
    }
}
