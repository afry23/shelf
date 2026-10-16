using System.Globalization;

namespace Proto;

public static class Checks
{
    private static string Inv(FormattableString s) => FormattableString.Invariant(s);

    private static bool Contains(string? text, string needle) =>
        text is not null && Text.Collapse(text).Contains(Text.Collapse(needle), StringComparison.Ordinal);

    private static string Q(string? s) => s is null ? "null" : "'" + Text.First(Text.Collapse(s), 120) + "'";

#if EPUB
    private sealed record EpubRow(string Id, string Path, string? Title, string[] Authors, string? Lang, bool LangPrefix, string Version, bool Cover, string? TextContains, string? CoverMagic);

    private static readonly EpubRow[] EpubRows =
    [
        new("X-E1", "upstream/gutenberg/pg201-flatland.epub2.epub", "Flatland: A Romance of Many Dimensions", ["Edwin Abbott Abbott"], "en", true, "2.0", true, "I call our world Flatland", null),
        new("X-E2", "upstream/gutenberg/pg201-flatland.epub3.epub", "Flatland: A Romance of Many Dimensions", ["Edwin Abbott Abbott"], "en", true, "3.0", true, "I call our world Flatland", null),
        new("X-E3", "upstream/gutenberg/pg22367-die-verwandlung.epub3.epub", "Die Verwandlung", ["Franz Kafka"], "de", true, "3.0", true, "Als Gregor Samsa eines Morgens aus unruhigen Träumen erwachte", null),
        new("X-E4", "upstream/standardebooks/edwin-a-abbott_flatland.epub", "Flatland", ["Edwin A. Abbott"], "en", true, "3.0", true, "I call our world Flatland", null),
        new("X-E5", "derived/epub3-isbn-series.epub", "Event Streams in Practice", ["Jördis Ålander"], "en", false, "3.0", false, "", null),
        new("X-E6", "derived/epub2-isbn-cover.epub", "Größenordnungen verteilter Systeme", ["Lukas Weiß", "Ana Núñez"], "de", false, "2.0", true, "", "image/png"),
        new("X-E7", "derived/Jane Doe - Quiet Systems (2004).epub", null, [], null, false, "3.0", false, "", null),
    ];

