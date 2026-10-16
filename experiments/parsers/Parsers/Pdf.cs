#if PDF
using System.Text;
using System.Xml.Linq;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Tokens;

namespace Proto;

public sealed class PdfResult
{
    public string? Title { get; set; }

    public string? Author { get; set; }

    public string? Subject { get; set; }

    public string? Keywords { get; set; }

    public string? Producer { get; set; }

    public int Pages { get; set; }

    public bool XmpPresent { get; set; }

    public string? XmpTitle { get; set; }

    public string? XmpCreator { get; set; }

    public string? XmpLanguage { get; set; }

    public string? XmpLibraryCreator { get; set; }

    public string? CatalogLang { get; set; }

    public int ImagesPage1 { get; set; }

    public bool Encrypted { get; set; }

    public string? Text { get; set; }

    public long TextChars { get; set; }

    public long Words { get; set; }

    public string? FirstPageText { get; set; }

    public int PageNumberErrors { get; set; }

    public string? InfoIsbn { get; set; }

    public List<string> TextIsbns { get; } = [];

    public string? Isbn => InfoIsbn ?? (TextIsbns.Count > 0 ? TextIsbns[0] : null);

    public double OpenMs { get; set; }

    public double MetaMs { get; set; }

    public double TextMs { get; set; }

    public double IsbnMs { get; set; }
}

public static class Pdf
{
    private static readonly XNamespace Dc = "http://purl.org/dc/elements/1.1/";
    private static readonly XNamespace Rdf = "http://www.w3.org/1999/02/22-rdf-syntax-ns#";

    private static string? LibraryCreator(UglyToad.PdfPig.Content.XmpMetadata xmp) =>
        xmp.GetXDocument().Descendants(Dc + "creator").Descendants(Rdf + "li").FirstOrDefault()?.Value;

    private static string? Blank(string? s) => string.IsNullOrWhiteSpace(s) ? null : s;

    /// <summary>Page text from the words of the default word extractor, joined by spaces (Page.Text drops the spaces and line breaks of many PDFs, see X-P5).</summary>
    public static string PageText(UglyToad.PdfPig.Content.Page page) => string.Join(' ', page.GetWords().Select(x => x.Text));

    public static PdfResult Read(string path, bool text, bool lenient = true, bool keepText = true)
    {
        var r = new PdfResult();
        var clock = new Clock();
        using var doc = PdfDocument.Open(path, new ParsingOptions { UseLenientParsing = lenient });
        r.OpenMs = clock.Ms;
        clock = new Clock();
        var info = doc.Information;
        r.Title = Blank(info.Title);
        r.Author = Blank(info.Author);
        r.Subject = Blank(info.Subject);
        r.Keywords = Blank(info.Keywords);
        r.Producer = Blank(info.Producer);
        r.Pages = doc.NumberOfPages;
        r.Encrypted = doc.IsEncrypted;
        if (doc.TryGetXmpMetadata(out var xmp))
        {
            r.XmpPresent = true;
            // PdfPig 0.1.16 XmpMetadata.GetXDocument() decodes the UTF-8 packet as Latin-1 (non-ASCII text becomes mojibake);
            // parse the raw bytes with an XmlReader that honors the encoding instead, DTDs ignored, no resolver.
            r.XmpLibraryCreator = Blank(LibraryCreator(xmp));
            using var xmpStream = new MemoryStream(xmp.GetXmlBytes().ToArray());
            using var xmlReader = System.Xml.XmlReader.Create(xmpStream, new System.Xml.XmlReaderSettings { DtdProcessing = System.Xml.DtdProcessing.Ignore, XmlResolver = null });
            var x = XDocument.Load(xmlReader);
            r.XmpTitle = Blank(x.Descendants(Dc + "title").Descendants(Rdf + "li").FirstOrDefault()?.Value);
            r.XmpCreator = Blank(x.Descendants(Dc + "creator").Descendants(Rdf + "li").FirstOrDefault()?.Value);
            r.XmpLanguage = Blank(x.Descendants(Dc + "language").Descendants(Rdf + "li").FirstOrDefault()?.Value);
        }

        if (doc.Structure.Catalog.CatalogDictionary.TryGet(NameToken.Create("Lang"), out IToken lang) && lang is StringToken st)
        {
            r.CatalogLang = Blank(st.Data);
        }

        r.InfoIsbn = Isbn.Find((r.Keywords ?? string.Empty) + " " + (r.Subject ?? string.Empty));
        r.MetaMs = clock.Ms;
        if (r.Pages > 0)
        {
            var first = doc.GetPage(1);
            r.ImagesPage1 = first.NumberOfImages;
        }

        if (!text)
        {
            return r;
        }

        clock = new Clock();
        var sb = keepText ? new StringBuilder() : null;
        var scan = new List<string>();
        for (var i = 1; i <= r.Pages; i++)
        {
            var page = doc.GetPage(i);
            if (page.Number != i)
            {
                r.PageNumberErrors++;
            }

            var t = PageText(page);
            if (i == 1)
            {
                r.FirstPageText = t;
            }

            r.TextChars += t.Length;
            r.Words += Text.CountWords(t);
            sb?.Append(t).Append('\n');
            if (i <= 5 || i > r.Pages - 3)
            {
                scan.Add(t);
            }
        }

        r.TextMs = clock.Ms;
        r.Text = sb?.ToString();
        clock = new Clock();
        foreach (var t in scan)
        {
            foreach (var hit in Isbn.FindAll(t))
            {
                if (!r.TextIsbns.Contains(hit))
                {
                    r.TextIsbns.Add(hit);
                }
            }
        }

        r.IsbnMs = clock.Ms;
        return r;
    }
}
#endif
