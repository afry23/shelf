using System.Text;
using Shelf.TestData.Content;
using Xunit;

namespace Shelf.TestData.Tests;

public sealed class ContentTests
{
    [Fact]
    public void AuthorVariantsFollowThePlanExamples()
    {
        var author = new AuthorName("Martin", "Fowler");
        Assert.Equal("Fowler, Martin", NameVariants.Render(author, AuthorVariant.SurnameFirst));
        Assert.Equal("M. Fowler", NameVariants.Render(author, AuthorVariant.Initials));
        Assert.Equal("Martin Fowler", NameVariants.Render(author, AuthorVariant.Canonical));
        Assert.Equal("Jose Nunez", NameVariants.Render(new AuthorName("José", "Núñez"), AuthorVariant.NoDiacritics));
        Assert.Equal("Lukas Weiss", NameVariants.Render(new AuthorName("Lukas", "Weiß"), AuthorVariant.NoDiacritics));
    }

    [Fact]
    public void DiacriticsAreOnlyDetectedInNamesThatHaveThem()
    {
        Assert.True(NameVariants.HasDiacritics("José Núñez"));
        Assert.False(NameVariants.HasDiacritics("Martin Fowler"));
    }

    [Fact]
    public void TitleVariantsFollowThePlanExamples()
    {
        const string title = "Patterns of Distributed Caching";
        Assert.Equal("The Patterns of Distributed Caching", TitleVariants.Apply(title, "en", TitleVariant.LeadingArticle));
        Assert.Equal("Die Grundlagen der Datenverarbeitung", TitleVariants.Apply("Grundlagen der Datenverarbeitung", "de", TitleVariant.LeadingArticle));
        Assert.Equal(title + ", 2nd Edition", TitleVariants.Apply(title, "en", TitleVariant.EditionSuffix, 0));
        Assert.Equal(title + " (German Edition)", TitleVariants.Apply(title, "en", TitleVariant.EditionSuffix, 2));
        Assert.True(TitleVariants.HasNonAsciiLetter("Größe"));
        Assert.False(TitleVariants.HasNonAsciiLetter("Size - 2"));
    }

    [Fact]
    public void FileNamePatterns()
    {
        const string author = "Martin Fowler";
        const string title = "Patterns of Distributed Caching";
        Assert.Equal("Martin Fowler - Patterns of Distributed Caching (2017)", FileNames.Stem(FileNamePattern.AuthorTitleYear, author, title, 2017, null));
        Assert.Equal("Patterns of Distributed Caching", FileNames.Stem(FileNamePattern.Title, author, title, 2017, null));
        Assert.Equal("patterns_of_distributed_caching_2017", FileNames.Stem(FileNamePattern.SnakeCase, author, title, 2017, null));
        Assert.Equal("9780000000019", FileNames.Stem(FileNamePattern.Isbn13, author, title, 2017, "9780000000019"));
        Assert.Equal(title, FileNames.Stem(FileNamePattern.Isbn13, author, title, 2017, null));
        Assert.Equal("Patterns of Distributed Caching - Martin Fowler", FileNames.Stem(FileNamePattern.TitleAuthor, author, title, 2017, null));
    }

    [Fact]
    public void SanitizingReplacesEachForbiddenCharacter() =>
        Assert.Equal("a-b-c-- d", FileNames.Sanitize("a/b:c*?  d"));

    [Fact]
    public void SanitizingTrimsTrailingDotsAndSpaces() =>
        Assert.Equal("Title", FileNames.Sanitize("Title . . "));

    [Fact]
    public void SanitizingNormalizesToNfc() =>
        Assert.Equal("ü", FileNames.Sanitize("ü"));

    [Fact]
    public void LongTitlesAreCutAtTheLimit()
    {
        var title = new string('x', 130);
        Assert.Equal(120, FileNames.Sanitize(title).Length);
    }

    [Fact]
    public void CuttingNeverSplitsASurrogatePair()
    {
        var title = new string('x', 119) + "\U0001F600" + "tail";
        var sanitized = FileNames.Sanitize(title);
        Assert.Equal(119, sanitized.Length);
        Assert.All(sanitized, c => Assert.False(char.IsSurrogate(c)));
    }

    [Fact]
    public void CollisionsGetNumberedSuffixes()
    {
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        Assert.Equal("Book.epub", FileNames.MakeUnique("Book", ".epub", used));
        Assert.Equal("Book (1).epub", FileNames.MakeUnique("Book", ".epub", used));
        Assert.Equal("book (2).epub", FileNames.MakeUnique("book", ".epub", used));
    }

    [Fact]
    public void WordCountIsWithinTheRequestedRange()
    {
        var document = TextGenerator.Generate("en", 1, 3500);
        Assert.Equal(3500, document.WordCount);
        Assert.InRange(document.Chapters.Count, 1, 12);
    }

    [Fact]
    public void TheSameSeedProducesTheSameText()
    {
        var a = TextGenerator.Generate("de", 99, 1000);
        var b = TextGenerator.Generate("de", 99, 1000);
        Assert.Equal(a.Paragraphs, b.Paragraphs);
        Assert.NotEqual(a.Paragraphs, TextGenerator.Generate("de", 100, 1000).Paragraphs);
    }

    [Fact]
    public void AnEditionSharesAboutEightyFivePercentOfTheParagraphs()
    {
        var original = TextGenerator.Generate("en", 5, 8000);
        var edition = TextGenerator.Edition(original, "en", 6);
        var shared = original.Paragraphs.Zip(edition.Paragraphs).Count(pair => pair.First == pair.Second);
        var total = original.Paragraphs.Count();
        Assert.Equal(original.WordCount, edition.WordCount);
        Assert.InRange(shared * 100 / total, 70, 95);
    }

    [Fact]
    public void AllBodyCharactersAreEncodableInWinAnsi()
    {
        var latin1 = Encoding.Latin1;
        foreach (var language in new[] { "en", "de" })
        {
            foreach (var word in Vocabulary.Words(language).Concat(Vocabulary.Topics(language)))
            {
                Assert.All(word, c => Assert.True(c is >= ' ' and <= '~' or >= ' ' and <= 'ÿ', $"'{c}' in '{word}'"));
            }
        }

        foreach (var name in Vocabulary.GivenNames.Concat(Vocabulary.Surnames))
        {
            Assert.All(name, c => Assert.True(c is >= ' ' and <= '~' or >= ' ' and <= 'ÿ', $"'{c}' in '{name}'"));
        }

        Assert.Equal("ä", latin1.GetString(latin1.GetBytes("ä")));
    }

    [Fact]
    public void GermanVocabularyHasUmlautsAndSharpS()
    {
        var all = string.Concat(Vocabulary.Words("de"));
        Assert.All(new[] { 'ä', 'ö', 'ü', 'ß' }, c => Assert.Contains(c, all));
    }
}
