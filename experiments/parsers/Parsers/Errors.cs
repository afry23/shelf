using System.IO.Compression;
using System.Text;
using System.Xml;

namespace Proto;

public static class Errors
{
    public static (string Kind, string Type) Classify(Exception e)
    {
        var t = e.GetType().FullName ?? e.GetType().Name;
        var kind = e switch
        {
            InvalidDataException => "not-a-container",
            XmlException => "bad-xml",
            OutOfMemoryException => "memory",
            FileNotFoundException or DirectoryNotFoundException => "io",
            IOException => "io",
            NotSupportedException => "unsupported",
            _ when t.StartsWith("VersOne.Epub.", StringComparison.Ordinal) => "bad-epub",
            _ when t.EndsWith("PdfDocumentEncryptedException", StringComparison.Ordinal) => "encrypted",
            _ when t.EndsWith("PdfDocumentFormatException", StringComparison.Ordinal) || t.EndsWith("PdfDocumentStackDepthException", StringComparison.Ordinal) => "bad-pdf",
            _ => "other",
        };
        return (kind, t);
    }
}

public static class Codepages
{
#if CODEPAGES
    public const bool Compiled = true;
#else
    public const bool Compiled = false;
#endif

    public static bool Enable()
    {
#if CODEPAGES
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return true;
#else
        return false;
#endif
    }
}

public static class Dump
{
    public static string OutcomeError(Exception e)
    {
        var (kind, type) = Errors.Classify(e);
        return new J().Set("outcome", "error").Set("kind", kind).Set("type", type).ToJson();
    }

    private static string? N(string? s) => s is null ? null : Text.Collapse(s);

    private static J TextBlock(string? text)
    {
        var t = Text.Collapse(text ?? string.Empty);
        return new J().Set("len", (long)t.Length).Set("sha256", Text.Sha256Hex(t)).Set("head", Text.First(t, 200));
    }

    public static string One(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        try
        {
#if EPUB
            if (ext == ".epub")
            {
                var r = Epub.Read(path, text: true, cover: true, variantA: true);
                return new J()
                    .Set("outcome", "ok").Set("format", "epub").Set("version", r.Version).Set("title", N(r.Title))
                    .Set("authors", J.Arr(r.Authors.Select(N))).Set("authorDetails", J.Arr(r.AuthorDetails.Select(N)))
                    .Set("language", r.Language).Set("identifiers", J.Arr(r.Identifiers)).Set("opfIsbn", r.OpfIsbn).Set("textIsbns", J.Arr(r.TextIsbns))
                    .Set("series", r.SeriesName).Set("seriesIndex", r.SeriesIndex)
                    .Set("cover", new J().Set("has", r.HasCover).Set("rule", r.CoverRule).Set("magic", r.CoverMagic).Set("declared", r.CoverDeclared)
                        .Set("bytes", r.CoverBytes).Set("sha256", r.CoverSha).Set("w", r.CoverSize?.W).Set("h", r.CoverSize?.H))
                    .Set("spine", r.Spine.Select(s => (object?)new J().Set("idref", s.Idref).Set("href", s.Href).Set("linear", s.Linear).Set("chars", s.Chars).Set("cfi", s.Cfi)).ToList())
                    .Set("nav", new J().Set("entries", (long)r.NavEntries).Set("mapped", (long)r.NavMapped))
                    .Set("text", TextBlock(r.Text)).Set("words", r.Words)
                    .Set("xhtmlA", new J().Set("ok", (long)r.XhtmlAOk).Set("errors", J.Arr(r.XhtmlAErrors)))
                    .ToJson();
            }
#endif
#if PDF
            if (ext == ".pdf")
            {
                var r = Pdf.Read(path, text: true);
                return new J()
                    .Set("outcome", "ok").Set("format", "pdf").Set("title", N(r.Title)).Set("author", N(r.Author)).Set("subject", N(r.Subject)).Set("keywords", N(r.Keywords))
                    .Set("producer", N(r.Producer)).Set("pages", (long)r.Pages).Set("xmp", r.XmpPresent).Set("xmpTitle", N(r.XmpTitle)).Set("xmpCreator", N(r.XmpCreator))
                    .Set("xmpLanguage", r.XmpLanguage).Set("catalogLang", r.CatalogLang).Set("imagesPage1", (long)r.ImagesPage1).Set("encrypted", r.Encrypted)
                    .Set("text", TextBlock(r.Text)).Set("words", r.Words).Set("infoIsbn", r.InfoIsbn).Set("textIsbns", J.Arr(r.TextIsbns))
                    .Set("pageNumberErrors", (long)r.PageNumberErrors)
                    .ToJson();
            }
#endif
            return new J().Set("outcome", "skipped").ToJson();
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            return OutcomeError(e);
        }
    }

    /// <summary>dump &lt;file|dir&gt; [--isolate]: one canonical line per input. --isolate runs each input in a child process.</summary>
    public static int Run(Args a, TextWriter w)
    {
        var target = a.Positional.Count > 0 ? a.Positional[0] : throw new ArgumentException("dump <file|dir>");
        if (a.Has("--codepages"))
        {
            w.WriteLine("codepages=" + (Codepages.Enable() ? "registered" : "not-compiled"));
        }

        var files = File.Exists(target)
            ? [target]
            : Directory.EnumerateFiles(target, "*", SearchOption.AllDirectories)
                .Where(f => f.EndsWith(".epub", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                .OrderBy(f => f, StringComparer.Ordinal).ToArray();
        var root = File.Exists(target) ? Path.GetDirectoryName(Path.GetFullPath(target))! : Path.GetFullPath(target);
        foreach (var f in files)
        {
            var name = Path.GetRelativePath(root, Path.GetFullPath(f)).Replace('\\', '/');
            string json;
            if (a.Has("--isolate") && Mutations.IsObservation(f))
            {
                continue;
            }

            if (a.Has("--isolate"))
            {
                var args = new List<string> { "dump-one", f };
                if (a.Has("--codepages"))
                {
                    args.Add("--codepages");
                }

                var (code, stdout, _, timedOut) = Procs.RunSelf([.. args], 60_000);
                json = timedOut ? new J().Set("outcome", "timeout").ToJson()
                    : code == 0 && stdout.Trim().StartsWith('{') ? stdout.Trim()
                    : new J().Set("outcome", "crash").ToJson();
            }
            else
            {
                json = One(f);
            }

            w.WriteLine(name + "\t" + json);
        }

        return 0;
    }
}
