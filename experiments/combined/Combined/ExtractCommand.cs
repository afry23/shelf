using UglyToad.PdfPig;
using VersOne.Epub;

namespace Proto;

internal static class ExtractCommand
{
    public static async Task<int> RunAsync(string path, TextWriter stdout, TextWriter stderr)
    {
        if (!File.Exists(path))
        {
            stderr.WriteLine($"file not found: {path}");
            return 1;
        }

        if (path.EndsWith(".epub", StringComparison.OrdinalIgnoreCase))
        {
            using var book = await EpubReader.OpenBookAsync(path).ConfigureAwait(false);
            var meta = book.Schema.Package.Metadata;
            stdout.WriteLine("format=epub");
            stdout.WriteLine("title=" + book.Title);
            stdout.WriteLine("authors=" + string.Join("; ", meta.Creators.Select(c => c.Creator)));
            stdout.WriteLine("language=" + (meta.Languages.Count > 0 ? meta.Languages[0].Language : ""));
            return 0;
        }

        if (path.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            using var doc = PdfDocument.Open(path);
            stdout.WriteLine("format=pdf");
            stdout.WriteLine("title=" + doc.Information.Title);
            stdout.WriteLine("author=" + doc.Information.Author);
            stdout.WriteLine("pages=" + doc.NumberOfPages);
            return 0;
        }

        stderr.WriteLine($"unsupported file type: {path}");
        return 1;
    }
}