    public static int EpubCheck(Args a, TextWriter w)
    {
        var dir = a.Positional.Count > 0 ? a.Positional[0] : throw new ArgumentException("epub-check <fixtures-dir>");
        var rows = new Rows(w);
        var results = new Dictionary<string, EpubResult>(StringComparer.Ordinal);
        foreach (var row in EpubRows)
        {
            EpubResult r;
            try
            {
                r = Epub.Read(Path.Combine(dir, row.Path), text: true, cover: true, variantA: true);
            }
            catch (Exception e)
            {
                rows.Gate(row.Id, false, "exception " + e.GetType().FullName + ": " + e.Message);
                continue;
            }

            results[row.Id] = r;
            var problems = new List<string>();
            if (!string.Equals(Text.Collapse(r.Title ?? string.Empty), row.Title ?? string.Empty, StringComparison.Ordinal))
            {
                problems.Add($"title {Q(r.Title)}");
            }

            if (!r.Authors.Select(Text.Collapse).SequenceEqual(row.Authors, StringComparer.Ordinal))
            {
                problems.Add("authors " + string.Join(" / ", r.Authors));
            }

            if (row.Lang is null ? r.Language is not null : r.Language is null || (row.LangPrefix ? !r.Language.StartsWith(row.Lang, StringComparison.Ordinal) : r.Language != row.Lang))
            {
                problems.Add("language " + Q(r.Language));
            }

            if (r.Version != row.Version)
            {
                problems.Add("version " + r.Version);
            }

            if (r.HasCover != row.Cover)
            {
                problems.Add("cover " + r.HasCover);
            }

            if (row.TextContains is { Length: > 0 } needle ? !Contains(r.Text, needle) : Text.Collapse(r.Text ?? string.Empty).Length == 0)
            {
                problems.Add("text check failed");
            }

            if (row.Id == "X-E5")
            {
                if (r.OpfIsbn != "9780000000019" || !r.Identifiers.Contains("|urn:isbn:9780000000019"))
                {
                    problems.Add("isbn " + r.OpfIsbn);
                }

                if (r.SeriesName != "Streaming Foundations" || r.SeriesIndex != "2")
                {
                    problems.Add($"series {r.SeriesName} #{r.SeriesIndex}");
                }
            }

            if (row.Id == "X-E6")
            {
                if (r.OpfIsbn != "9780000000026" || !r.Identifiers.Contains("ISBN|9780000000026"))
                {
                    problems.Add("isbn " + r.OpfIsbn + " ids " + string.Join(",", r.Identifiers));
                }

                if (r.SeriesName != "Verteilte Systeme" || r.SeriesIndex != "1")
                {
                    problems.Add($"series {r.SeriesName} #{r.SeriesIndex}");
                }
            }

            rows.Gate(row.Id, problems.Count == 0, problems.Count == 0 ? $"{row.Path} title={Q(r.Title)} authors={string.Join(" / ", r.Authors)} lang={r.Language} v={r.Version}" : string.Join("; ", problems));
            rows.Observe(row.Id + "-identifiers", string.Join(" ; ", r.Identifiers) + " ; series=" + r.SeriesName + " #" + r.SeriesIndex + " ; opfIsbn=" + r.OpfIsbn);
        }

        foreach (var row in EpubRows)
        {
            if (!results.TryGetValue(row.Id, out var r))
            {
                continue;
            }

            var id = "C-" + row.Id[2..];
            if (row.Cover)
            {
                var ok = r.HasCover && r.CoverBytes > 0 && r.CoverMagic is "image/jpeg" or "image/png";
                if (row.CoverMagic is { } want)
                {
                    ok &= r.CoverMagic == want && r.CoverSize == (64, 96);
                }

                rows.Gate(id, ok, Inv($"magic={r.CoverMagic} declared={r.CoverDeclared} bytes={r.CoverBytes} rule={r.CoverRule} png={r.CoverSize?.W}x{r.CoverSize?.H} ext={Covers.Extension(r.CoverMagic)}"));
            }
            else
            {
                rows.Gate(id, !r.HasCover, "no cover");
            }
        }

        foreach (var row in EpubRows)
        {
            if (!results.TryGetValue(row.Id, out var r))
            {
                continue;
            }

            var resolved = r.Spine.All(s => s.Href.Length > 0) && r.Spine.Count > 0;
            rows.Gate("L-E2-" + row.Id[2..], resolved, Inv($"spine items={r.Spine.Count} hrefs resolved"));
            rows.Observe("L-E-" + row.Id[2..], Inv($"spine={r.Spine.Count} nav={r.NavEntries} mapped={r.NavMapped} cfi0={r.Spine.FirstOrDefault()?.Cfi} chars=[{string.Join(",", r.Spine.Select(s => s.Chars))}] linear=[{string.Join(",", r.Spine.Select(s => s.Linear ? 1 : 0))}]"));
            rows.Observe("XHTML-" + row.Id[2..], Inv($"variant(a) ok={r.XhtmlAOk}/{r.Spine.Count} errors=[{string.Join(",", r.XhtmlAErrors.Distinct())}] variant(b) chars={r.TextChars}"));
            rows.Observe("I-E-" + row.Id[2..], "text isbns=[" + string.Join(",", r.TextIsbns) + "]");
            if (row.Id is "X-E5" or "X-E6" or "X-E7")
            {
                rows.Gate("I-E-" + row.Id[2..], r.TextIsbns.Count == 0, "no ISBN in synthetic EPUB text");
            }
        }

        if (results.TryGetValue("X-E5", out var e5))
        {
            rows.Gate("L-E1", e5.Spine.Count == e5.NavEntries && e5.NavMapped == e5.NavEntries && e5.Spine.Count > 0, Inv($"spine={e5.Spine.Count} nav={e5.NavEntries} mapped={e5.NavMapped}"));
            rows.Observe("X-E8", string.Join(" ; ", e5.AuthorDetails));
        }

        TimeEpubModes(dir, rows);
        ManifestCrossCheck(dir, rows);
        return rows.Finish();
    }

