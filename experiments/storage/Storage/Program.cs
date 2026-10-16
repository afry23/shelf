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
            o.WriteLine("commands: info fts-check load search fts-bench wal-writer wal-reader wal-hold wal-try vec-gen vec-load vec-check ext-try knn knn-bench");
            return 2;
        }

        var a = new Args(args, 1);
        return args[0] switch
        {
            "info" => Fts.Info(o),
            "fts-check" => Fts.Check(o),
            "load" => Data.Load(a, o),
            "search" => Fts.Search(a, o),
            "fts-bench" => Fts.Bench(a, o),
            "wal-writer" => Wal.Writer(a, o),
            "wal-reader" => Wal.Reader(a, o),
            "wal-hold" => Wal.Hold(a, o),
            "wal-try" => Wal.Try(a, o),
            "vec-gen" => VecData.Gen(a, o),
            "vec-load" => VecData.VecLoad(a, o),
            "vec-check" => Vec.Check(a, o),
            "ext-try" => Vec.ExtTry(a, o),
            "knn" => VecData.Knn(a, o),
            "knn-bench" => VecData.KnnBench(a, o),
            _ => Unknown(args[0], o),
        };
    }

    private static int Unknown(string cmd, TextWriter o)
    {
        o.WriteLine("unknown command: " + cmd);
        return 2;
    }
}
