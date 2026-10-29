using Shelf.Core.Model;
using Shelf.Core.Search;

namespace Shelf.Index;

/// <summary>The citations of a set of works (F-PAP-5): all works, or the result of a search.</summary>
public sealed record CitationExport(string Text, int Works, IReadOnlyList<string> Skipped);

/// <summary>Exports works as BibTeX, CSL-JSON or RIS (F-PAP-5).</summary>
public static class LibraryCitations
{
    private const int NoLimit = 1_000_000;

    /// <summary>
    /// Renders the works that match <paramref name="query"/> (all works if it is null or empty), or only the work <paramref name="only"/>.
    /// With <paramref name="assignKeys"/> a work without a cite key gets one first and the key is written to its sidecar; a work
    /// whose author is unknown cannot get one. Without it nothing is written, and a work without a key is skipped.
    /// </summary>
    /// <exception cref="SidecarException">A sidecar is invalid.</exception>
    /// <exception cref="IndexException">There is no usable index.</exception>
    public static CitationExport Export(string root, CitationFormat format, SearchQuery? query = null, bool assignKeys = true, Ulid? only = null)
    {
        if (assignKeys)
        {
            LibraryCiteKeys.Assign(root, null);
        }

        var works = new List<LibraryWork>();
        using (var connection = IndexDatabase.OpenRead(IndexDatabase.PathFor(root)))
        {
            if (only is { } one)
            {
                works.AddRange(AllWorks(connection).Where(w => w.Id == one));
            }
            else if (query is { IsEmpty: false })
            {
                var hits = LibrarySearch.Search(connection, query, NoLimit);
                var ids = hits.Select(h => h.WorkId).ToHashSet();
                works.AddRange(AllWorks(connection).Where(w => ids.Contains(w.Id)));
                var order = hits.Select((h, i) => (h.WorkId, i)).ToDictionary(x => x.WorkId, x => x.i);
                works.Sort((a, b) => order[a.Id].CompareTo(order[b.Id]));
            }
            else
            {
                works.AddRange(AllWorks(connection));
            }
        }

        var sidecars = new List<WorkSidecar>();
        var skipped = new List<string>();
        foreach (var work in works)
        {
            var sidecar = LibraryEditor.Read(root, work);
            if (string.IsNullOrWhiteSpace(sidecar.CiteKey))
            {
                skipped.Add(sidecar.Title);
            }
            else
            {
                sidecars.Add(sidecar);
            }
        }

        return new CitationExport(CitationFormats.Render(format, sidecars), sidecars.Count, skipped);
    }

    private static List<LibraryWork> AllWorks(Microsoft.Data.Sqlite.SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, dir_path, title FROM works ORDER BY author_sort, year, title_norm";
        using var reader = command.ExecuteReader();
        var all = new List<LibraryWork>();
        while (reader.Read())
        {
            all.Add(new LibraryWork(Ulid.Parse(reader.GetString(0)), reader.GetString(1), reader.GetString(2)));
        }

        return all;
    }
}
