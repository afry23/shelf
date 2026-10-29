using Microsoft.Data.Sqlite;
using Shelf.Core.Model;

namespace Shelf.Index;

/// <summary>The work in the library that a reference points to.</summary>
public sealed record ReferencedWork(Ulid Id, string Title);

/// <summary>A reference and, if its DOI is that of a work in the library, that work.</summary>
public sealed record ReferenceEntry(Reference Reference, ReferencedWork? Work);

/// <summary>A work in the library that lists the paper in its references.</summary>
public sealed record CitingWork(Ulid Id, string Title, int? Year);

/// <summary>What the library knows about the references of a work: its list and the works that cite it (F-PAP-11, F-PAP-12).</summary>
public sealed record ReferenceListing(
    IReadOnlyList<ReferenceEntry> References,
    IReadOnlyList<CitingWork> CitedBy,
    string? Source,
    DateTimeOffset? FetchedAt)
{
    public int InLibrary => References.Count(r => r.Work is not null);
}

/// <summary>
/// The reference list of a paper (F-PAP-11). <c>references.json</c> next to <c>book.json</c> is the source of truth; the index holds a
/// copy so that the works that cite a paper can be found (ADR 0019). Like annotations, a change writes the file and updates the one
/// work in the index, and it is not in the journal. The list is loaded only on <c>shelf refs fetch</c>.
/// </summary>
public static class LibraryReferences
{
    /// <summary>The catalog that reference lists are asked from.</summary>
    public const string SourceName = "crossref";

    public static string PathOf(string root, LibraryWork work) => Path.Combine(root, work.Directory, ReferencesFormat.FileName);

    /// <exception cref="SidecarException">The file is invalid.</exception>
    public static ReferencesSidecar Read(string root, LibraryWork work) => ReferencesStore.Read(PathOf(root, work));

    /// <summary>
    /// Asks Crossref for the references of the paper with this DOI. Nothing is stored.
    /// </summary>
    /// <exception cref="LibraryEditException">No source can list references, or it does not know the DOI.</exception>
    /// <exception cref="MetadataSourceException">The source cannot be reached.</exception>
    public static IReadOnlyList<Reference> Fetch(IEnumerable<IMetadataSource> sources, string doi, CancellationToken cancellation)
    {
        ArgumentNullException.ThrowIfNull(sources);
        var source = sources.FirstOrDefault(s => s.Name == SourceName)
            ?? throw new LibraryEditException("no online source can list references");
        return source.FindReferences(doi, cancellation)
            ?? throw new LibraryEditException($"{SourceName} does not know the DOI {doi}");
    }

    /// <summary>Stores the list next to the work (none removes the file) and mirrors it in the index.</summary>
    /// <exception cref="SidecarException"/>
    /// <exception cref="IndexException"/>
    public static void Save(string root, LibraryWork work, IReadOnlyList<Reference> references, DateTimeOffset fetched)
    {
        var sidecar = ReadOrEmpty(root, work) with
        {
            Source = SourceName,
            FetchedAt = DateTimeOffset.FromUnixTimeSeconds(fetched.ToUnixTimeSeconds()),
            References = references,
        };
        ReferencesStore.Write(PathOf(root, work), sidecar);

        using var connection = IndexDatabase.OpenWrite(IndexDatabase.PathFor(root));
        using var writer = new IndexWriter(connection);
        using var transaction = writer.BeginTransaction();
        writer.ReplaceReferences(work.Id.ToString(), references);
        transaction.Commit();
    }

    /// <summary>The list with the works of the library that it points to, and the works that cite this one.</summary>
    /// <exception cref="SidecarException"/>
    /// <exception cref="IndexException"/>
    public static ReferenceListing Describe(string root, LibraryWork work)
    {
        var sidecar = ReadOrEmpty(root, work);
        using var connection = IndexDatabase.OpenRead(IndexDatabase.PathFor(root));
        return new ReferenceListing(Match(connection, work.Id, sidecar.References), CitedBy(connection, work.Id), sidecar.Source, sidecar.FetchedAt);
    }

    /// <summary>Which of the references point to a work of the library (by DOI, whatever its case); a work does not cite itself.</summary>
    /// <exception cref="IndexException"/>
    public static IReadOnlyList<ReferenceEntry> Match(string root, Ulid self, IReadOnlyList<Reference> references)
    {
        using var connection = IndexDatabase.OpenRead(IndexDatabase.PathFor(root));
        return Match(connection, self, references);
    }

    private static List<ReferenceEntry> Match(SqliteConnection connection, Ulid self, IReadOnlyList<Reference> references)
    {
        var known = new Dictionary<string, ReferencedWork>(StringComparer.Ordinal);
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT id, title, doi FROM works WHERE doi IS NOT NULL AND id <> $self";
            command.Parameters.AddWithValue("$self", self.ToString());
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                known.TryAdd(PaperIds.Normalize(reader.GetString(2)), new ReferencedWork(Ulid.Parse(reader.GetString(0)), reader.GetString(1)));
            }
        }

        return [.. references.Select(r => new ReferenceEntry(r, r.Doi is { } doi && known.TryGetValue(PaperIds.Normalize(doi), out var work) ? work : null))];
    }

    private static List<CitingWork> CitedBy(SqliteConnection connection, Ulid self)
    {
        var cited = new List<CitingWork>();
        using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT DISTINCT w.id, w.title, w.year FROM works me JOIN work_refs r ON r.doi = me.doi JOIN works w ON w.id = r.work_id "
            + "WHERE me.id = $self AND me.doi IS NOT NULL AND w.id <> me.id ORDER BY w.title, w.id";
        command.Parameters.AddWithValue("$self", self.ToString());
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            cited.Add(new CitingWork(Ulid.Parse(reader.GetString(0)), reader.GetString(1), reader.IsDBNull(2) ? null : reader.GetInt32(2)));
        }

        return cited;
    }

    /// <summary>The references of the work folder, for the copy in the index; a file that cannot be read is a problem and has none.</summary>
    internal static IReadOnlyList<Reference> ReadForMirror(string root, string directory, List<ReindexProblem> problems)
    {
        var path = Path.Combine(root, directory, ReferencesFormat.FileName);
        try
        {
            return ReferencesStore.Read(path).References;
        }
        catch (SidecarException e)
        {
            problems.Add(new ReindexProblem(path, e.Message));
            return [];
        }
    }

    /// <summary>
    /// A merge removes the folder of the source. Its list goes to the target if the target has none; else it goes (it can be loaded
    /// again).
    /// </summary>
    internal static void CarryOnMerge(string root, LibraryWork source, LibraryWork target)
    {
        var from = PathOf(root, source);
        if (!File.Exists(from))
        {
            return;
        }

        var to = PathOf(root, target);
        if (File.Exists(to))
        {
            File.Delete(from);
        }
        else
        {
            File.Move(from, to);
        }
    }

    private static ReferencesSidecar ReadOrEmpty(string root, LibraryWork work)
    {
        try
        {
            return Read(root, work);
        }
        catch (SidecarException)
        {
            return new ReferencesSidecar();
        }
    }
}
