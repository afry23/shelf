using System.Globalization;
using Shelf.Core.Grouping;
using Shelf.Core.Model;

namespace Shelf.Index;

/// <summary>How a field of the work relates to what the source knows.</summary>
public enum FieldAction
{
    /// <summary>The work has no value: the online one is written.</summary>
    Fill,

    /// <summary>The work has another value and <c>--overwrite</c> was given: the online one is written.</summary>
    Overwrite,

    /// <summary>The work has another value: it stays.</summary>
    Keep,
}

/// <param name="Field">The name of the field as <c>shelf edit</c> and <c>book.json</c> know it.</param>
/// <param name="Current">The value in the sidecar, or null.</param>
/// <param name="Online">The value of the source.</param>
public sealed record FieldChange(string Field, FieldAction Action, string? Current, string Online)
{
    public bool IsWritten => Action != FieldAction.Keep;
}

/// <param name="Match">How the book was found: <c>ISBN 978…</c> or <c>title and author</c>.</param>
/// <param name="Warnings">Sources that failed before one answered, as <c>openlibrary: connection refused</c>.</param>
public sealed record EnrichmentPreview(OnlineBook Book, string Match, IReadOnlyList<FieldChange> Changes, IReadOnlyList<string> Warnings, IMetadataSource Source)
{
    public IEnumerable<FieldChange> Written => Changes.Where(c => c.IsWritten);
}

/// <summary>The lookup has no usable result. <see cref="Candidates"/> is set when several books match and the user has to pick one.</summary>
public sealed class EnrichmentException(string message, IReadOnlyList<OnlineBook>? candidates = null, IReadOnlyList<string>? warnings = null) : Exception(message)
{
    public IReadOnlyList<OnlineBook> Candidates { get; } = candidates ?? [];

    public IReadOnlyList<string> Warnings { get; } = warnings ?? [];
}

/// <param name="Fields">The names of the fields that were written.</param>
/// <param name="Cover">The file name of the cover that was saved, or null.</param>
public sealed record EnrichmentResult(IReadOnlyList<string> Fields, string? Cover, IReadOnlyList<string> Warnings);

/// <summary>
/// Looks a work up in the online sources and writes what they know into its sidecar (F-IMP-6). The first source that knows the
/// book wins; the sources are not merged. By default only empty fields are filled.
/// </summary>
public static class EnrichmentService
{
    private static readonly string[] CoverExtensions = ["jpg", "png", "gif", "webp"];

    /// <exception cref="EnrichmentException">No source knows the book, or no single book can be chosen.</exception>
    public static EnrichmentPreview Preview(WorkSidecar sidecar, IReadOnlyList<IMetadataSource> sources, bool overwrite, int? pick, CancellationToken cancellation)
    {
        ArgumentNullException.ThrowIfNull(sidecar);
        ArgumentNullException.ThrowIfNull(sources);
        var warnings = new List<string>();
        var paper = sidecar.Kind == WorkKind.Paper;
        var isbn = paper ? null : sidecar.Isbn13 ?? (sidecar.Isbn10 is { } ten ? Isbn.ToIsbn13(ten) : null);
        var identifier = paper ? IdentifierOf(sidecar) : null;
        var author = sidecar.Authors.FirstOrDefault(a => a != AuthorNames.UnknownAuthor);
        var seen = 0;
        sources = [.. sources.Where(s => s.ForPapers == paper)];

        // Several papers with the same title are common (reposts of the same preprint). The first source that has several is
        // remembered and the next source is asked before the user has to choose; a source with a single match decides.
        (IReadOnlyList<OnlineBook> Clear, string Message)? ambiguous = null;
        foreach (var source in sources)
        {
            IReadOnlyList<OnlineBook> found;
            try
            {
                found = isbn is not null ? source.FindByIsbn(isbn, cancellation)
                    : identifier is { } id ? id.Lookup(source, cancellation)
                    : source.Search(sidecar.Title, author, cancellation);
            }
            catch (MetadataSourceException e)
            {
                warnings.Add($"{source.Name}: {e.Message}");
                continue;
            }

            seen += found.Count;
            if (found.Count == 0)
            {
                continue;
            }

            if (isbn is not null)
            {
                return Build(sidecar, source, found[0], $"ISBN {isbn}", overwrite, warnings);
            }

            if (identifier is { } known)
            {
                return Build(sidecar, source, found[0], known.Description, overwrite, warnings);
            }

            var clear = found.Where(b => IsClearMatch(sidecar, b)).ToList();
            if (clear.Count == 0)
            {
                continue;
            }

            if (pick is { } number)
            {
                return number <= clear.Count
                    ? Build(sidecar, source, clear[number - 1], "title and author", overwrite, warnings)
                    : throw new EnrichmentException($"--pick {number.ToString(CultureInfo.InvariantCulture)}: there are only {clear.Count.ToString(CultureInfo.InvariantCulture)} candidates", clear, warnings);
            }

            if (clear.Count == 1)
            {
                return Build(sidecar, source, clear[0], "title and author", overwrite, warnings);
            }

            var message = $"{clear.Count.ToString(CultureInfo.InvariantCulture)} candidates; pick one with --pick";
            if (!paper)
            {
                throw new EnrichmentException(message, clear, warnings);
            }

            ambiguous ??= (clear, message);
        }

        if (ambiguous is { } several)
        {
            throw new EnrichmentException(several.Message, several.Clear, warnings);
        }

        throw new EnrichmentException(
            seen == 0
                ? warnings.Count == sources.Count ? "no source could be asked" : "no online source knows this book"
                : $"no clear match for '{sidecar.Title}': {seen.ToString(CultureInfo.InvariantCulture)} results, none with the same title and author; add the ISBN with 'shelf edit --isbn'",
            warnings: warnings);
    }

