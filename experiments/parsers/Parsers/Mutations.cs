using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;

namespace Proto;

public static partial class Mutations
{
    private static readonly DateTimeOffset FixedTime = new(2000, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static List<(string Name, byte[] Data)> ReadZip(string path)
    {
        using var zip = ZipFile.OpenRead(path);
        var list = new List<(string, byte[])>();
        foreach (var e in zip.Entries)
        {
            using var s = e.Open();
            using var ms = new MemoryStream();
            s.CopyTo(ms);
            list.Add((e.FullName, ms.ToArray()));
        }

        return list;
    }

    private static void WriteZip(string path, IEnumerable<(string Name, byte[] Data)> entries, CompressionLevel level = CompressionLevel.NoCompression)
    {
        using var fs = File.Create(path);
        using var zip = new ZipArchive(fs, ZipArchiveMode.Create);
        foreach (var (name, data) in entries)
        {
            var e = zip.CreateEntry(name, level);
            e.LastWriteTime = FixedTime;
            using var s = e.Open();
            s.Write(data);
        }
    }

    private static byte[] U8(string s) => new UTF8Encoding(false).GetBytes(s);

    private static string L1(byte[] b) => Encoding.Latin1.GetString(b);

    private static byte[] L1(string s) => Encoding.Latin1.GetBytes(s);

    [GeneratedRegex("full-path=\"[^\"]*\"")]
    private static partial Regex FullPath();

    [GeneratedRegex("<dc:title([^>]*)>[^<]*</dc:title>")]
    private static partial Regex DcTitle();

    [GeneratedRegex("<\\?xml[^>]*\\?>")]
    private static partial Regex XmlDecl();

    [GeneratedRegex("/Kids \\[[^\\]]*\\]")]
    private static partial Regex Kids();

    [GeneratedRegex("<< /Length (\\d+) >>\\nstream\\n")]
    private static partial Regex ContentStream();

    private static string OpfPath(List<(string Name, byte[] Data)> zip)
    {
        var container = L1(zip.Single(e => e.Name == "META-INF/container.xml").Data);
        return Regex.Match(container, "full-path=\"([^\"]+)\"").Groups[1].Value;
    }

    private static string WithDoctype(string opf, string doctype)
    {
        var m = XmlDecl().Match(opf);
        return m.Success ? opf.Insert(m.Index + m.Length, "\n" + doctype) : doctype + "\n" + opf;
    }

    /// <summary>Writes the R-row inputs (deterministic) into outDir; prints name and SHA-256 per file.</summary>
    public static int Run(Args a, TextWriter w)
    {
        var fixtures = a.Positional.Count > 0 ? a.Positional[0] : throw new ArgumentException("mutate <fixtures-dir> <out-dir>");
        var outDir = a.Positional.Count > 1 ? a.Positional[1] : throw new ArgumentException("mutate <fixtures-dir> <out-dir>");
        Directory.CreateDirectory(outDir);
        string F(string rel) => Path.Combine(fixtures, rel);
        string O(string name) => Path.Combine(outDir, name);

        // EPUB
        File.Copy(F("derived/malformed-opf.epub"), O("R-E1.epub"), true);
        File.Copy(F("derived/not-a-zip.epub"), O("R-E2.epub"), true);
        File.WriteAllBytes(O("R-E3.epub"), []);
        var series = File.ReadAllBytes(F("derived/epub3-isbn-series.epub"));
        File.WriteAllBytes(O("R-E4.epub"), series[..(series.Length / 2)]);
        var baseEntries = ReadZip(F("derived/epub3-isbn-series.epub"));
        var opfName = OpfPath(baseEntries);
        WriteZip(O("R-E5.epub"), baseEntries.Where(e => e.Name != "META-INF/container.xml"));
        WriteZip(O("R-E6.epub"), baseEntries.Select(e => e.Name == "META-INF/container.xml"
            ? (e.Name, U8(FullPath().Replace(L1(e.Data), "full-path=\"OEBPS/missing.opf\"")))
            : e));
        WriteZip(O("R-E7a.epub"), baseEntries.Select(e => e.Name == opfName
            ? (e.Name, U8(L1(e.Data).Replace("</spine>", "<itemref idref=\"ghost\"/></spine>", StringComparison.Ordinal)))
            : e));
        WriteZip(O("R-E7b.epub"), baseEntries.Where(e => e.Name != "OEBPS/chapter-02.xhtml"));
        WriteZip(O("R-E8.epub"), baseEntries.Select(e => e.Name == opfName
            ? (e.Name, U8(DcTitle().Replace(WithDoctype(Encoding.UTF8.GetString(e.Data), "<!DOCTYPE package [<!ENTITY x SYSTEM \"file:///etc/hosts\">]>"), "<dc:title$1>&x;</dc:title>")))
            : e));
        var laughs = new StringBuilder("<!DOCTYPE package [<!ENTITY a0 \"lol\">");
        for (var i = 1; i <= 9; i++)
        {
            laughs.Append("<!ENTITY a").Append(i).Append(" \"").Append(string.Concat(Enumerable.Repeat("&a" + (i - 1) + ";", 10))).Append("\">");
        }

        laughs.Append("]>");
        WriteZip(O("R-E9.epub"), baseEntries.Select(e => e.Name == opfName
            ? (e.Name, U8(DcTitle().Replace(WithDoctype(Encoding.UTF8.GetString(e.Data), laughs.ToString()), "<dc:title$1>&a9;</dc:title>")))
            : e));
        const string chapter = "OEBPS/chapter-01.xhtml";
        byte[] Doc(string decl, string body) => L1(decl + "\n<!DOCTYPE html PUBLIC \"-//W3C//DTD XHTML 1.1//EN\" \"http://www.w3.org/TR/xhtml11/DTD/xhtml11.dtd\">\n<html xmlns=\"http://www.w3.org/1999/xhtml\"><head><title>t</title></head><body>" + body + "</body></html>");
        WriteZip(O("R-E10.epub"), baseEntries.Select(e => e.Name == chapter ? (e.Name, Doc("<?xml version=\"1.0\" encoding=\"UTF-8\"?>", "<p>Alpha&nbsp;Beta</p>")) : e));
        WriteZip(O("R-E11.epub"), baseEntries.Select(e => e.Name == chapter ? (e.Name, Doc("<?xml version=\"1.0\" encoding=\"windows-1252\"?>", "<p>Käse</p>")) : e));
        WriteZip(O("R-E12.epub"), baseEntries.Select(e => e.Name == chapter
            ? (e.Name, U8("<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<html xmlns=\"http://www.w3.org/1999/xhtml\"><head><title>t</title></head><body>" + string.Concat(Enumerable.Repeat("<div>", 10_000)) + "x" + string.Concat(Enumerable.Repeat("</div>", 10_000)) + "</body></html>"))
            : e));
        WriteBomb(O("R-E13.epub"), baseEntries, chapter);
        // R-E14: OPF with a public DOCTYPE and no entities (typical of real EPUB 2 packages) must still parse.
        WriteZip(O("R-E14.epub"), baseEntries.Select(e => e.Name == opfName
            ? (e.Name, U8(WithDoctype(Encoding.UTF8.GetString(e.Data), "<!DOCTYPE package PUBLIC \"-//IDPF//DTD OPF 2.0//EN\" \"http://www.idpf.org/dtds/2007/opf20.dtd\">")))
            : e));

        // PDF
        File.WriteAllBytes(O("R-P1.pdf"), []);
        File.Copy(F("derived/not-a-zip.epub"), O("R-P2.pdf"), true);
        var arxiv = File.ReadAllBytes(F("upstream/pdf/arxiv-2403.02174v1.pdf"));
        File.WriteAllBytes(O("R-P3.pdf"), arxiv[..(arxiv.Length / 2)]);
        var p2 = L1(File.ReadAllBytes(F("derived/Event Streams in Practice.pdf")));
        var sx = p2.LastIndexOf("startxref", StringComparison.Ordinal);
        File.WriteAllBytes(O("R-P4.pdf"), L1(p2[..sx] + "startxref\n0\n%%EOF\n"));
        var p3 = L1(File.ReadAllBytes(F("derived/Jane Doe - Quiet Systems (2004).pdf")));
        var kids = Kids().Match(p3);
        var loop = "/Kids [2 0 R" + new string(' ', Math.Max(0, kids.Length - "/Kids [2 0 R]".Length)) + "]";
        File.WriteAllBytes(O("R-P5.pdf"), L1(p3.Remove(kids.Index, kids.Length).Insert(kids.Index, loop)));
        File.WriteAllBytes(O("R-P6.pdf"), L1(ContentStream().Replace(p2, "<< /Length 2147483647 >>\nstream\n", 1)));
        var cs = ContentStream().Match(p2);
        var end = p2.IndexOf("endstream", cs.Index, StringComparison.Ordinal);
        var rnd = new StringBuilder();
        ulong x = 0x9E3779B97F4A7C15UL;
        for (var i = 0; i < 64; i++)
        {
            x ^= x << 13;
            x ^= x >> 7;
            x ^= x << 17;
            rnd.Append((char)(x & 0xFF));
        }

        File.WriteAllBytes(O("R-P7.pdf"), L1(p2[..cs.Index] + "<< /Length 64 /Filter /FlateDecode >>\nstream\n" + rnd + "\n" + p2[end..]));
        File.WriteAllBytes(O("R-P8.pdf"), DeepPdf());

        foreach (var f in Directory.EnumerateFiles(outDir).OrderBy(f => f, StringComparer.Ordinal))
        {
            w.WriteLine(Path.GetFileName(f) + " " + Text.Sha256Hex(File.ReadAllBytes(f)) + " " + new FileInfo(f).Length);
        }

        return 0;
    }

    private static void WriteBomb(string path, List<(string Name, byte[] Data)> baseEntries, string chapter)
    {
        using var fs = File.Create(path);
        using var zip = new ZipArchive(fs, ZipArchiveMode.Create);
        foreach (var (name, data) in baseEntries)
        {
            var e = zip.CreateEntry(name, name == chapter ? CompressionLevel.SmallestSize : CompressionLevel.NoCompression);
            e.LastWriteTime = FixedTime;
            using var s = e.Open();
            if (name == chapter)
            {
                var zeros = new byte[1 << 20];
                for (var i = 0; i < 256; i++)
                {
                    s.Write(zeros);
                }
            }
            else
            {
                s.Write(data);
            }
        }
    }

    private static byte[] DeepPdf()
    {
        var sb = new StringBuilder("%PDF-1.4\n");
        var offsets = new List<int>();
        void Obj(string body)
        {
            offsets.Add(sb.Length);
            sb.Append(offsets.Count).Append(" 0 obj\n").Append(body).Append("\nendobj\n");
        }

        Obj("<< /Type /Catalog /Pages 2 0 R /Deep " + new string('[', 100_000) + new string(']', 100_000) + " >>");
        Obj("<< /Type /Pages /Count 1 /Kids [3 0 R] >>");
        Obj("<< /Type /Page /Parent 2 0 R /MediaBox [0 0 100 100] >>");
        var xref = sb.Length;
        sb.Append("xref\n0 4\n0000000000 65535 f \n");
        foreach (var o in offsets)
        {
            sb.Append(o.ToString("D10", System.Globalization.CultureInfo.InvariantCulture)).Append(" 00000 n \n");
        }

        sb.Append("trailer\n<< /Size 4 /Root 1 0 R >>\nstartxref\n").Append(xref).Append("\n%%EOF\n");
        return L1(sb.ToString());
    }

    private sealed record RRow(bool Gate, bool MustError);

    private static readonly Dictionary<string, RRow> RRows = new(StringComparer.Ordinal)
    {
        ["R-E1"] = new(true, true), ["R-E2"] = new(true, true), ["R-E3"] = new(true, true), ["R-E4"] = new(true, true),
        ["R-E5"] = new(true, true), ["R-E6"] = new(true, true), ["R-E7a"] = new(true, false), ["R-E7b"] = new(true, false),
        ["R-E8"] = new(true, false), ["R-E9"] = new(true, false), ["R-E10"] = new(false, false), ["R-E11"] = new(false, false),
        ["R-E12"] = new(false, false), ["R-E13"] = new(false, false), ["R-E14"] = new(false, false),
        ["R-P1"] = new(true, true), ["R-P2"] = new(true, true), ["R-P3"] = new(true, false), ["R-P4"] = new(true, false),
        ["R-P5"] = new(true, false), ["R-P6"] = new(true, false), ["R-P7"] = new(true, false), ["R-P8"] = new(false, false),
    };

    /// <summary>True for R-rows that are observations only (not gated); the isolated differential skips them (their outcome depends on memory and time).</summary>
    public static bool IsObservation(string fileName) => RRows.TryGetValue(Path.GetFileNameWithoutExtension(fileName), out var r) && !r.Gate;

    private const long RssLimit = 512L * 1024 * 1024;
    private const int TimeoutMs = 10_000;

    /// <summary>robust &lt;dir&gt;: every input in a child process (10 s kill timeout).</summary>
    public static int Robust(Args a, TextWriter w)
    {
        var dir = a.Positional.Count > 0 ? a.Positional[0] : throw new ArgumentException("robust <dir>");
        var rows = new Rows(w);
        foreach (var f in Directory.EnumerateFiles(dir).OrderBy(f => f, StringComparer.Ordinal))
        {
            var id = Path.GetFileNameWithoutExtension(f);
            if (!RRows.TryGetValue(id, out var spec))
            {
                continue;
            }

            var args = new List<string> { "robust-one", f };
            if (a.Has("--codepages"))
            {
                args.Add("--codepages");
            }

            var (code, stdout, ms, timedOut) = Procs.RunSelf([.. args], TimeoutMs);
            var line = stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? string.Empty;
            var rss = ParseRss(line);
            var detail = timedOut ? $"timeout after {TimeoutMs} ms" : $"exit={code} wall={ms:F0}ms {line}";
            if (!spec.Gate)
            {
                rows.Observe(id, detail);
                continue;
            }

            var controlled = !timedOut && (code == 0 || code == 3);
            var ok = controlled && ms <= TimeoutMs && (rss < 0 || rss < RssLimit);
            if (spec.MustError)
            {
                ok &= code == 3;
            }

            if (id == "R-E8" && controlled && code == 0)
            {
                ok &= !line.Contains("localhost", StringComparison.OrdinalIgnoreCase);
            }

            rows.Gate(id, ok, detail.Replace("\r", string.Empty));
        }

        return rows.Finish();
    }

    private static long ParseRss(string line)
    {
        var m = Regex.Match(line, @"peakRss=(\d+)");
        return m.Success ? long.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture) : -1;
    }

