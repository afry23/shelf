using System.Text;
using Shelf.TestData.Content;
using Shelf.TestData.Formats;
using Shelf.TestData.Random;

namespace Shelf.TestData;

/// <summary>
/// Fixed definitions of <c>tests/fixtures/derived</c>: small files with known defects and metadata, produced from
/// synthetic text (seeds 7 to 11). No third-party content.
/// </summary>
public static class DerivedFixtures
{
    internal const string EventStreamsIsbn = "9780000000019";
    internal const string SystemeIsbn = "9780000000026";
    private const int Words = 300;

    public static IReadOnlyList<string> FileNames { get; } =
    [
        "epub3-isbn-series.epub",
        "Event Streams in Practice.pdf",
        "epub2-isbn-cover.epub",
        "Jane Doe - Quiet Systems (2004).epub",
        "malformed-opf.epub",
        "not-a-zip.epub",
        "Jane Doe - Quiet Systems (2004).pdf",
        "scanned-page.pdf",
    ];

    /// <summary>Content of one derived fixture.</summary>
    public static byte[] Create(string fileName) => fileName switch
    {
        "epub3-isbn-series.epub" => EventStreamsEpub(),
        "Event Streams in Practice.pdf" => EventStreamsPdf(),
        "epub2-isbn-cover.epub" => GermanEpub(),
        "Jane Doe - Quiet Systems (2004).epub" => EmptyMetadataEpub(),
        "malformed-opf.epub" => MalformedEpub(),
        "not-a-zip.epub" => NotAZip(),
        "Jane Doe - Quiet Systems (2004).pdf" => QuietSystemsPdf(),
        "scanned-page.pdf" => ScannedPage(),
        _ => throw new ArgumentException($"Unknown derived fixture '{fileName}'.", nameof(fileName)),
    };

    public static void Write(string directory)
    {
        Directory.CreateDirectory(directory);
        foreach (var name in FileNames)
        {
            File.WriteAllBytes(Path.Combine(directory, name), Create(name));
        }
    }

    internal static TextDocument EventStreamsText() => TextGenerator.Generate(Languages.English, 7, Words);

    private static byte[] EventStreamsEpub() => EpubWriter.Write(new EpubSpec
    {
        Version = 3,
        Title = "Event Streams in Practice",
        Authors = [new EpubAuthor("Jördis Ålander", "Ålander, Jördis")],
        Language = "en",
        Isbn13 = EventStreamsIsbn,
        IdentitySeed = "d1",
        SeriesName = "Streaming Foundations",
        SeriesIndex = 2,
        Document = EventStreamsText(),
    });

    private static byte[] EventStreamsPdf()
    {
        var lines = new List<string>
        {
            "Event Streams in Practice",
            "by Jördis Ålander",
            string.Empty,
            "ISBN " + Isbn.Hyphenate(EventStreamsIsbn),
            string.Empty,
        };
        AddBody(lines, EventStreamsText());
        return PdfWriter.Write(new PdfSpec
        {
            Pages = Paginate(lines),
            HasInfo = true,
            Title = "Event Streams in Practice",
            Author = "Jördis Ålander",
            HasXmp = true,
            IdentitySeed = "d1",
        });
    }

    private static byte[] GermanEpub() => EpubWriter.Write(new EpubSpec
    {
        Version = 2,
        Title = "Größenordnungen verteilter Systeme",
        Authors = [new EpubAuthor("Lukas Weiß", "Weiß, Lukas"), new EpubAuthor("Ana Núñez", "Núñez, Ana")],
        Language = "de",
        Isbn13 = SystemeIsbn,
        IdentitySeed = "d2",
        SeriesName = "Verteilte Systeme",
        SeriesIndex = 1,
        CoverPng = PngWriter.Write(64, 96, 40, 90, 160),
        Document = TextGenerator.Generate(Languages.German, 8, Words),
    });

    private static byte[] EmptyMetadataEpub() => EpubWriter.Write(new EpubSpec
    {
        Version = 3,
        Title = string.Empty,
        IdentitySeed = "d3",
        Document = QuietSystemsText(),
    });

    private static byte[] MalformedEpub() => EpubWriter.Write(new EpubSpec
    {
        Version = 3,
        Title = "Broken Metadata",
        Authors = [new EpubAuthor("Ada Example", "Example, Ada")],
        Language = "en",
        IdentitySeed = "d4",
        MalformedOpf = true,
        Document = TextGenerator.Generate(Languages.English, 10, Words),
    });

    private static byte[] NotAZip()
    {
        const string text = "This file is not an EPUB container.";
        return Encoding.ASCII.GetBytes(text.PadRight(63) + "\n");
    }

    private static byte[] QuietSystemsPdf()
    {
        var lines = new List<string> { "Quiet Systems", "by Jane Doe", string.Empty };
        AddBody(lines, QuietSystemsText());
        return PdfWriter.Write(new PdfSpec { Pages = Paginate(lines), IdentitySeed = "d3" });
    }

    private static byte[] ScannedPage()
    {
        const int width = 200;
        const int height = 280;
        var rowBytes = (width + 7) / 8;
        var data = new byte[rowBytes * height];
        var rng = new Xoshiro256StarStar(11);
        for (var i = 0; i < data.Length; i++)
        {
            // Mostly white (set bits) with sparse noise, like a scan of an empty page.
            var value = (byte)0xFF;
            for (var bit = 0; bit < 8; bit++)
            {
                if (rng.NextInt(100) < 6)
                {
                    value = (byte)(value & ~(1 << bit));
                }
            }

            data[i] = value;
        }

        return PdfWriter.Write(new PdfSpec
        {
            Image = new PdfImage(width, height, data),
            HasInfo = true,
            Title = "Scanned Page",
            IdentitySeed = "d5",
        });
    }

    private static TextDocument QuietSystemsText() => TextGenerator.Generate(Languages.English, 9, Words);

    private static void AddBody(List<string> lines, TextDocument document)
    {
        foreach (var chapter in document.Chapters)
        {
            lines.Add(chapter.Title);
            lines.Add(string.Empty);
            foreach (var paragraph in chapter.Paragraphs)
            {
                lines.AddRange(PdfWriter.Wrap(paragraph));
                lines.Add(string.Empty);
            }
        }
    }

    private static List<IReadOnlyList<string>> Paginate(List<string> lines)
    {
        var pages = new List<IReadOnlyList<string>>();
        for (var from = 0; from < lines.Count; from += PdfWriter.LinesPerPage)
        {
            pages.Add(lines.GetRange(from, Math.Min(PdfWriter.LinesPerPage, lines.Count - from)));
        }

        return pages;
    }
}