    private static void TimeEpubModes(string dir, Rows rows)
    {
        foreach (var row in EpubRows)
        {
            var path = Path.Combine(dir, row.Path);
            double Median(Func<double> f) => Stats.Median([.. Enumerable.Range(0, 5).Select(_ => f())]);
            var meta = Median(() => Clock.Time(() => Epub.Read(path, text: false, cover: false)));
            var full = Median(() => Clock.Time(() => VersOne.Epub.EpubReader.ReadBookAsync(path).GetAwaiter().GetResult()));
            rows.Observe("X-E9-" + row.Id[2..], Inv($"OpenBook+metadata median={meta:F2} ms; ReadBook (all content) median={full:F2} ms; size={new FileInfo(path).Length} B"));
        }
    }

    private static void ManifestCrossCheck(string dir, Rows rows)
    {
        var manifest = JsonFiles.Manifest(Path.Combine(dir, "manifest.json"));
        foreach (var f in manifest.Fixtures.Where(f => f.Format == "epub" && f.Traits.Contains("malformed-opf") == false && f.Traits.Contains("not-a-zip") == false))
        {
            try
            {
                var r = Epub.Read(Path.Combine(dir, f.Path), text: false, cover: true);
                var ok = Text.Collapse(r.Title ?? string.Empty) == (f.Title ?? string.Empty)
                    && r.Authors.Select(Text.Collapse).SequenceEqual(f.Authors)
                    && r.HasCover == f.Traits.Contains("cover")
                    && (f.EpubVersion is null || r.Version == f.EpubVersion);
                rows.Gate("M-" + f.Path, ok, $"manifest title={Q(f.Title)} read={Q(r.Title)}");
            }
            catch (Exception e)
            {
                rows.Gate("M-" + f.Path, false, e.GetType().Name);
            }
        }
    }
#else
    public static int EpubCheck(Args a, TextWriter w)
    {
        w.WriteLine("epub-check: EPUB not compiled into this variant");
        return 2;
    }
#endif

#if PDF
    public static int PdfCheck(Args a, TextWriter w)
    {
        var dir = a.Positional.Count > 0 ? a.Positional[0] : throw new ArgumentException("pdf-check <fixtures-dir>");
        var rows = new Rows(w);
        string P(string rel) => Path.Combine(dir, rel);

        var p1 = Pdf.Read(P("upstream/pdf/arxiv-2403.02174v1.pdf"), text: true);
        rows.Gate("X-P1", Contains(p1.FirstPageText, "Hilbert") && p1.XmpPresent && p1.Pages > 0,
            Inv($"info title={Q(p1.Title)} author={Q(p1.Author)} xmp dc:title={Q(p1.XmpTitle)} dc:creator={Q(p1.XmpCreator)} pages={p1.Pages} isbn={p1.Isbn ?? "none"}"));

        var p2 = Pdf.Read(P("derived/Event Streams in Practice.pdf"), text: true);
        rows.Observe("X-P2-xmp-library", Inv($"PdfPig GetXDocument() dc:creator={Q(p2.XmpLibraryCreator)} (UTF-8 packet decoded as Latin-1 by the library) vs. raw-bytes parse dc:creator={Q(p2.XmpCreator)}"));
        var x2 = p2.Title == "Event Streams in Practice" && p2.Author == "Jördis Ålander" && p2.XmpPresent && p2.XmpTitle == p2.Title && p2.XmpCreator == "Jördis Ålander"
            && Contains(p2.FirstPageText, "by Jördis Ålander") && Contains(p2.FirstPageText, "ISBN 978-0-00-000001-9") && p2.TextIsbns.Contains("9780000000019") && p2.Isbn == "9780000000019";
        rows.Gate("X-P2", x2, Inv($"title={Q(p2.Title)} author={Q(p2.Author)} xmp={p2.XmpPresent} dc:title={Q(p2.XmpTitle)} dc:creator={Q(p2.XmpCreator)} pages={p2.Pages} isbn={p2.Isbn}"));
        rows.Gate("I-P-X-P2", p2.TextIsbns.SequenceEqual(["9780000000019"]), "text isbns=[" + string.Join(",", p2.TextIsbns) + "]");

        var p3 = Pdf.Read(P("derived/Jane Doe - Quiet Systems (2004).pdf"), text: true);
        rows.Gate("X-P3", p3.Title is null && p3.Author is null && !p3.XmpPresent && Text.Collapse(p3.FirstPageText ?? string.Empty).StartsWith("Quiet Systems", StringComparison.Ordinal) && p3.Isbn is null,
            Inv($"title={Q(p3.Title)} author={Q(p3.Author)} xmp={p3.XmpPresent} pages={p3.Pages} page1={Q(p3.FirstPageText)}"));
        rows.Gate("I-P-X-P3", p3.TextIsbns.Count == 0 && p3.InfoIsbn is null, "no ISBN");

        var p4 = Pdf.Read(P("derived/scanned-page.pdf"), text: true);
        var noText = Text.Collapse(p4.Text ?? string.Empty).Length == 0;
        rows.Gate("X-P4", p4.Title == "Scanned Page" && p4.Author is null && p4.Pages == 1 && noText && p4.ImagesPage1 == 1 && p4.Isbn is null,
            Inv($"title={Q(p4.Title)} author={Q(p4.Author)} pages={p4.Pages} images={p4.ImagesPage1} class={(noText && p4.ImagesPage1 >= 1 ? "no-text-layer" : "text")} xmp={p4.XmpPresent} xmpTitle={Q(p4.XmpTitle)}"));

        ObserveXp5(P("upstream/pdf/arxiv-2403.02174v1.pdf"), rows);
        ObserveXp7(P("upstream/pdf/arxiv-2403.02174v1.pdf"), rows);

        var manifest = JsonFiles.Manifest(Path.Combine(dir, "manifest.json"));
        foreach (var f in manifest.Fixtures.Where(f => f.Format == "pdf"))
        {
            var r = Pdf.Read(P(f.Path), text: true);
            rows.Observe("X-P6-" + Path.GetFileName(f.Path), $"catalog /Lang={r.CatalogLang ?? "none"} xmp dc:language={r.XmpLanguage ?? "none"} producer={Q(r.Producer)}");
            using var doc = UglyToad.PdfPig.PdfDocument.Open(P(f.Path), new UglyToad.PdfPig.ParsingOptions { UseLenientParsing = true });
            var enumerated = doc.GetPages().Select(pg => pg.Number).ToList();
            var ok = enumerated.Count == doc.NumberOfPages && enumerated.SequenceEqual(Enumerable.Range(1, doc.NumberOfPages)) && r.PageNumberErrors == 0;
            rows.Gate("L-P1-" + Path.GetFileName(f.Path), ok, Inv($"locators page=1..{doc.NumberOfPages}"));
            var manifestOk = Text.Collapse(r.Title ?? string.Empty) == (f.Title ?? string.Empty) || f.Path.Contains("arxiv", StringComparison.Ordinal);
            rows.Gate("M-" + f.Path, manifestOk, $"manifest title={Q(f.Title)} read={Q(r.Title)}");
        }

        return rows.Finish();
    }

