using System.Globalization;
using System.Security.Cryptography;
using Shelf.TestData.Content;
using Shelf.TestData.Formats;
using Shelf.TestData.Model;
using Shelf.TestData.Planning;
using Shelf.TestData.Random;

namespace Shelf.TestData;

public sealed record GeneratorOptions(
    int Works,
    int Files,
    int Seed = 42,
    int WordsMin = 3000,
    int WordsMax = 8000,
    bool Deflate = false,
    int MaxDegreeOfParallelism = -1,
    bool PrintLayout = false);

public sealed record GenerationResult(int Works, int Files, long TotalBytes, string OutputDirectory);

/// <summary>
/// Deterministic synthetic library (slice plan 3.4/3.5). Everything that enters the output derives from the seed:
/// each work has its own generator, so work rendering is order-independent and may run in parallel.
/// </summary>
public static class LibraryGenerator
{
    private static readonly string[] Folders = ["downloads", "downloads/old", "ebooks-backup"];

    public static LibrarySummary Summarize(LibraryPlan plan, long? totalBytes = null) => new()
    {
        Works = plan.Works,
        Files = plan.Files,
        SingleFileWorks = plan.SingleFileWorks,
        TwoFileWorks = plan.TwoFileWorks,
        ThreeFileWorks = plan.ThreeFileWorks,
        EpubFiles = plan.EpubFiles,
        PdfFiles = plan.PdfFiles,
        ExactDuplicates = plan.ExactDuplicates,
        SameFormatDuplicates = plan.SameFormatDuplicates,
        EditionPairs = plan.EditionPairs,
        HardNegativePairs = plan.HardNegativePairs,
        SeriesWorks = plan.SeriesWorks,
        TotalBytes = totalBytes,
    };

    /// <summary>Generates the library below <paramref name="outDirectory"/> (which must be empty or missing) and writes ground-truth.json.</summary>
    public static GenerationResult Generate(GeneratorOptions options, string outDirectory)
    {
        ArgumentNullException.ThrowIfNull(options);
        var plan = LibraryPlanner.Plan(options.Works, options.Files);
        var builder = new LibraryBuilder(options, plan);
        builder.Build();

        Directory.CreateDirectory(outDirectory);
        foreach (var folder in Folders)
        {
            Directory.CreateDirectory(Path.Combine(outDirectory, "input", folder));
        }

        var parallel = new ParallelOptions { MaxDegreeOfParallelism = options.MaxDegreeOfParallelism };
        Parallel.For(0, builder.Works.Length, parallel, i => Render(builder.Works[i], options, outDirectory));

        var groundTruth = builder.ToGroundTruth();
        groundTruth.Write(Path.Combine(outDirectory, "ground-truth.json"));
        return new GenerationResult(plan.Works, plan.Files, groundTruth.Summary.TotalBytes ?? 0, outDirectory);
    }

    internal static ulong Derive(ulong seed, int index, int salt)
    {
        unchecked
        {
            var mixer = new SplitMix64((seed * 0x9E3779B97F4A7C15UL) ^ ((ulong)(index + 1) * 0xD1B54A32D192ED03UL) ^ ((ulong)(salt + 1) * 0x8CB92BA72F3D8DD7UL));
            mixer.Next();
            return mixer.Next();
        }
    }

    // ---- Phase 2: bytes -----------------------------------------------------------------------------------

    private static void Render(WorkSpec work, GeneratorOptions options, string outDirectory)
    {
        var baseDocument = TextGenerator.Generate(work.Language, work.TextSeed, work.Words);
        var document = work.EditionOf >= 0
            ? TextGenerator.Edition(baseDocument, work.Language, work.EditionSeed)
            : baseDocument;
        var written = new Dictionary<FileSpec, byte[]>();
        for (var i = 0; i < work.Files.Count; i++)
        {
            var file = work.Files[i];
            byte[] bytes;
            if (file.DuplicateKind == DuplicateKinds.Exact && file.DuplicateOf is not null)
            {
                bytes = written[file.DuplicateOf];
            }
            else if (file.Format == "epub")
            {
                bytes = RenderEpub(work, file, document, options, i);
            }
            else
            {
                bytes = RenderPdf(work, file, document, i, options.PrintLayout, out var pages);
                file.PageCount = pages;
            }

            written[file] = bytes;
            File.WriteAllBytes(Path.Combine(outDirectory, file.Path.Replace('/', Path.DirectorySeparatorChar)), bytes);
            file.Size = bytes.Length;
            file.Sha256 = Convert.ToHexStringLower(SHA256.HashData(bytes));
        }
    }

