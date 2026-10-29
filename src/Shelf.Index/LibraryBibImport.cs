using System.Globalization;
using System.Text.Json;
using Shelf.Core.Grouping;
using Shelf.Core.Model;

namespace Shelf.Index;

/// <summary>What became of one entry of a BibTeX file.</summary>
/// <param name="Work">The work the entry belongs to; null if there is none or several (<see cref="Problem"/> says which).</param>
/// <param name="Match">How the work was found: <c>DOI 10.…</c>, <c>arXiv ID …</c> or <c>title and author</c>.</param>
/// <param name="Problem">Why the entry was not used.</param>
/// <param name="Notes">Things about a matched entry that the user should know, e.g. a cite key that is taken.</param>
public sealed record BibEntryResult(BibEntry Entry, LibraryWork? Work, string? Match, string? Problem, IReadOnlyList<FieldChange> Changes, IReadOnlyList<string> Notes)
{
    public bool IsMatched => Work is not null;
}

/// <param name="Written">The number of works whose sidecar was (or, in a preview, would be) changed.</param>
public sealed record BibImportResult(IReadOnlyList<BibEntryResult> Entries, bool Applied, int Written)
{
    public int Matched => Entries.Count(e => e.IsMatched);
}

/// <summary>
/// Matches the entries of a BibTeX file to the works of the library and fills their empty fields (F-PAP-6): by DOI, by arXiv
/// ID, or by exact normalized title with a shared author surname. A value that differs from the one in the sidecar stays
/// unless <c>overwrite</c> is set. The title is never changed. Nothing goes online.
/// </summary>
public static class LibraryBibImport
{
    /// <exception cref="IndexException">There is no usable index.</exception>
    /// <exception cref="SidecarException">A sidecar is invalid.</exception>
    public static BibImportResult Run(string root, IReadOnlyList<BibEntry> entries, bool overwrite, bool apply)
    {
        ArgumentNullException.ThrowIfNull(entries);
        var works = LoadWorks(root);
        var keys = new Dictionary<string, Ulid>(StringComparer.Ordinal);
        foreach (var work in works.Where(w => w.CiteKey is not null))
        {
            keys[work.CiteKey!] = work.Work.Id;
        }

        var current = new Dictionary<Ulid, WorkSidecar>();
        var changed = new HashSet<Ulid>();
        var results = new List<BibEntryResult>();
        foreach (var entry in entries)
        {
            var title = entry.Text("title") ?? "(no title)";
            var (matches, how) = Find(works, entry);
            if (matches.Count != 1)
            {
                results.Add(new BibEntryResult(entry, null, null, matches.Count == 0 ? $"no work: {entry.Key} ({title})" : $"ambiguous: {entry.Key} ({title}): {matches.Count} works match", [], []));
                continue;
            }

            var work = matches[0].Work;
            if (!current.TryGetValue(work.Id, out var sidecar))
            {
                sidecar = LibraryEditor.Read(root, work);
            }

            var notes = new List<string>();
            var changes = Compare(sidecar, entry, overwrite, keys, notes);
            foreach (var change in changes.Where(c => c.IsWritten))
            {
                sidecar = Write(sidecar, change);
                if (change.Field == "citeKey")
                {
                    keys[change.Online] = work.Id;
                }
            }

            if (changes.Any(c => c.IsWritten))
            {
                changed.Add(work.Id);
            }

            current[work.Id] = sidecar;
            results.Add(new BibEntryResult(entry, work, how, null, changes, notes));
        }

        if (apply && changed.Count > 0)
        {
            foreach (var id in changed)
            {
                var work = works.First(w => w.Work.Id == id).Work;
                SidecarStore.Write(LibraryEditor.SidecarPath(root, work), current[id]);
            }

            LibraryEditor.Reconcile(root);
        }

        return new BibImportResult(results, apply, changed.Count);
    }

    private sealed record Row(LibraryWork Work, string TitleNorm, IReadOnlyList<string> Authors, string? Doi, string? ArxivId, string? CiteKey);

    private static List<Row> LoadWorks(string root)
    {
        using var connection = IndexDatabase.OpenRead(IndexDatabase.PathFor(root));
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, dir_path, title, title_norm, authors_json, doi, arxiv_id, cite_key FROM works ORDER BY seq";
        using var reader = command.ExecuteReader();
        var rows = new List<Row>();
        while (reader.Read())
        {
            using var authors = JsonDocument.Parse(reader.GetString(4));
            rows.Add(new Row(
                new LibraryWork(Ulid.Parse(reader.GetString(0)), reader.GetString(1), reader.GetString(2)),
                reader.GetString(3),
                [.. authors.RootElement.EnumerateArray().Select(a => a.GetString()!)],
                reader.IsDBNull(5) ? null : reader.GetString(5),
                reader.IsDBNull(6) ? null : reader.GetString(6),
                reader.IsDBNull(7) ? null : reader.GetString(7)));
        }

        return rows;
    }

    // DOI first, then arXiv ID, then the exact title with a shared author: the first rule that finds a work decides.
    private static (List<Row> Matches, string? How) Find(List<Row> works, BibEntry entry)
    {
        if (DoiOf(entry) is { } doi && works.Where(w => w.Doi == doi).ToList() is { Count: > 0 } byDoi)
        {
            return (byDoi, $"DOI {doi}");
        }

        if (ArxivOf(entry) is { } arxiv && works.Where(w => w.ArxivId == arxiv).ToList() is { Count: > 0 } byArxiv)
        {
            return (byArxiv, $"arXiv ID {arxiv}");
        }

        if (entry.Text("title") is { } title)
        {
            var normalized = TitleNormalizer.Normalize(title);
            var authors = entry.Authors;
            var byTitle = works.Where(w => w.TitleNorm == normalized && AuthorNames.Match(w.Authors, authors)).ToList();
            if (byTitle.Count > 0)
            {
                return (byTitle, "title and author");
            }
        }

        return ([], null);
    }

