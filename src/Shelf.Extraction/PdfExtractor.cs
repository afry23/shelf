using System.Xml;
using System.Xml.Linq;
using Shelf.Core.Model;
using UglyToad.PdfPig;

namespace Shelf.Extraction;

/// <summary>
/// PDF metadata from the XMP packet and the Info dictionary, text by page (PdfPig; conditions of ADR 0005):
/// text from <c>GetWords()</c> (never <c>Page.Text</c>, which drops spaces), XMP from the raw bytes with a
/// hardened <c>XmlReader</c> (never <c>GetXDocument()</c>, which decodes UTF-8 as Latin-1), lenient parsing.
/// </summary>
internal static class PdfExtractor
{
    private static readonly XNamespace Dc = "http://purl.org/dc/elements/1.1/";
    private static readonly XNamespace Rdf = "http://www.w3.org/1999/02/22-rdf-syntax-ns#";

    // ISBN scan range (ADR 0005, Q6): pages 1 to 5 and the last 3.
    private const int IsbnHeadPages = 5;
    private const int IsbnTailPages = 3;

    public static ExtractedBook Read(string path, bool withText)
    {
        using var document = PdfDocument.Open(path, new ParsingOptions { UseLenientParsing = true });
        var pages = document.NumberOfPages;
        if (pages == 0 && new FileInfo(path).Length > 0)
        {
            // PdfPig returns no pages for some damaged files without an error; that is not a success (ADR 0005, R-P5).
            throw new ExtractionException($"{path}: bad-pdf: the file has no pages", ExtractionErrorKind.BadPdf, path);
        }

        var info = document.Information;
        string? xmpTitle = null;
        List<string> xmpCreators = [];
        string? xmpLanguage = null;
        string? xmpIsbn = null;
        if (document.TryGetXmpMetadata(out var xmp))
        {
            using var stream = new MemoryStream(xmp.GetXmlBytes().ToArray());
            using var reader = XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore, XmlResolver = null });
            var xml = XDocument.Load(reader);
            xmpTitle = TextHelpers.Clean(Items(xml, "title").FirstOrDefault());
            xmpCreators = [.. Items(xml, "creator").Select(TextHelpers.Clean).OfType<string>()];
            xmpLanguage = TextHelpers.Clean(Items(xml, "language").FirstOrDefault());
            xmpIsbn = Items(xml, "identifier").Select(Isbn.FromIdentifier).FirstOrDefault(i => i is not null);
        }

        var segments = new List<TextSegment>();
        string? textIsbn = null;
        if (withText)
        {
            for (var number = 1; number <= pages; number++)
            {
                var text = string.Join(' ', document.GetPage(number).GetWords().Select(w => w.Text));
                if (string.IsNullOrWhiteSpace(text))
                {
                    continue;
                }

                segments.Add(new TextSegment(Locators.PdfPage(number), text));
                if (textIsbn is null && (number <= IsbnHeadPages || number > pages - IsbnTailPages))
                {
                    textIsbn = Isbn.Find(text);
                }
            }
        }

        // XMP is UTF-8 by definition; the Info dictionary may use PDFDocEncoding. Prefer XMP where both exist.
        var title = xmpTitle ?? TextHelpers.Clean(info.Title);
        var authors = xmpCreators.Count > 0
            ? xmpCreators
            : SplitAuthors(info.Author);
        var infoIsbn = Isbn.Find(string.Join(' ', info.Keywords ?? string.Empty, info.Subject ?? string.Empty));
        var metadata = new BookMetadata
        {
            Title = title,
            TitleOrigin = title is null ? MetadataOrigin.None : MetadataOrigin.FileContent,
            Authors = authors,
            AuthorsOrigin = authors.Count == 0 ? MetadataOrigin.None : MetadataOrigin.FileContent,
            Language = xmpLanguage,
            Isbn13 = xmpIsbn ?? infoIsbn ?? textIsbn,
        };
        return new ExtractedBook(metadata, pages, segments);
    }

    private static IEnumerable<string> Items(XDocument xml, string name) =>
        xml.Descendants(Dc + name).Descendants(Rdf + "li").Select(li => li.Value);

    /// <summary>The Info dictionary has one Author string; common separators are ";", "&amp;" and "and".</summary>
    private static List<string> SplitAuthors(string? author)
    {
        var cleaned = TextHelpers.Clean(author);
        if (cleaned is null)
        {
            return [];
        }

        return [.. System.Text.RegularExpressions.Regex.Split(cleaned, @"\s*;\s*|\s+&\s+|\s+and\s+", System.Text.RegularExpressions.RegexOptions.IgnoreCase)
            .Select(a => a.Trim())
            .Where(a => a.Length > 0)];
    }
}
