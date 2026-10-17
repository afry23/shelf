using Shelf.Core.Grouping;
using Xunit;

namespace Shelf.Core.Tests.Grouping;

public sealed class SimHashTests
{
    private static string Text(int words) =>
        string.Join(' ', Enumerable.Range(0, words).Select(i => "word" + (i * 7 % 311).ToString(System.Globalization.CultureInfo.InvariantCulture)));

    [Fact]
    public void The_fingerprint_of_a_fixed_text_never_changes()
    {
        // Fingerprints are stored in the index and compared across versions of shelf: the algorithm is frozen.
        // If this fails on purpose (new algorithm), every stored fingerprint must be recomputed by a reindex.
        Assert.Equal(0x1C98ABAA765A72EDUL, SimHash.Compute([Text(1000)]));
    }

    [Fact]
    public void Fewer_than_the_minimum_number_of_words_give_no_fingerprint()
    {
        Assert.Null(SimHash.Compute([Text(SimHash.MinWords - 1)]));
        Assert.NotNull(SimHash.Compute([Text(SimHash.MinWords)]));
        Assert.Null(SimHash.Compute([]));
        Assert.Null(SimHash.Compute(["", "  ", "..."]));
    }

    [Fact]
    public void Words_after_the_maximum_are_ignored()
    {
        var head = Text(SimHash.MaxWords);

        Assert.Equal(SimHash.Compute([head]), SimHash.Compute([head + " extra words that do not count at all"]));
    }

    [Fact]
    public void The_words_of_all_segments_count_together()
    {
        var words = Text(1000).Split(' ');

        Assert.Equal(
            SimHash.Compute([string.Join(' ', words)]),
            SimHash.Compute([string.Join(' ', words.Take(300)), string.Join(' ', words.Skip(300).Take(100)), string.Join(' ', words.Skip(400))]));
    }

    [Fact]
    public void A_word_split_by_a_segment_border_is_two_words_but_a_space_is_not_needed_between_segments()
    {
        var first = SimHash.Compute([Text(500) + " one", "two " + Text(500)]);
        var second = SimHash.Compute([Text(500) + " one two " + Text(500)]);

        Assert.Equal(second, first);
    }

    [Fact]
    public void Letters_with_accents_and_ligatures_are_folded()
    {
        var plain = string.Join(' ', Enumerable.Repeat("strasse cafe aether angstrom", 80));
        var accented = string.Join(' ', Enumerable.Repeat("Straße Café Æther Ångström", 80));

        Assert.Equal(SimHash.Compute([plain]), SimHash.Compute([accented]));
    }

    [Theory]
    [InlineData(0UL, 0UL, 0)]
    [InlineData(0UL, ulong.MaxValue, 64)]
    [InlineData(0b1011UL, 0b0001UL, 2)]
    public void Distance_counts_the_differing_bits(ulong first, ulong second, int expected) =>
        Assert.Equal(expected, SimHash.Distance(first, second));
}
