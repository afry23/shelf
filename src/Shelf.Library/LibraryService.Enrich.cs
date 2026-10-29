using Shelf.Core.Model;
using Shelf.Index;

namespace Shelf.Library;

public sealed partial class LibraryService
{
    private static readonly string[] SourceNames = ["openlibrary", "google", "crossref", "arxiv"];

    /// <summary>
    /// Asks the online sources what they know about a work and shows what would change; nothing is written (F-IMP-6). Several
    /// matching books are returned as candidates, not as an error: the caller chooses one with <paramref name="pick"/>.
    /// </summary>
    /// <exception cref="LibraryException">The work is unknown, an argument is unusable, or no source knows the book (<see cref="LibraryErrorKind.Conflict"/>).</exception>
    public EnrichPreviewResult EnrichPreview(string id, string? source, bool overwrite, int? pick, CancellationToken cancellation)
    {
        var work = Find(id);
        try
        {
            var preview = Lookup(work, source, overwrite, pick, cancellation);
            return new EnrichPreviewResult(
                work.Id.ToString(),
                preview.Source.Name,
                preview.Match,
                [.. preview.Written.Select(c => new EnrichChange(c.Field, c.Action.ToString().ToLowerInvariant(), c.Current, c.Online))],
                [.. preview.Changes.Where(c => !c.IsWritten).Select(c => new EnrichKept(c.Field, c.Current, c.Online))],
                [],
                [.. preview.Warnings]);
        }
        catch (EnrichmentException e) when (e.Candidates.Count > 1 && pick is null)
        {
            return new EnrichPreviewResult(
                work.Id.ToString(),
                null,
                null,
                [],
                [],
                [.. e.Candidates.Select((b, i) => new EnrichCandidate(i + 1, b.Title, [.. b.Authors], b.Publisher, b.Year, b.Isbn13))],
                [.. e.Warnings]);
        }
        catch (EnrichmentException e)
        {
            throw new LibraryException(e.Message, LibraryErrorKind.Conflict);
        }
    }

    /// <summary>Writes what <see cref="EnrichPreview"/> shows, and saves the cover when the work has none and <paramref name="cover"/> is set.</summary>
    /// <exception cref="LibraryException">As <see cref="EnrichPreview"/>; several matching books without a <c>pick</c> are a conflict.</exception>
    public EnrichApplyResult EnrichApply(string id, string? source, bool overwrite, int? pick, bool cover, CancellationToken cancellation)
    {
        var work = Find(id);
        try
        {
            var preview = Lookup(work, source, overwrite, pick, cancellation);
            EnrichmentResult result;
            lock (_writeGate)
            {
                result = EnrichmentService.Apply(root, work, preview, cover, cancellation);
            }

            return new EnrichApplyResult(work.Id.ToString(), preview.Source.Name, [.. result.Fields], result.Cover, [.. preview.Warnings, .. result.Warnings]);
        }
        catch (EnrichmentException e)
        {
            throw new LibraryException(e.Message, LibraryErrorKind.Conflict);
        }
        catch (Exception e) when (e is LibraryEditException or SidecarException or IndexException)
        {
            throw new LibraryException(e.Message);
        }
    }

    private EnrichmentPreview Lookup(LibraryWork work, string? source, bool overwrite, int? pick, CancellationToken cancellation)
    {
        if (source is not null && !SourceNames.Contains(source))
        {
            throw new LibraryException("source must be openlibrary, google, crossref or arxiv");
        }

        if (pick is < 1)
        {
            throw new LibraryException("pick must be 1 or greater");
        }

        var sources = (metadataSources ?? []).Where(s => source is null || s.Name == source).ToList();
        if (sources.Count == 0)
        {
            throw new LibraryException("no online source is available", LibraryErrorKind.Conflict);
        }

        return EnrichmentService.Preview(LibraryEditor.Read(root, work), sources, overwrite, pick, cancellation);
    }
}
