using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Proto;

public static partial class Bench
{
    private static string Inv(FormattableString s) => FormattableString.Invariant(s);

    public static int Meta(Args a, TextWriter w)
    {
        var file = a.Positional[0];
        try
        {
#if EPUB
            if (file.EndsWith(".epub", StringComparison.OrdinalIgnoreCase))
            {
                var r = Epub.Read(file, text: false, cover: false);
                Out.Kv(w, "meta", ("format", "epub"), ("version", r.Version), ("title", r.Title), ("authors", string.Join("; ", r.Authors)), ("spine", r.Spine.Count), ("language", r.Language));
                return 0;
            }
#endif
#if PDF
            if (file.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            {
                var r = Pdf.Read(file, text: false);
                Out.Kv(w, "meta", ("format", "pdf"), ("title", r.Title), ("authors", r.Author), ("pages", r.Pages), ("xmp", r.XmpPresent));
                return 0;
            }
#endif
            w.WriteLine("meta: unsupported file " + file);
            return 2;
        }
        catch (Exception e)
        {
            var (kind, type) = Errors.Classify(e);
            w.WriteLine($"error kind={kind} type={type}");
            return 3;
        }
    }

    public static int Extract(Args a, TextWriter w)
    {
        var file = a.Positional[0];
        var text = a.Has("--text");
        var clock = new Clock();
        try
        {
#if EPUB
            if (file.EndsWith(".epub", StringComparison.OrdinalIgnoreCase))
            {
                var r = Epub.Read(file, text, cover: a.Has("--cover"), keepText: false);
                if (a.Get("--cover") is { } dir && r.CoverData is { } bytes)
                {
                    Directory.CreateDirectory(dir);
                    File.WriteAllBytes(Path.Combine(dir, "cover." + Covers.Extension(r.CoverMagic)), bytes);
                }

                Out.Kv(w, "extract", ("format", "epub"), ("spine", r.Spine.Count), ("chars", r.TextChars), ("words", r.Words), ("isbn", r.Isbn ?? "none"), ("cover", r.CoverMagic),
                    ("openMs", r.OpenMs.ToString("F2", CultureInfo.InvariantCulture)), ("textMs", r.TextMs.ToString("F2", CultureInfo.InvariantCulture)), ("totalMs", clock.Ms.ToString("F2", CultureInfo.InvariantCulture)), ("peakRssMb", Procs.PeakRssBytes() / (1024 * 1024)));
                return 0;
            }
#endif
#if PDF
            if (file.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            {
                var r = Pdf.Read(file, text, keepText: false);
                Out.Kv(w, "extract", ("format", "pdf"), ("pages", r.Pages), ("chars", r.TextChars), ("words", r.Words), ("isbn", r.Isbn ?? "none"),
                    ("openMs", r.OpenMs.ToString("F2", CultureInfo.InvariantCulture)), ("textMs", r.TextMs.ToString("F2", CultureInfo.InvariantCulture)), ("totalMs", clock.Ms.ToString("F2", CultureInfo.InvariantCulture)), ("peakRssMb", Procs.PeakRssBytes() / (1024 * 1024)));
                return 0;
            }
#endif
            w.WriteLine("extract: unsupported file " + file);
            return 2;
        }
        catch (Exception e)
        {
            var (kind, type) = Errors.Classify(e);
            w.WriteLine($"error kind={kind} type={type}");
            return 3;
        }
    }

    private static string Resolve(string groundTruth, string relative) =>
        Path.Combine(Path.GetDirectoryName(Path.GetFullPath(groundTruth))!, relative.Replace('/', Path.DirectorySeparatorChar));

    private static List<string> Split(string? s) => s is null ? [] : [.. s.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)];

    public static int Verify(Args a, TextWriter w)
    {
        var gtPath = a.Positional[0];
        var gt = JsonFiles.GroundTruth(gtPath);
        var works = gt.Works.ToDictionary(x => x.Key, StringComparer.Ordinal);
        var files = gt.Files.OrderBy(f => f.Path, StringComparer.Ordinal).Take(a.GetInt("--limit", int.MaxValue)).ToList();
        var failures = new ConcurrentBag<string>();
        var counters = new ConcurrentDictionary<string, int>(StringComparer.Ordinal);
        void Count(string key, bool ok)
        {
            counters.AddOrUpdate(key + ".total", 1, (_, v) => v + 1);
            if (ok)
            {
                counters.AddOrUpdate(key + ".ok", 1, (_, v) => v + 1);
            }
        }

        Parallel.ForEach(files, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, f =>
        {
            var path = Resolve(gtPath, f.Path);
            var e = f.Embedded;
            bool Check(string key, bool ok)
            {
                Count(key, ok);
                if (!ok)
                {
                    failures.Add($"{key} {f.Path}");
                }

                return ok;
            }

            try
            {
#if EPUB
                if (f.Format == "epub")
                {
                    var r = Epub.Read(path, text: true, cover: true, keepText: false);
                    Check("epub.title", Text.Collapse(r.Title ?? string.Empty) == Text.Collapse(e.Title ?? string.Empty));
                    Check("epub.authors", r.Authors.Select(Text.Collapse).SequenceEqual(e.Authors.Select(Text.Collapse), StringComparer.Ordinal));
                    Check("epub.language", r.Language == e.Language);
                    Check("epub.isbn13", r.Isbn == e.Isbn13 && r.OpfIsbn == e.Isbn13);
                    Check("epub.text", r.TextChars > 0 && r.Words > 0);
                    Check("epub.cover", r.HasCover == f.Traits.Contains("cover"));
                    Count("epub.errors", true);
                    return;
                }
#endif
#if PDF
                if (f.Format == "pdf")
                {
                    var r = Pdf.Read(path, text: true, keepText: false);
                    Check("pdf.pages", r.Pages == f.PageCount);
                    var info = f.Traits.Contains("pdf-info");
                    Check("pdf.title", info ? r.Title == e.Title : r.Title is null && e.Title is null);
                    Check("pdf.authors", info ? Split(r.Author).SequenceEqual(e.Authors, StringComparer.Ordinal) : r.Author is null && e.Authors.Count == 0);
                    if (f.Traits.Contains("pdf-xmp"))
                    {
                        Check("pdf.xmp", r.XmpPresent && r.XmpTitle == e.Title);
                    }
                    else
                    {
                        Check("pdf.xmp", !r.XmpPresent);
                    }

                    var textIsbn = f.Traits.Contains("pdf-isbn-in-text");
                    Check("pdf.isbn-text", textIsbn ? r.TextIsbns.Count > 0 && r.TextIsbns[0] == works[f.Work].Isbn13 : r.TextIsbns.Count == 0);
                    Check("pdf.isbn13", r.Isbn == e.Isbn13);
                    Count("pdf.errors", true);
                    return;
                }
#endif
            }
            catch (Exception ex)
            {
                var format = f.Format;
                Count(format + ".errors", false);
                failures.Add($"{format}.error {f.Path} {ex.GetType().Name}");
            }
        });

        var allOk = true;
        foreach (var key in counters.Keys.Where(k => k.EndsWith(".total", StringComparison.Ordinal)).Select(k => k[..^6]).OrderBy(k => k, StringComparer.Ordinal))
        {
            var total = counters[key + ".total"];
            var ok = counters.GetValueOrDefault(key + ".ok");
            w.WriteLine(Inv($"verify {key} {ok}/{total}"));
            allOk &= ok == total;
        }

        foreach (var fmt in new[] { "epub", "pdf" })
        {
            var n = files.Count(f => f.Format == fmt);
            w.WriteLine(Inv($"verify {fmt}.files {n}"));
        }

        foreach (var f in failures.Order(StringComparer.Ordinal).Take(20))
        {
            w.WriteLine("verify-failure " + f);
        }

        w.WriteLine("verify result=" + (allOk ? "pass" : "FAIL"));
        return allOk ? 0 : 1;
    }

    public static int Run(Args a, TextWriter w)
    {
        var gtPath = a.Positional[0];
        var gt = JsonFiles.GroundTruth(gtPath);
        var format = a.Get("--format") ?? "all";
        var threads = a.GetInt("--threads", 1);
        var files = gt.Files.Where(f => format == "all" || f.Format == format).OrderBy(f => f.Path, StringComparer.Ordinal).Take(a.GetInt("--limit", int.MaxValue)).ToList();
        var open = new double[files.Count];
        var meta = new double[files.Count];
        var textMs = new double[files.Count];
        var isbn = new double[files.Count];
        var total = new double[files.Count];
        var words = new long[files.Count];
        var errors = 0;
        var wall = new Clock();
        Parallel.For(0, files.Count, new ParallelOptions { MaxDegreeOfParallelism = threads }, i =>
        {
            var f = files[i];
            var path = Resolve(gtPath, f.Path);
            var c = new Clock();
            try
            {
#if EPUB
                if (f.Format == "epub")
                {
                    var r = Epub.Read(path, text: true, cover: false, keepText: false);
                    (open[i], meta[i], textMs[i], isbn[i], words[i]) = (r.OpenMs, r.MetaMs, r.TextMs, r.IsbnMs, r.Words);
                }
#endif
#if PDF
                if (f.Format == "pdf")
                {
                    var r = Pdf.Read(path, text: true, keepText: false);
                    (open[i], meta[i], textMs[i], isbn[i], words[i]) = (r.OpenMs, r.MetaMs, r.TextMs, r.IsbnMs, r.Words);
                }
#endif
            }
            catch (Exception)
            {
                Interlocked.Increment(ref errors);
            }

            total[i] = c.Ms;
        });
        var wallMs = wall.Ms;
        var bytes = files.Sum(f => f.Size);
        Out.Kv(w, "bench", ("format", format), ("threads", threads), ("files", files.Count), ("errors", errors), ("wall_s", (wallMs / 1000).ToString("F2", CultureInfo.InvariantCulture)),
            ("files_per_s", (files.Count / (wallMs / 1000)).ToString("F1", CultureInfo.InvariantCulture)), ("mb_per_s", (bytes / 1048576.0 / (wallMs / 1000)).ToString("F2", CultureInfo.InvariantCulture)),
            ("words_per_s", ((long)(words.Sum() / (wallMs / 1000))).ToString(CultureInfo.InvariantCulture)), ("peak_rss_mb", Procs.PeakRssBytes() / (1024 * 1024)), ("bytes", bytes));
        foreach (var fmt in format == "all" ? new[] { "epub", "pdf" } : [format])
        {
            var idx = Enumerable.Range(0, files.Count).Where(i => files[i].Format == fmt).ToArray();
            if (idx.Length == 0)
            {
                continue;
            }

            double[] Pick(double[] src) => [.. idx.Select(i => src[i])];
            foreach (var (name, data) in new[] { ("open", open), ("metadata", meta), ("text", textMs), ("isbn", isbn), ("total", total) })
            {
                w.WriteLine($"bench-phase format={fmt} threads={threads} phase={name} {Stats.Summary(Pick(data))}");
            }
        }

        return errors == 0 ? 0 : 1;
    }

    [GeneratedRegex(@"peakRss=(\d+)")]
    private static partial Regex RssRegex();

    [GeneratedRegex(@"pages=(\d+)")]
    private static partial Regex PagesRegex();

    /// <summary>corpus &lt;dir&gt;: aggregates only. Per-file lines carry the first 12 hex digits of the SHA-256, never names or text.</summary>
    public static int Corpus(Args a, TextWriter w)
    {
        var dir = a.Positional[0];
        var files = Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories)
            .Where(f => f.EndsWith(".epub", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            .OrderBy(f => f, StringComparer.Ordinal).ToList();
        var outcomes = new Dictionary<string, int>(StringComparer.Ordinal);
        var times = new Dictionary<string, List<double>>(StringComparer.Ordinal);
        long maxRss = 0;
        long pages = 0;
        double pdfMs = 0;
        foreach (var f in files)
        {
            var fmt = Path.GetExtension(f).TrimStart('.').ToLowerInvariant();
            var (code, stdout, ms, timedOut) = Procs.RunSelf(["robust-one", f], 30_000);
            var line = stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? string.Empty;
            var kind = timedOut ? "timeout" : code == 0 ? "ok" : code == 3 ? "error:" + Regex.Match(line, @"kind=(\S+)").Groups[1].Value : "crash";
            outcomes[fmt + " " + kind] = outcomes.GetValueOrDefault(fmt + " " + kind) + 1;
            if (!times.TryGetValue(fmt, out var list))
            {
                times[fmt] = list = [];
            }

            list.Add(ms);
            if (RssRegex().Match(line) is { Success: true } m)
            {
                maxRss = Math.Max(maxRss, long.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture));
            }

            if (fmt == "pdf" && code == 0 && PagesRegex().Match(line) is { Success: true } pm)
            {
                pages += long.Parse(pm.Groups[1].Value, CultureInfo.InvariantCulture);
                pdfMs += ms;
            }

            w.WriteLine($"corpus-file {Text.Sha256Hex(File.ReadAllBytes(f))[..12]} {fmt} {kind} {ms:F0} ms");
        }

        foreach (var (k, v) in outcomes.OrderBy(x => x.Key, StringComparer.Ordinal))
        {
            w.WriteLine($"corpus-outcome {k} {v}");
        }

        foreach (var (fmt, list) in times)
        {
            w.WriteLine($"corpus-time {fmt} {Stats.Summary([.. list])} (includes process start)");
        }

        w.WriteLine($"corpus-summary files={files.Count} peakRssMaxMb={maxRss / (1024 * 1024)} pdfPagesPerS={(pdfMs > 0 ? pages / (pdfMs / 1000) : 0):F1}");
        return 0;
    }
}
