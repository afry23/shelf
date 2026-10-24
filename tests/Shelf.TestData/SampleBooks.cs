using Shelf.TestData.Content;
using Shelf.TestData.Formats;

namespace Shelf.TestData;

/// <summary>
/// Single EPUB and PDF files with chosen metadata and text, for scenarios that need a particular case
/// (same title and author but different text, a second variant of a file, ...). Deterministic: the same
/// arguments give the same bytes. The text is synthetic pseudo-prose; the seed decides which text.
/// </summary>
public static class SampleBooks
{
    /// <summary>An EPUB 3 (or 2) with a given title (null: no title element), authors, ISBN-13 and text.</summary>
    public static byte[] Epub(string? title, IReadOnlyList<string> authors, string? isbn13, int seed, int words = 1000, string language = "en", int version = 3) =>
        EpubWriter.Write(new EpubSpec
        {
            Version = version,
            Title = title,
            Authors = [.. authors.Select(a => new EpubAuthor(a, a))],
            Language = language,
            Isbn13 = isbn13,
            IdentitySeed = $"sample-{seed}-{title}",
            Document = TextGenerator.Generate(language == "de" ? Languages.German : Languages.English, (ulong)seed, words),
        });

    /// <summary>
    /// The EPUB of <see cref="Epub(string?, IReadOnlyList{string}, string?, int, int, string, int)"/> with the same seed, but with
    /// <paramref name="changedWords"/> words replaced, spread evenly over the text: a second edition of the same book.
    /// </summary>
    public static byte[] EpubWithChangedWords(string? title, IReadOnlyList<string> authors, int seed, int changedWords, int words = 1000)
    {
        var document = TextGenerator.Generate(Languages.English, (ulong)seed, words);
        var every = Math.Max(1, words / Math.Max(1, changedWords));
        var index = 0;
        bool Changed(int position) => position % every == 0 && position / every < changedWords;
        var chapters = document.Chapters.Select(chapter => new Chapter(chapter.Title, [.. chapter.Paragraphs.Select(paragraph =>
            string.Join(' ', paragraph.Split(' ').Select(word => Changed(index++) ? "edition" : word)))])).ToList();
        return EpubWriter.Write(new EpubSpec
        {
            Version = 3,
            Title = title,
            Authors = [.. authors.Select(a => new EpubAuthor(a, a))],
            Language = "en",
            IdentitySeed = $"sample-{seed}-{changedWords}-{title}",
            Document = new TextDocument(chapters),
        });
    }

    /// <summary>An EPUB 3 whose chapters are exactly the given ones (title and one paragraph of text each).</summary>
    public static byte[] Epub(string? title, IReadOnlyList<string> authors, IReadOnlyList<(string Title, string Text)> chapters, string language = "en") =>
        EpubWriter.Write(new EpubSpec
        {
            Version = 3,
            Title = title,
            Authors = [.. authors.Select(a => new EpubAuthor(a, a))],
            Language = language,
            IdentitySeed = $"sample-chapters-{title}",
            Document = new TextDocument([.. chapters.Select(c => new Chapter(c.Title, [c.Text]))]),
        });

    /// <summary>An EPUB 3 like the one with chapters, each chapter carrying an <c>epub:type</c> (null: none) on its body.</summary>
    public static byte[] Epub(string? title, IReadOnlyList<string> authors, IReadOnlyList<(string Title, string? Type, string Text)> chapters) =>
        EpubWriter.Write(new EpubSpec
        {
            Version = 3,
            Title = title,
            Authors = [.. authors.Select(a => new EpubAuthor(a, a))],
            Language = "en",
            IdentitySeed = $"sample-typed-chapters-{title}",
            Document = new TextDocument([.. chapters.Select(c => new Chapter(c.Title, [c.Text], c.Type))]),
        });

    /// <summary>A PDF with exactly the given pages (the text of each is wrapped into lines) and no title page.</summary>
    public static byte[] Pdf(string? title, string? author, IReadOnlyList<string> pages) =>
        PdfWriter.Write(new PdfSpec
        {
            Pages = [.. pages.Select(p => (IReadOnlyList<string>)PdfWriter.Wrap(p))],
            HasInfo = true,
            Title = title,
            Author = author,
            HasXmp = true,
            IdentitySeed = $"sample-pages-{title}",
        });

    /// <summary>
    /// A PDF whose first page shows title, author and ISBN, followed by the text. With <paramref name="withInfo"/>
    /// the Info dictionary names title and author too.
    /// </summary>
    public static byte[] Pdf(string? title, string? author, string? isbn13, int seed, int words = 1000, bool withInfo = true)
    {
        var lines = new List<string>();
        if (title is not null)
        {
            lines.Add(title);
        }

        if (author is not null)
        {
            lines.Add("by " + author);
        }

        if (isbn13 is not null)
        {
            lines.Add("ISBN " + Isbn.Hyphenate(isbn13));
        }

        lines.Add(string.Empty);
        DerivedFixtures.AddBody(lines, TextGenerator.Generate(Languages.English, (ulong)seed, words));
        return PdfWriter.Write(new PdfSpec
        {
            Pages = DerivedFixtures.Paginate(lines),
            HasInfo = withInfo,
            Title = title,
            Author = author,
            HasXmp = withInfo,
            IdentitySeed = $"sample-{seed}-{title}",
        });
    }
}