    /// <summary>Writes the written fields of the preview into the sidecar and, if the work has no cover yet, saves the cover of the book.</summary>
    public static EnrichmentResult Apply(string root, LibraryWork work, EnrichmentPreview preview, bool withCover, CancellationToken cancellation)
    {
        ArgumentNullException.ThrowIfNull(work);
        ArgumentNullException.ThrowIfNull(preview);
        var written = preview.Written.ToList();
        if (written.Count > 0)
        {
            LibraryEditor.Update(root, work, s => written.Aggregate(s, Write));
        }

        // The cite key is built from author, year and title, which the source may just have filled in (F-PAP-4).
        if (written.Count > 0 && LibraryEditor.Read(root, work).CiteKey is null)
        {
            LibraryCiteKeys.Assign(root, work);
        }

        var warnings = new List<string>();
        string? cover = null;
        if (withCover)
        {
            cover = SaveCover(root, work, preview, warnings, cancellation);
        }

        return new EnrichmentResult([.. written.Select(c => c.Field)], cover, warnings);
    }

    private static EnrichmentPreview Build(WorkSidecar sidecar, IMetadataSource source, OnlineBook book, string match, bool overwrite, List<string> warnings)
    {
        var changes = new List<FieldChange>();
        void Compare(string field, string? current, string? online)
        {
            if (online is null || string.Equals(current, online, StringComparison.Ordinal))
            {
                return;
            }

            changes.Add(new FieldChange(field, current is null ? FieldAction.Fill : overwrite ? FieldAction.Overwrite : FieldAction.Keep, current, online));
        }

        Compare("isbn", sidecar.Isbn13, book.Isbn13);
        Compare("doi", sidecar.Doi, book.Doi);
        Compare("arxiv", sidecar.ArxivId, book.ArxivId);
        Compare("title", sidecar.Title, book.Title);
        Compare("subtitle", sidecar.Subtitle, book.Subtitle);

        // A sidecar needs an author; "Unknown Author" stands for none and is filled like an empty field.
        var knownAuthors = sidecar.Authors.Where(a => a != AuthorNames.UnknownAuthor).ToList();
        Compare("authors", knownAuthors.Count == 0 ? null : string.Join(", ", sidecar.Authors), book.Authors.Count == 0 ? null : string.Join(", ", book.Authors));
        Compare("venue", sidecar.Venue, book.Venue);
        Compare("volume", sidecar.Volume, book.Volume);
        Compare("issue", sidecar.Issue, book.Issue);
        Compare("pages", sidecar.Pages, book.Pages);
        Compare("abstract", sidecar.Abstract, book.Abstract);
        Compare("publisher", sidecar.Publisher, book.Publisher);
        Compare("year", sidecar.Year?.ToString(CultureInfo.InvariantCulture), book.Year?.ToString(CultureInfo.InvariantCulture));
        Compare("language", sidecar.Language, book.Language);
        Compare("description", sidecar.Description, book.Description);

        // The title is only compared to be shown; a title that differs in case or in the subtitle is not worth a line.
        changes.RemoveAll(c => c.Field == "title" && TitleNormalizer.Normalize(c.Current ?? string.Empty) == TitleNormalizer.Normalize(c.Online));
        return new EnrichmentPreview(book, match, changes, warnings, source);
    }

