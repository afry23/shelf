using Xunit;

namespace Shelf.Semantic.Tests;

public sealed class PassageWindowsTests
{
    private static string Words(int count) => string.Join(' ', Enumerable.Range(1, count).Select(i => $"w{i}"));

    [Theory]
    [InlineData(1, 1)]
    [InlineData(280, 1)]
    [InlineData(281, 2)]
    [InlineData(520, 2)]
    [InlineData(521, 3)]
    [InlineData(600, 3)]
    public void A_chunk_is_cut_into_as_many_windows_as_it_needs(int words, int windows) =>
        Assert.Equal(windows, PassageWindows.Split(Words(words)).Count);

    [Fact]
    public void Neighbouring_windows_share_40_words_and_the_last_ends_with_the_text()
    {
        var windows = PassageWindows.Split(Words(600));

        var first = windows[0].Split(' ');
        var second = windows[1].Split(' ');
        Assert.Equal(280, first.Length);
        Assert.Equal(first[^40..], second[..40]);
        Assert.EndsWith("w600", windows[^1]);
    }

    [Fact]
    public void Text_without_words_stays_one_window() => Assert.Equal([""], PassageWindows.Split(""));

    [Fact]
    public void Never_more_than_the_largest_number_of_windows()
    {
        Assert.Equal(PassageWindows.MaxWindows, PassageWindows.Split(Words(5000)).Count);
    }

    [Theory]
    [InlineData(1L, 0)]
    [InlineData(1L, 7)]
    [InlineData(123456789L, 3)]
    public void A_key_leads_back_to_its_chunk(long chunk, int part) =>
        Assert.Equal(chunk, PassageWindows.ChunkOf(PassageWindows.KeyOf(chunk, part)));

    [Fact]
    public void Keys_of_different_chunks_never_meet() =>
        Assert.NotEqual(PassageWindows.KeyOf(1, PassageWindows.MaxWindows - 1), PassageWindows.KeyOf(2, 0));
}
