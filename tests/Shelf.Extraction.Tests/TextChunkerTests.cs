using Xunit;

namespace Shelf.Extraction.Tests;

public sealed class TextChunkerTests
{
    private static string Words(int count) => string.Join(' ', Enumerable.Range(1, count).Select(i => "w" + i));

    [Fact]
    public void A_short_segment_is_one_chunk_with_its_locator() =>
        Assert.Equal([new TextChunk(0, "page=3", "a b c")], TextChunker.Chunk([new TextSegment("page=3", "a   b\n c")]));

    [Fact]
    public void A_segment_of_up_to_600_words_stays_whole()
    {
        var chunks = TextChunker.Chunk([new TextSegment("page=1", Words(600))]);

        Assert.Single(chunks);
    }

    [Theory]
    [InlineData(601, 2)]
    [InlineData(1000, 2)]
    [InlineData(1001, 3)]
    [InlineData(5000, 10)]
    public void A_longer_segment_is_cut_into_nearly_equal_parts_of_about_500_words(int words, int expectedChunks)
    {
        var chunks = TextChunker.Chunk([new TextSegment("epubcfi(/6/2[c]!)", Words(words))]);

        Assert.Equal(expectedChunks, chunks.Count);
        var sizes = chunks.Select(c => c.Text.Split(' ').Length).ToList();
        Assert.Equal(words, sizes.Sum());
        Assert.True(sizes.Max() - sizes.Min() <= 1 || sizes[^1] < sizes[0], "parts are nearly equal");
        Assert.True(sizes.Max() <= TextChunker.MaxWords);
        Assert.All(chunks, c => Assert.Equal("epubcfi(/6/2[c]!)", c.Locator));
    }

    [Fact]
    public void Words_keep_their_order_across_the_parts()
    {
        var chunks = TextChunker.Chunk([new TextSegment("page=1", Words(1200))]);

        Assert.Equal(Words(1200), string.Join(' ', chunks.Select(c => c.Text)));
    }

    [Fact]
    public void Ordinals_run_through_all_segments_and_empty_segments_are_skipped()
    {
        var chunks = TextChunker.Chunk([
            new TextSegment("page=1", "one two"),
            new TextSegment("page=2", "   "),
            new TextSegment("page=3", "three"),
        ]);

        Assert.Equal([0, 1], chunks.Select(c => c.Ordinal));
        Assert.Equal(["page=1", "page=3"], chunks.Select(c => c.Locator));
    }

    [Fact]
    public void No_segments_give_no_chunks() => Assert.Empty(TextChunker.Chunk([]));
}
