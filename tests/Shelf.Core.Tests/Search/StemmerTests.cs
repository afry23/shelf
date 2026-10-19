using Shelf.Core.Search;
using Xunit;

namespace Shelf.Core.Tests.Search;

public sealed class StemmerTests
{
    [Theory]
    [InlineData("caresses", "caress")]
    [InlineData("ponies", "poni")]
    [InlineData("ties", "tie")]
    [InlineData("cats", "cat")]
    [InlineData("feed", "feed")]
    [InlineData("agreed", "agre")]
    [InlineData("plastered", "plaster")]
    [InlineData("motoring", "motor")]
    [InlineData("conflated", "conflat")]
    [InlineData("troubled", "troubl")]
    [InlineData("sized", "size")]
    [InlineData("hopping", "hop")]
    [InlineData("falling", "fall")]
    [InlineData("happy", "happi")]
    [InlineData("relational", "relat")]
    [InlineData("generously", "generous")]
    [InlineData("running", "run")]
    [InlineData("patterns", "pattern")]
    [InlineData("events", "event")]
    [InlineData("sourcing", "sourc")]
    [InlineData("sourced", "sourc")]
    [InlineData("skies", "sky")]
    [InlineData("dying", "die")]
    public void English_words_get_the_porter2_stem(string word, string stem) =>
        Assert.Equal(stem, Stemmer.Stem(word, Stemmer.English));

    [Theory]
    [InlineData("häuser", "haus")]
    [InlineData("bücher", "buch")]
    [InlineData("kategorie", "kategori")]
    [InlineData("kategorien", "kategori")]
    [InlineData("aufeinanderfolgende", "aufeinanderfolg")]
    [InlineData("systeme", "system")]
    [InlineData("system", "syst")] // Snowball strips -em; the index also keeps the stem of the stem, so "system" finds "Systeme"
    [InlineData("verteilte", "verteilt")]
    [InlineData("verteiltes", "verteilt")]
    [InlineData("verstehen", "versteh")]
    [InlineData("verstehe", "versteh")]
    [InlineData("strasse", "strass")]
    [InlineData("straße", "strass")]
    public void German_words_get_the_snowball_stem(string word, string stem) =>
        Assert.Equal(stem, Stemmer.Stem(word, Stemmer.German));

    [Fact]
    public void A_word_without_a_stemmer_is_unchanged() => Assert.Equal("cats", Stemmer.Stem("cats", null));

    [Theory]
    [InlineData("en", "en")]
    [InlineData("en-GB", "en")]
    [InlineData("EN_us", "en")]
    [InlineData("de", "de")]
    [InlineData("de-AT", "de")]
    [InlineData("fr", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void The_stemmer_is_chosen_by_the_primary_language(string? language, string? expected) =>
        Assert.Equal(expected, Stemmer.For(language));

    [Fact]
    public void Words_are_letters_and_digits_in_lower_case() =>
        Assert.Equal(["event", "sourcing", "in", "c", "2024"], Stemmer.Words("Event-Sourcing in C++ (2024)"));
}
