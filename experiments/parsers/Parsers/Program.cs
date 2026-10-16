using System.Runtime.InteropServices;
using System.Text;

namespace Proto;

internal static class Program
{
    private static int Main(string[] args)
    {
        Console.OutputEncoding = new UTF8Encoding(false);
        try
        {
            return Run(args, Console.Out);
        }
        catch (Exception e)
        {
            Console.Error.WriteLine("error: " + e);
            return 1;
        }
    }

    private static int Run(string[] args, TextWriter o)
    {
        if (args.Length == 0)
        {
            o.WriteLine("commands: info selfcheck isbn-check epub-check pdf-check mutate robust robust-one meta extract dump dump-one verify bench corpus");
            return 2;
        }

        var a = new Args(args, 1);
        return args[0] switch
        {
            "info" => Info(o),
            "selfcheck" => SelfChecks(a, o),
            "isbn-check" => Isbn.Check(o),
            "epub-check" => Checks.EpubCheck(a, o),
            "pdf-check" => Checks.PdfCheck(a, o),
            "mutate" => Mutations.Run(a, o),
            "robust" => Mutations.Robust(a, o),
            "robust-one" => Mutations.RobustOne(a, o),
            "meta" => Bench.Meta(a, o),
            "extract" => Bench.Extract(a, o),
            "dump" => Dump.Run(a, o),
            "dump-one" => DumpOne(a, o),
            "verify" => Bench.Verify(a, o),
            "bench" => Bench.Run(a, o),
            "corpus" => Bench.Corpus(a, o),
            _ => Unknown(args[0], o),
        };
    }

    private static int DumpOne(Args a, TextWriter o)
    {
        if (a.Has("--codepages"))
        {
            Codepages.Enable();
        }

        o.WriteLine(Dump.One(a.Positional[0]));
        return 0;
    }

    private static int Info(TextWriter o)
    {
        var variant =
#if EPUB && PDF
            "all";
#elif EPUB
            "epub";
#elif PDF
            "pdf";
#else
            "none";
#endif
        Out.Kv(o, "info", ("variant", variant), ("rid", RuntimeInformation.RuntimeIdentifier), ("os", RuntimeInformation.OSDescription), ("arch", RuntimeInformation.ProcessArchitecture),
            ("framework", RuntimeInformation.FrameworkDescription), ("cpus", Environment.ProcessorCount),
            ("codepages", Codepages.Compiled ? "compiled" : "not-compiled"));
#if EPUB
        Out.Kv(o, "info-lib", ("name", "VersOne.Epub"), ("version", typeof(VersOne.Epub.EpubReader).Assembly.GetName().Version));
#endif
#if PDF
        Out.Kv(o, "info-lib", ("name", "PdfPig"), ("version", typeof(UglyToad.PdfPig.PdfDocument).Assembly.GetName().Version));
#endif
        return 0;
    }

    private static int SelfChecks(Args a, TextWriter o)
    {
        var failures = SelfCheck.Run(o);
#if EPUB
        failures += Covers.SelfCheck(o);
#endif
        if (a.Positional.Count > 0)
        {
            var fixtures = a.Positional[0];
            var tmp = Path.Combine(Path.GetTempPath(), "shelf-spike-parsers", Guid.NewGuid().ToString("N"));
            try
            {
                string Hashes(string sub)
                {
                    var sw = new StringWriter();
                    Mutations.Run(new Args([fixtures, Path.Combine(tmp, sub)], 0), sw);
                    return sw.ToString();
                }

                var same = Hashes("a") == Hashes("b");
                o.WriteLine(same ? "pass selfcheck:mutate-twice-identical" : "fail selfcheck:mutate-twice-identical");
                if (!same)
                {
                    failures++;
                }
            }
            finally
            {
                Directory.Delete(tmp, true);
            }
        }

        o.WriteLine(failures == 0 ? "selfcheck: ok" : $"selfcheck: {failures} failures");
        return failures == 0 ? 0 : 1;
    }

    private static int Unknown(string cmd, TextWriter o)
    {
        o.WriteLine("unknown command: " + cmd);
        return 2;
    }
}