    /// <summary>robust-one &lt;file&gt;: exit 0 = ok, 3 = controlled error; anything else is a crash.</summary>
    public static int RobustOne(Args a, TextWriter w)
    {
        var file = a.Positional[0];
        if (a.Has("--codepages"))
        {
            w.WriteLine("codepages=" + (Codepages.Enable() ? "registered" : "not-compiled"));
        }

        var clock = new Clock();
        try
        {
            string summary;
#if EPUB
            if (file.EndsWith(".epub", StringComparison.OrdinalIgnoreCase))
            {
                var r = Epub.Read(file, text: true, cover: true, variantA: true, keepText: false);
                summary = $"ok epub spine={r.Spine.Count} chars={r.TextChars} title='{Text.First(Text.Collapse(r.Title ?? string.Empty), 60)}' xhtmlA={r.XhtmlAOk}/{r.Spine.Count} xhtmlAErrors=[{string.Join(",", r.XhtmlAErrors.Distinct())}] xhtmlAHasUmlautA={r.XhtmlAHasUmlautA}";
            }
            else
#endif
#if PDF
            if (file.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            {
                var r = Pdf.Read(file, text: true, keepText: false);
                summary = $"ok pdf pages={r.Pages} chars={r.TextChars}";
            }
            else
#endif
            {
                summary = "ok skipped";
            }

            w.WriteLine($"{summary} ms={clock.Ms:F0} peakRss={Procs.PeakRssBytes()}");
            return 0;
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            var (kind, type) = Errors.Classify(e);
            var inner = e.InnerException is { } ie ? " inner=" + ie.GetType().Name + ":" + Text.First(Text.Collapse(ie.Message), 140).Replace(' ', '_') : string.Empty;
            w.WriteLine($"error kind={kind} type={type}{inner} ms={clock.Ms:F0} peakRss={Procs.PeakRssBytes()}");
            return 3;
        }
        catch (OutOfMemoryException)
        {
            w.WriteLine($"error kind=memory type=System.OutOfMemoryException ms={clock.Ms:F0}");
            return 3;
        }
    }
}
