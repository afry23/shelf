using Shelf.Core.Model;
using Shelf.Index;

namespace Shelf.Library;

public sealed partial class LibraryService
{
    /// <summary>
    /// The references of a work as they were loaded, and the works of the library that cite it (F-PAP-11, F-PAP-12). Nobody is asked:
    /// the list comes from <c>references.json</c> and the index.
    /// </summary>
    /// <exception cref="LibraryException">NotFound: no such work.</exception>
    public ReferencesResult References(string id)
    {
        var work = Find(id);
        try
        {
            var listing = LibraryReferences.Describe(root, work);
            return new ReferencesResult(
                work.Id.ToString(),
                work.Title,
                ReadSidecar(work).Doi,
                listing.References.Count,
                listing.InLibrary,
                listing.Source,
                listing.FetchedAt is { } fetched ? Time(fetched) : null,
                [.. listing.References.Select(Item)],
                [.. listing.CitedBy.Select(c => new CitedByItem(c.Id.ToString(), c.Title, c.Year))]);
        }
        catch (Exception e) when (e is SidecarException or IndexException)
        {
            throw new LibraryException(e.Message);
        }
    }

    /// <summary>
    /// Asks Crossref for the references of a paper by its DOI (F-PAP-11). The list is stored only with <paramref name="apply"/>; a new
    /// list replaces the old one. Only on request.
    /// </summary>
    /// <exception cref="LibraryException">Invalid: the work has no DOI. Conflict: the source does not know the DOI or cannot be reached. NotFound: no such work.</exception>
    public ReferencesFetchResult FetchReferences(string id, bool apply, CancellationToken cancellation)
    {
        var work = Find(id);
        var doi = ReadSidecar(work).Doi;
        if (string.IsNullOrWhiteSpace(doi))
        {
            throw new LibraryException($"'{work.Title}' has no DOI; references are loaded by DOI");
        }

        try
        {
            var references = LibraryReferences.Fetch(metadataSources ?? [], doi, cancellation);
            var entries = LibraryReferences.Match(root, work.Id, references);
            if (apply)
            {
                lock (_writeGate)
                {
                    LibraryReferences.Save(root, work, references, DateTimeOffset.UtcNow);
                }
            }

            return new ReferencesFetchResult(
                work.Id.ToString(),
                work.Title,
                LibraryReferences.SourceName,
                references.Count,
                references.Count(r => r.Doi is not null),
                entries.Count(e => e.Work is not null),
                apply);
        }
        catch (Exception e) when (e is LibraryEditException or MetadataSourceException)
        {
            throw new LibraryException(e.Message, LibraryErrorKind.Conflict);
        }
        catch (Exception e) when (e is SidecarException or IndexException)
        {
            throw new LibraryException(e.Message);
        }
    }

    private static ReferenceItem Item(ReferenceEntry e) =>
        new(e.Reference.Doi, e.Reference.Title, e.Reference.Year, e.Reference.Author, e.Reference.Unstructured,
            e.Work is null ? null : new ReferencedWorkView(e.Work.Id.ToString(), e.Work.Title));
}