    private static WorkSidecar Write(WorkSidecar sidecar, FieldChange change) => change.Field switch
    {
        "isbn" => sidecar with { Isbn10 = null, Isbn13 = change.Online },
        "doi" => sidecar with { Kind = WorkKind.Paper, Doi = change.Online },
        "arxiv" => sidecar with { Kind = WorkKind.Paper, ArxivId = change.Online },
        "venue" => sidecar with { Venue = change.Online },
        "volume" => sidecar with { Volume = change.Online },
        "issue" => sidecar with { Issue = change.Online },
        "pages" => sidecar with { Pages = change.Online },
        "abstract" => sidecar with { Abstract = change.Online },
        "title" => sidecar with { Title = change.Online },
        "subtitle" => sidecar with { Subtitle = change.Online },
        "authors" => sidecar with { Authors = [.. change.Online.Split(", ")] },
        "publisher" => sidecar with { Publisher = change.Online },
        "year" => sidecar with { Year = int.Parse(change.Online, CultureInfo.InvariantCulture) },
        "language" => sidecar with { Language = change.Online },
        "description" => sidecar with { Description = change.Online },
        _ => sidecar,
    };

    // A paper with a DOI or arXiv ID is looked up by it; the DOI comes first because the publisher's record is the better one.
    private static PaperIdentifier? IdentifierOf(WorkSidecar sidecar) =>
        sidecar.Doi is { } doi ? new PaperIdentifier($"DOI {doi}", (s, c) => s.FindByDoi(doi, c))
        : sidecar.ArxivId is { } arxiv ? new PaperIdentifier($"arXiv {arxiv}", (s, c) => s.FindByArxivId(arxiv, c))
        : null;

    private sealed record PaperIdentifier(string Description, Func<IMetadataSource, CancellationToken, IReadOnlyList<OnlineBook>> Find)
    {
        public IReadOnlyList<OnlineBook> Lookup(IMetadataSource source, CancellationToken cancellation) => Find(source, cancellation);
    }

    private static bool IsClearMatch(WorkSidecar sidecar, OnlineBook book) =>
        TitleNormalizer.Normalize(book.Title) == TitleNormalizer.Normalize(sidecar.Title) && AuthorNames.Match(sidecar.Authors, book.Authors);

    private static string? SaveCover(string root, LibraryWork work, EnrichmentPreview preview, List<string> warnings, CancellationToken cancellation)
    {
        var folder = Path.Combine(root, work.Directory);
        var sidecar = LibraryEditor.Read(root, work);
        if ((sidecar.Cover is { } named && File.Exists(Path.Combine(folder, named)))
            || CoverExtensions.Any(e => File.Exists(Path.Combine(folder, "cover." + e))))
        {
            return null;
        }

        OnlineCover? cover;
        try
        {
            cover = preview.Source.FetchCover(preview.Book, cancellation);
        }
        catch (MetadataSourceException e)
        {
            warnings.Add($"{preview.Source.Name}: cover: {e.Message}");
            return null;
        }

        var extension = cover?.ContentType switch
        {
            "image/jpeg" => "jpg",
            "image/png" => "png",
            "image/gif" => "gif",
            "image/webp" => "webp",
            _ => null,
        };
        if (cover is null || extension is null)
        {
            return null;
        }

        var name = "cover." + extension;
        File.WriteAllBytes(Path.Combine(folder, name), cover.Bytes);
        // The cover is adopted like one that a user dropped into the folder: the sidecar names it and the index knows its path.
        LibraryCovers.FillWorks(root, [work.Id]);
        return name;
    }
}