    private static byte[] RenderEpub(WorkSpec work, FileSpec file, TextDocument document, GeneratorOptions options, int position)
    {
        var colorSeed = Derive((ulong)options.Seed, work.Index, 12);
        byte[]? cover = file.Cover
            ? PngWriter.Write(64, 96, (byte)(60 + (colorSeed % 160)), (byte)(60 + ((colorSeed >> 8) % 160)), (byte)(60 + ((colorSeed >> 16) % 160)))
            : null;
        return EpubWriter.Write(new EpubSpec
        {
            Version = file.EpubVersion,
            Title = file.EmbeddedTitle,
            Authors = file.EpubAuthors,
            Language = file.EmbeddedLanguage,
            Isbn13 = file.EmbeddedIsbn,
            IdentitySeed = work.Key + "/" + position.ToString(CultureInfo.InvariantCulture),
            SeriesName = file.SeriesEmbedded ? work.SeriesName : null,
            SeriesIndex = work.SeriesIndex,
            CoverPng = cover,
            Document = document,
            Deflate = options.Deflate,
        });
    }

    private static byte[] RenderPdf(WorkSpec work, FileSpec file, TextDocument document, int position, bool printLayout, out int pageCount)
    {
        var lines = new List<string> { work.Title, "by " + string.Join(", ", work.Authors.Select(a => a.Display)), string.Empty };
        if (file.IsbnInText && work.Isbn13 is not null)
        {
            lines.Add("ISBN " + Isbn.Hyphenate(work.Isbn13));
            lines.Add(string.Empty);
        }

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

        var pages = new List<IReadOnlyList<string>>();
        if (printLayout)
        {
            // What a print layout adds to the running text: a running header and a page number on every page, and words
            // that are hyphenated at the end of a line. The pages are two lines shorter for header and footer.
            Hyphenate(lines);
            var body = PdfWriter.LinesPerPage - 2;
            for (var from = 0; from < lines.Count; from += body)
            {
                var page = new List<string> { work.Title };
                page.AddRange(lines.GetRange(from, Math.Min(body, lines.Count - from)));
                page.Add((pages.Count + 1).ToString(CultureInfo.InvariantCulture));
                pages.Add(page);
            }
        }
        else
        {
            for (var from = 0; from < lines.Count; from += PdfWriter.LinesPerPage)
            {
                pages.Add(lines.GetRange(from, Math.Min(PdfWriter.LinesPerPage, lines.Count - from)));
            }
        }

        pageCount = pages.Count;
        var hasMetadata = file.PdfMode != PdfMetadataMode.None;
        return PdfWriter.Write(new PdfSpec
        {
            Pages = pages,
            HasInfo = hasMetadata,
            Title = hasMetadata ? file.EmbeddedTitle : null,
            Author = hasMetadata && file.EmbeddedAuthors.Count > 0 ? string.Join("; ", file.EmbeddedAuthors) : null,
            Keywords = hasMetadata && file.IsbnInInfo && work.Isbn13 is not null ? "ISBN " + work.Isbn13 : null,
            HasXmp = file.PdfMode == PdfMetadataMode.InfoAndXmp,
            IdentitySeed = work.Key + "/" + position.ToString(CultureInfo.InvariantCulture),
        });
    }

    /// <summary>Hyphenates the last word of every seventh line (if it is long enough) and moves the rest to the next line, as a print layout does.</summary>
    private static void Hyphenate(List<string> lines)
    {
        for (var i = 0; i + 1 < lines.Count; i += 7)
        {
            var line = lines[i];
            var space = line.LastIndexOf(' ');
            var last = space < 0 ? line : line[(space + 1)..];
            if (last.Length > 7 && last.All(char.IsLetter) && lines[i + 1].Length > 0)
            {
                lines[i] = line[..(line.Length - last.Length + 4)] + "-";
                lines[i + 1] = last[4..] + " " + lines[i + 1];
            }
        }
    }
}
