using Microsoft.Data.Sqlite;
using Shelf.Core.Configuration;
using Shelf.Core.Model;

namespace Shelf.Index;

/// <summary>What <see cref="LibraryPapers.Fetch"/> did.</summary>
/// <param name="Source">The source that knew the paper.</param>
public sealed record FetchResult(Ulid Work, string Title, string File, string Folder, string Source, IReadOnlyList<string> Warnings);

/// <summary>Papers by hand, from the internet (F-PAP-8). Only on explicit command.</summary>
public static class LibraryPapers
{
    /// <summary>
    /// Loads the metadata and the open-access PDF of a paper and creates a work from them. Nothing is created unless both are
    /// found. The arXiv ID decides where the PDF comes from; a DOI is looked up at Crossref and has a PDF only if a source
    /// knows one.
    /// </summary>
    /// <exception cref="LibraryEditException">The paper is already in the library, no source knows it, or no PDF is found.</exception>
    public static FetchResult Fetch(string root, PaperReference reference, IReadOnlyList<IMetadataSource> sources, FetchSettings settings, CancellationToken cancellation)
    {
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentNullException.ThrowIfNull(sources);
        ArgumentNullException.ThrowIfNull(settings);

        if (WorkWith(root, reference) is { } known)
        {
            throw new LibraryEditException($"already in the library as {known}");
        }

        var warnings = new List<string>();
        var paperSources = sources.Where(s => s.ForPapers).Select(s => s.ForSettings(settings)).OfType<IMetadataSource>().ToList();
        var (book, source) = Lookup(reference, paperSources, warnings, cancellation);

        var pdf = Pdf(book with { ArxivId = book.ArxivId ?? reference.ArxivId }, paperSources, warnings, cancellation)
            ?? throw new LibraryEditException(NoPdf(book, reference, settings, warnings));

        var temp = Directory.CreateTempSubdirectory("shelf-fetch-");
        try
        {
            var file = Path.Combine(temp.FullName, (reference.ArxivId ?? reference.Doi!).Replace('/', '_') + ".pdf");
            File.WriteAllBytes(file, pdf);
            var values = new WorkValues(book.Title, book.Authors.Count > 0 ? book.Authors : null, book.Year);
            var created = LibraryFiles.CreateWork(root, file, values, move: false, temp.FullName);
            var work = WorkLookup.Resolve(root, created.Work.ToString(), temp.FullName);
            LibraryEditor.Update(root, work, s => s with
            {
                Kind = WorkKind.Paper,
                Doi = book.Doi ?? reference.Doi ?? s.Doi,
                ArxivId = book.ArxivId ?? reference.ArxivId ?? s.ArxivId,
                Venue = book.Venue ?? s.Venue,
                Abstract = book.Abstract ?? s.Abstract,
                Volume = book.Volume ?? s.Volume,
                Issue = book.Issue ?? s.Issue,
                Pages = book.Pages ?? s.Pages,
            });
            return new FetchResult(created.Work, book.Title, created.File, created.Folder, source.Name, warnings);
        }
        finally
        {
            temp.Delete(recursive: true);
        }
    }

    private static string NoPdf(OnlineBook book, PaperReference reference, FetchSettings settings, List<string> warnings)
    {
        var message = $"no open-access PDF found for '{book.Title}'; get the file yourself and add it with 'shelf create'";

        // A DOI could be looked up at Unpaywall if it were allowed to be.
        if (reference.ArxivId is null && settings.UnpaywallEmail is null)
        {
            message += "; set fetch.unpaywall_email to let shelf ask Unpaywall for a free copy";
        }

        // A source that failed may be the reason, so the user should see what it said.
        return warnings.Count > 0 ? $"{message} ({string.Join("; ", warnings)})" : message;
    }

    private static (OnlineBook Book, IMetadataSource Source) Lookup(PaperReference reference, List<IMetadataSource> sources, List<string> warnings, CancellationToken cancellation)
    {
        foreach (var source in sources)
        {
            try
            {
                var found = reference.ArxivId is { } arxiv ? source.FindByArxivId(arxiv, cancellation) : source.FindByDoi(reference.Doi!, cancellation);
                if (found.Count > 0)
                {
                    return (found[0], source);
                }
            }
            catch (MetadataSourceException e)
            {
                warnings.Add($"{source.Name}: {e.Message}");
            }
        }

        var message = sources.Count == 0 ? "no online source for papers is available"
            : warnings.Count == sources.Count ? "no source could be asked: " + string.Join("; ", warnings)
            : "no online source knows this paper" + (warnings.Count > 0 ? ": " + string.Join("; ", warnings) : string.Empty);
        throw new LibraryEditException(message);
    }

    private static byte[]? Pdf(OnlineBook book, List<IMetadataSource> sources, List<string> warnings, CancellationToken cancellation)
    {
        foreach (var source in sources)
        {
            try
            {
                if (source.FetchPdf(book, cancellation) is { } pdf)
                {
                    return pdf;
                }
            }
            catch (MetadataSourceException e)
            {
                warnings.Add($"{source.Name}: {e.Message}");
            }
        }

        return null;
    }

    private static string? WorkWith(string root, PaperReference reference)
    {
        using var connection = IndexDatabase.OpenRead(IndexDatabase.PathFor(root));
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT id FROM works WHERE (doi = $doi AND $doi IS NOT NULL) OR (arxiv_id = $arxiv AND $arxiv IS NOT NULL) LIMIT 1";
        command.Parameters.AddWithValue("$doi", (object?)reference.Doi ?? DBNull.Value);
        command.Parameters.AddWithValue("$arxiv", (object?)reference.ArxivId ?? DBNull.Value);
        return command.ExecuteScalar() as string;
    }
}
