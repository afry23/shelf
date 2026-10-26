using Xunit;

using Shelf.Core.Model;

namespace Shelf.Core.Tests.Model;

public sealed class CiteKeysTests
{
    [Theory]
    [InlineData("Bilgin Ibryam", 2019, "Kubernetes Patterns", "ibryam2019kubernetes")]
    [InlineData("Ibryam, Bilgin", 2019, "Kubernetes Patterns", "ibryam2019kubernetes")]
    [InlineData("Jan van der Berg", 1968, "The Art of Computer Programming", "berg1968art")]
    [InlineData("Lukas Weiß", 2015, "Über die Brücke (2nd Edition)", "weiss2015uber")]
    [InlineData("Edwin A. Abbott", null, "Flatland", "abbottflatland")]
    [InlineData("Jane Roe", 2001, "Of and The", "roe2001of")]
    public void Suggest_builds_surname_year_and_first_significant_word(string author, int? year, string title, string expected) =>
        Assert.Equal(expected, CiteKeys.Suggest([author], year, title));

    [Fact]
    public void Suggest_gives_nothing_without_a_known_author()
    {
        Assert.Null(CiteKeys.Suggest(["Unknown Author"], 2000, "Flatland"));
        Assert.Null(CiteKeys.Suggest([], 2000, "Flatland"));
    }

    [Theory]
    [InlineData("Bilgin Ibryam", 2019, "Kubernetes Patterns")]
    [InlineData("Jördis Ålander", 2021, "Event Streams in Practice")]
    public void A_suggestion_is_a_valid_key(string author, int year, string title) =>
        Assert.True(CiteKeys.IsValid(CiteKeys.Suggest([author], year, title)!));

    [Fact]
    public void MakeUnique_appends_letters_and_then_numbers()
    {
        Assert.Equal("k", CiteKeys.MakeUnique("k", _ => false));
        Assert.Equal("ka", CiteKeys.MakeUnique("k", t => t == "k"));
        Assert.Equal("kb", CiteKeys.MakeUnique("k", t => t is "k" or "ka"));
        var letters = Enumerable.Range('a', 26).Select(c => "k" + (char)c).Append("k").ToHashSet();
        Assert.Equal("k-2", CiteKeys.MakeUnique("k", letters.Contains));
        Assert.Equal("k-3", CiteKeys.MakeUnique("k", t => letters.Contains(t) || t == "k-2"));
    }
}
