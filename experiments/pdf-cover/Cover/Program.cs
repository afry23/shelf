using System.Diagnostics;
using System.Globalization;
using PDFtoImage.Exceptions;
using Proto;
using SkiaSharp;

return Run(args);

static int Run(string[] args)
{
    try
    {
        return args.Length == 0 ? Usage() : args[0] switch
        {
            "render" => Render(args[1], args[2], args.Length > 3 ? int.Parse(args[3], CultureInfo.InvariantCulture) : CoverRender.MaxEdge),
            "robust" => Robust(args[1]),
            "stress" => Stress(args[1], args[2]),
            "bench" => Bench(args[1], args.Length > 2 ? int.Parse(args[2], CultureInfo.InvariantCulture) : 20),
            "info" => Info(),
            _ => Usage(),
        };
    }
    catch (Exception e)
    {
        Console.Error.WriteLine($"fatal {e.GetType().FullName}: {e.Message}");
        return 1;
    }
}

static int Usage()
{
    Console.Error.WriteLine("usage: proto-cover render <pdf> <out-dir> [max-edge] | robust <pdf> | stress <pdf1> <pdf2> | bench <pdf> [runs] | info");
    return 2;
}

static string Describe(string kind, Exception e) =>
    $"error kind={kind} type={e.GetType().Name} message={e.Message.ReplaceLineEndings(" ").Split('.')[0]}";

static string KindOf(Exception e) => e switch
{
    PdfPasswordProtectedException => "encrypted",
    PdfUnsupportedSecuritySchemeException => "unsupported-security",
    PdfInvalidFormatException => "bad-pdf",
    PdfPageNotFoundException => "no-pages",
    PdfCannotOpenFileException => "cannot-open",
    PdfException => "pdf-other",
    ArgumentException or IOException => "io",
    _ => "other",
};

static string RenderLine(byte[] pdf, string? outDir, int maxEdge)
{
    using var bitmap = CoverRender.RenderFirstPage(pdf, out var page, maxEdge);
    var png = CoverRender.Encode(bitmap, SKEncodedImageFormat.Png, 100);
    var jpg = CoverRender.Encode(bitmap, SKEncodedImageFormat.Jpeg, 85);
    var webp = CoverRender.Encode(bitmap, SKEncodedImageFormat.Webp, 80);
    if (outDir is not null)
    {
        Directory.CreateDirectory(outDir);
        File.WriteAllBytes(Path.Combine(outDir, "cover.png"), png);
        File.WriteAllBytes(Path.Combine(outDir, "cover.jpg"), jpg);
        File.WriteAllBytes(Path.Combine(outDir, "cover.webp"), webp);
    }

    return string.Create(CultureInfo.InvariantCulture,
        $"page={page.Width:0.##}x{page.Height:0.##}pt size={bitmap.Width}x{bitmap.Height} png={png.Length} jpg={jpg.Length} webp={webp.Length} pixelhash={Pixels.Hash(bitmap)} stddev={Pixels.LuminanceStdDev(bitmap):0.00}");
}

static int Render(string pdfPath, string outDir, int maxEdge)
{
    Console.WriteLine(RenderLine(File.ReadAllBytes(pdfPath), outDir, maxEdge));
    return 0;
}

static int Robust(string pdfPath)
{
    try
    {
        Console.WriteLine(RenderLine(File.ReadAllBytes(pdfPath), null, CoverRender.MaxEdge));
        return 0;
    }
    catch (Exception e)
    {
        Console.WriteLine(Describe(KindOf(e), e));
        return 3;
    }
}

static int Stress(string pdf1, string pdf2)
{
    var inputs = new[] { File.ReadAllBytes(pdf1), File.ReadAllBytes(pdf2) };
    string HashOf(byte[] pdf)
    {
        using var b = CoverRender.RenderFirstPage(pdf, out _);
        return Pixels.Hash(b);
    }

    var sw = Stopwatch.StartNew();
    var expected = inputs.Select(HashOf).ToArray();
    var sequentialOne = sw.Elapsed.TotalMilliseconds;
    const int threads = 8, perThread = 5;
    var mismatches = 0;
    var exceptions = new List<string>();
    sw.Restart();
    var workers = Enumerable.Range(0, threads).Select(t => new Thread(() =>
    {
        for (var i = 0; i < perThread; i++)
        {
            var which = (t + i) % 2;
            try
            {
                if (HashOf(inputs[which]) != expected[which])
                    Interlocked.Increment(ref mismatches);
            }
            catch (Exception e)
            {
                lock (exceptions)
                    exceptions.Add($"{e.GetType().Name}: {e.Message}");
            }
        }
    })).ToArray();
    foreach (var w in workers)
        w.Start();
    foreach (var w in workers)
        w.Join();
    var parallelMs = sw.Elapsed.TotalMilliseconds;
    Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
        $"stress threads={threads} renders={threads * perThread} mismatches={mismatches} exceptions={exceptions.Count} hashes={expected[0][..12]},{expected[1][..12]}"));
    Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
        $"observed stress-wall: sequential2={sequentialOne:0.0} ms parallel{threads * perThread}={parallelMs:0.0} ms"));
    foreach (var e in exceptions.Distinct().Take(3))
        Console.WriteLine("exception " + e);
    return mismatches == 0 && exceptions.Count == 0 ? 0 : 1;
}

static int Bench(string pdfPath, int runs)
{
    var pdf = File.ReadAllBytes(pdfPath);
    var times = new List<double>();
    var first = 0.0;
    for (var i = 0; i < runs + 1; i++)
    {
        var sw = Stopwatch.StartNew();
        using (var b = CoverRender.RenderFirstPage(pdf, out _))
            _ = CoverRender.Encode(b, SKEncodedImageFormat.Jpeg, 85);
        var ms = sw.Elapsed.TotalMilliseconds;
        if (i == 0)
            first = ms;
        else
            times.Add(ms);
    }

    times.Sort();
    Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"bench {Path.GetFileName(pdfPath)} first={first:0.0} ms median={times[times.Count / 2]:0.0} ms min={times[0]:0.0} ms max={times[^1]:0.0} ms (render + JPEG encode)"));
    return 0;
}

static int Info()
{
    Console.WriteLine("PDFtoImage " + typeof(PDFtoImage.Conversion).Assembly.GetName().Version);
    Console.WriteLine("SkiaSharp " + typeof(SKBitmap).Assembly.GetName().Version);
    Console.WriteLine("base " + AppContext.BaseDirectory);
    foreach (var f in Directory.GetFiles(AppContext.BaseDirectory).OrderBy(x => x, StringComparer.Ordinal))
        Console.WriteLine($"file {Path.GetFileName(f)} {new FileInfo(f).Length}");
    return 0;
}