    private static void ObserveXp5(string path, Rows rows)
    {
        using var doc = UglyToad.PdfPig.PdfDocument.Open(path, new UglyToad.PdfPig.ParsingOptions { UseLenientParsing = true });
        var page = doc.GetPage(1);
        var text = Text.Collapse(page.Text);
        var words = page.GetWords().Select(x => x.Text).ToList();
        var order = Text.Collapse(UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor.ContentOrderTextExtractor.GetText(page));
        rows.Observe("X-P5", Inv($"page.Text chars={text.Length} words={Text.CountWords(text)} 'On Hilbert'={text.Contains("On Hilbert", StringComparison.Ordinal)}; GetWords count={words.Count} joined 'On Hilbert'={string.Join(" ", words).Contains("On Hilbert", StringComparison.Ordinal)}; ContentOrderTextExtractor chars={order.Length} words={Text.CountWords(order)} 'On Hilbert'={order.Contains("On Hilbert", StringComparison.Ordinal)}"));
    }

    private static void ObserveXp7(string path, Rows rows)
    {
        var a = Pdf.Read(path, text: true, lenient: true);
        var b = Pdf.Read(path, text: true, lenient: false);
        var same = a.Title == b.Title && a.Author == b.Author && a.Pages == b.Pages && a.Text == b.Text;
        rows.Observe("X-P7", Inv($"UseLenientParsing false vs true identical output={same} (pages {b.Pages}/{a.Pages}, text chars {b.TextChars}/{a.TextChars})"));
    }
#else
    public static int PdfCheck(Args a, TextWriter w)
    {
        w.WriteLine("pdf-check: PDF not compiled into this variant");
        return 2;
    }
#endif
}