    private static string? DoiOf(BibEntry entry) =>
        PaperIds.FindDoi(entry.Text("doi") ?? entry.Text("url")) is { } doi ? PaperIds.Normalize(doi) : null;

    private static string? ArxivOf(BibEntry entry)
    {
        var prefix = entry.Text("archiveprefix") ?? entry.Text("eprinttype");
        if (entry.Text("eprint") is { } eprint && (prefix is null || prefix.Contains("arxiv", StringComparison.OrdinalIgnoreCase)))
        {
            return PaperIds.FindArxivId(eprint) ?? PaperIds.FindArxivId("arXiv:" + eprint);
        }

        return PaperIds.FindArxivId(entry.Text("arxivid") ?? entry.Text("arxiv"))
            ?? (entry.Text("journal") is { } journal && journal.Contains("arxiv", StringComparison.OrdinalIgnoreCase) ? PaperIds.FindArxivId(journal) : null);
    }

    private static List<FieldChange> Compare(WorkSidecar sidecar, BibEntry entry, bool overwrite, Dictionary<string, Ulid> keys, List<string> notes)
    {
        var changes = new List<FieldChange>();
        void Add(string field, string? current, string? online)
        {
            if (online is null || string.Equals(current, online, StringComparison.Ordinal))
            {
                return;
            }

            changes.Add(new FieldChange(field, current is null ? FieldAction.Fill : overwrite ? FieldAction.Overwrite : FieldAction.Keep, current, online));
        }

        var known = sidecar.Authors.Where(a => a != AuthorNames.UnknownAuthor).ToList();
        Add("authors", known.Count == 0 ? null : string.Join(", ", sidecar.Authors), entry.Authors.Count == 0 ? null : string.Join(", ", entry.Authors));
        Add("year", sidecar.Year?.ToString(CultureInfo.InvariantCulture), YearOf(entry));
        Add("venue", sidecar.Venue, VenueOf(entry));
        Add("publisher", sidecar.Publisher, entry.Text("publisher"));
        Add("volume", sidecar.Volume, entry.Text("volume"));
        Add("issue", sidecar.Issue, entry.Text("number") ?? entry.Text("issue"));
        Add("pages", sidecar.Pages, entry.Text("pages")?.Replace("--", "-", StringComparison.Ordinal).Replace('–', '-'));
        Add("doi", sidecar.Doi is null ? null : PaperIds.Normalize(sidecar.Doi), DoiOf(entry));
        Add("arxiv", sidecar.ArxivId, ArxivOf(entry));
        Add("isbn", sidecar.Isbn13, entry.Text("isbn") is { } isbn ? Isbn.FromIdentifier(isbn) : null);
        Add("abstract", sidecar.Abstract, entry.Text("abstract"));
        Add("keywords", sidecar.Keywords.Count == 0 ? null : string.Join(", ", sidecar.Keywords), KeywordsOf(entry));

        // The cite key must be one BibTeX can use and must not belong to another work.
        if (entry.Key != sidecar.CiteKey)
        {
            if (!CiteKeys.IsValid(entry.Key))
            {
                notes.Add($"cite key '{entry.Key}' not used: {CiteKeys.Rule}");
            }
            else if (keys.TryGetValue(entry.Key, out var owner) && owner != sidecar.Id)
            {
                notes.Add($"cite key '{entry.Key}' not used: it belongs to another work");
            }
            else
            {
                Add("citeKey", sidecar.CiteKey, entry.Key);
            }
        }

        return changes;
    }

    private static string? YearOf(BibEntry entry)
    {
        var text = entry.Text("year") ?? entry.Text("date");
        return text is { Length: >= 4 } && text[..4].All(char.IsAsciiDigit) ? text[..4].TrimStart('0') : null;
    }

    // "arXiv preprint arXiv:1706.03762" is the way Google Scholar names the lack of a venue.
    private static string? VenueOf(BibEntry entry)
    {
        var venue = entry.Text("journal") ?? entry.Text("journaltitle") ?? entry.Text("booktitle");
        return venue is null || venue.StartsWith("arxiv", StringComparison.OrdinalIgnoreCase) ? null : venue;
    }

    private static string? KeywordsOf(BibEntry entry)
    {
        var words = (entry.Text("keywords") ?? string.Empty).Split([',', ';'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        return words.Length == 0 ? null : string.Join(", ", words);
    }

    private static WorkSidecar Write(WorkSidecar sidecar, FieldChange change) => change.Field switch
    {
        "authors" => sidecar with { Authors = [.. change.Online.Split(", ")] },
        "year" => sidecar with { Year = int.Parse(change.Online, CultureInfo.InvariantCulture) },
        "venue" => sidecar with { Venue = change.Online },
        "publisher" => sidecar with { Publisher = change.Online },
        "volume" => sidecar with { Volume = change.Online },
        "issue" => sidecar with { Issue = change.Online },
        "pages" => sidecar with { Pages = change.Online },
        "doi" => sidecar with { Kind = WorkKind.Paper, Doi = change.Online },
        "arxiv" => sidecar with { Kind = WorkKind.Paper, ArxivId = change.Online },
        "isbn" => sidecar with { Isbn10 = null, Isbn13 = change.Online },
        "abstract" => sidecar with { Abstract = change.Online },
        "keywords" => sidecar with { Keywords = [.. change.Online.Split(", ")] },
        "citeKey" => sidecar with { CiteKey = change.Online },
        _ => sidecar,
    };
}
