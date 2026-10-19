using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;
using Shelf.Core.Model;
using Shelf.Extraction;
using Shelf.Organize;

namespace Shelf.Index;

public enum DuplicateKind
{
    /// <summary>Files with the same SHA-256 (F-DUP-1).</summary>
    Identical,

    /// <summary>Different files of one work in the same format (F-DUP-2).</summary>
    SameFormat,

    /// <summary>Different works with (nearly) the same text or vectors (F-DUP-3); for review only.</summary>
    Probable,
}

/// <param name="Folder">The folder of the work, library-relative.</param>
/// <param name="Name">The file name inside that folder, as the sidecar names it.</param>
/// <param name="Path">Library-relative path with '/'.</param>
/// <param name="Completeness">How many of <see cref="LibraryDuplicates.MetadataFields"/> metadata fields the file has; only for <see cref="DuplicateKind.SameFormat"/>.</param>
public sealed record DuplicateFile(Ulid Work, string Title, string Folder, string Name, string Path, long Size, int? Pages, int? Completeness);

/// <param name="Files">In path order.</param>
/// <param name="Keep">Index of the file that is suggested to stay.</param>
public sealed record DuplicateGroup(DuplicateKind Kind, string Format, IReadOnlyList<DuplicateFile> Files, int Keep);

/// <summary>Duplicates in the library (F-DUP-1, F-DUP-2) and what to do about them (F-DUP-4, F-DUP-6).</summary>
public static partial class LibraryDuplicates
{
    /// <summary>Title, authors, language, year, publisher, description, ISBN-13, series.</summary>
    public const int MetadataFields = 8;

    /// <summary>Pairs marked as not duplicates (F-DUP-6) are not suggested again.</summary>
    /// <exception cref="IndexException">There is no usable index.</exception>
    public static IReadOnlyList<DuplicateGroup> Find(string root)
    {
        root = Normalize(root);
        using var connection = IndexDatabase.OpenRead(IndexDatabase.PathFor(root));
        var rows = LoadFiles(connection);
        var ignores = LoadIgnores(connection);
        var groups = new List<DuplicateGroup>();

        foreach (var same in rows.Where(r => r.Sha256 is not null).GroupBy(r => r.Sha256!).Where(g => g.Count() > 1))
        {
            foreach (var component in Components([.. same], ignores))
            {
                groups.Add(new DuplicateGroup(DuplicateKind.Identical, component[0].Format, [.. component.Select(r => r.File)], KeepIdentical(component)));
            }
        }

        foreach (var work in rows.GroupBy(r => r.File.Work))
        {
            foreach (var format in work.GroupBy(r => r.Format))
            {
                // Files with the same content are one candidate here; they are already a group of their own.
                var candidates = format.GroupBy(r => r.Sha256 ?? r.File.Path).Select(g => g.First()).ToList();
                if (candidates.Count < 2)
                {
                    continue;
                }

                foreach (var component in Components(candidates, ignores))
                {
                    var files = component.Select(r => r.File with { Completeness = Completeness(root, r.File) }).ToList();
                    groups.Add(new DuplicateGroup(DuplicateKind.SameFormat, format.Key, files, KeepMostComplete(files)));
                }
            }
        }

        return [.. groups.OrderBy(g => g.Files[0].Path, StringComparer.Ordinal)];
    }

    /// <summary>A session of changes; all of them are one undoable journal (label <c>dupes</c>).</summary>
    public static DuplicateSession Begin(string root) => new(Normalize(root));

    internal static string Normalize(string root)
    {
        ArgumentException.ThrowIfNullOrEmpty(root);
        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
    }

    private static int KeepIdentical(List<Row> rows)
    {
        // A name without " (1)" first, then the path order; the library's own layout puts the right one first.
        var best = rows.Select((r, i) => (r, i)).OrderBy(x => NumberedName().IsMatch(x.r.File.Name) ? 1 : 0).ThenBy(x => x.r.File.Path, StringComparer.Ordinal).First();
        return rows.IndexOf(best.r);
    }

    private static int KeepMostComplete(List<DuplicateFile> files)
    {
        var best = files
            .Select((f, i) => (f, i))
            .OrderByDescending(x => x.f.Completeness)
            .ThenByDescending(x => x.f.Pages ?? 0)
            .ThenByDescending(x => x.f.Size)
            .ThenBy(x => x.f.Path, StringComparer.Ordinal)
            .First();
        return best.i;
    }

    private static int? Completeness(string root, DuplicateFile file)
    {
        try
        {
            var m = BookExtractor.ReadMetadata(Path.Combine(root, file.Path.Replace('/', Path.DirectorySeparatorChar)));
            return new[]
            {
                !string.IsNullOrWhiteSpace(m.Title),
                m.Authors.Count > 0,
                !string.IsNullOrWhiteSpace(m.Language),
                m.Year is not null,
                !string.IsNullOrWhiteSpace(m.Publisher),
                !string.IsNullOrWhiteSpace(m.Description),
                !string.IsNullOrWhiteSpace(m.Isbn13),
                !string.IsNullOrWhiteSpace(m.Series),
            }.Count(present => present);
        }
        catch (ExtractionException)
        {
            return 0;
        }
    }

    /// <summary>Groups of rows that are connected by pairs that are not marked as "not a duplicate"; at least two each.</summary>
    private static List<List<Row>> Components(List<Row> rows, Ignores ignores)
    {
        var parent = Enumerable.Range(0, rows.Count).ToArray();
        int Find(int i) => parent[i] == i ? i : parent[i] = Find(parent[i]);
        for (var i = 0; i < rows.Count; i++)
        {
            for (var j = i + 1; j < rows.Count; j++)
            {
                if (!ignores.Contains(rows[i].File, rows[j].File))
                {
                    parent[Find(i)] = Find(j);
                }
            }
        }

        return [.. Enumerable.Range(0, rows.Count)
            .GroupBy(Find)
            .Select(g => g.Select(i => rows[i]).OrderBy(r => r.File.Path, StringComparer.Ordinal).ToList())
            .Where(c => c.Count > 1)];
    }

    private static List<Row> LoadFiles(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT f.work_id, w.title, w.dir_path, f.rel_path, f.format, f.size, f.sha256, f.page_count
            FROM files f JOIN works w ON w.id = f.work_id
            ORDER BY f.rel_path
            """;
        using var reader = command.ExecuteReader();
        var rows = new List<Row>();
        while (reader.Read())
        {
            var folder = reader.GetString(2);
            var path = reader.GetString(3);
            var name = folder == "." ? path : path[(folder.Length + 1)..];
            rows.Add(new Row(
                new DuplicateFile(Ulid.Parse(reader.GetString(0)), reader.GetString(1), folder, name, path, reader.GetInt64(5), reader.IsDBNull(7) ? null : reader.GetInt32(7), null),
                reader.GetString(4),
                reader.IsDBNull(6) ? null : reader.GetString(6)));
        }

        return rows;
    }

    private static Ignores LoadIgnores(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT a_id, b_id, kind FROM dupe_ignores";
        using var reader = command.ExecuteReader();
        var ignores = new Ignores();
        while (reader.Read())
        {
            ignores.Add(reader.GetString(0), reader.GetString(1), reader.GetString(2));
        }

        return ignores;
    }

    private sealed record Row(DuplicateFile File, string Format, string? Sha256);

    private sealed class Ignores
    {
        private readonly HashSet<(string, string)> _works = [];
        private readonly HashSet<(string, string)> _files = [];

        public void Add(string a, string b, string kind) => (kind == "work" ? _works : _files).Add(Order(a, b));

        public bool ContainsWorks(Ulid a, Ulid b) => _works.Contains(Order(a.ToString(), b.ToString()));

        public bool Contains(DuplicateFile x, DuplicateFile y) => x.Work == y.Work
            ? _files.Contains(Order(x.Path, y.Path))
            : _works.Contains(Order(x.Work.ToString(), y.Work.ToString()));

        private static (string, string) Order(string a, string b) => string.CompareOrdinal(a, b) <= 0 ? (a, b) : (b, a);
    }

    [GeneratedRegex(@"\s\(\d+\)\.[^.]+$")]
    private static partial Regex NumberedName();
}

/// <summary>
/// Changes to resolve duplicates: files go to the trash through one journal, the sidecars follow, and the index is
/// brought in line after every step so that <see cref="LibraryDuplicates.Find"/> sees the new state. The journal is
/// only begun by the first change; call <see cref="Finish"/> at the end.
/// </summary>
public sealed class DuplicateSession
{
    private readonly string _root;
    private readonly List<Ulid> _worksRemoved = [];
    private FileJournal? _journal;

    internal DuplicateSession(string root) => _root = root;

    /// <summary>Files moved to the trash in this session.</summary>
    public int Trashed { get; private set; }

    /// <summary>Works that had no file left and went to the trash with their sidecar.</summary>
    public IReadOnlyList<Ulid> WorksRemoved => _worksRemoved;

    private FileJournal Journal => _journal ??= FileJournal.Begin(_root, "dupes", undoable: true);

    /// <summary>Moves all files of the group except <paramref name="keep"/> to the trash and updates the sidecars.</summary>
    /// <returns>The number of files moved.</returns>
    public int Resolve(DuplicateGroup group, int keep)
    {
        ArgumentNullException.ThrowIfNull(group);
        ArgumentOutOfRangeException.ThrowIfNegative(keep);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(keep, group.Files.Count);

        var trashed = 0;
        foreach (var work in group.Files.Where((_, i) => i != keep).GroupBy(f => f.Work))
        {
            var folder = Path.Combine(_root, work.First().Folder);
            var removed = new HashSet<string>(StringComparer.Ordinal);
            foreach (var file in work)
            {
                if (Journal.Trash(Path.Combine(_root, file.Path.Replace('/', Path.DirectorySeparatorChar))) == MoveStatus.Moved)
                {
                    trashed++;
                }

                removed.Add(file.Name);
            }

            var sidecarPath = Path.Combine(folder, SidecarStore.FileName);
            var sidecar = SidecarStore.Read(sidecarPath);
            var remaining = sidecar.Files.Where(f => !removed.Contains(f.Name)).ToList();
            if (remaining.Count == 0)
            {
                Journal.Trash(sidecarPath);
                if (sidecar.Cover is { } cover && File.Exists(Path.Combine(folder, cover)))
                {
                    Journal.Trash(Path.Combine(folder, cover));
                }

                _worksRemoved.Add(work.Key);
            }
            else
            {
                var updated = sidecar with
                {
                    Files = remaining,
                    NotDuplicates = sidecar.NotDuplicates with
                    {
                        Files = [.. sidecar.NotDuplicates.Files.Where(p => !removed.Contains(p.A) && !removed.Contains(p.B))],
                    },
                };
                Journal.Replace(sidecarPath, Bytes(updated, sidecarPath));
            }

            if (work.First().Folder != ".")
            {
                LibraryEditor.RemoveFolderIfEmpty(_root, folder);
            }
        }

        Trashed += trashed;
        LibraryEditor.Reconcile(_root);
        return trashed;
    }

    /// <summary>
    /// Marks every pair of the group as "not a duplicate" (F-DUP-6): within a work as a file pair, across works with the
    /// ID of the other work on both sides.
    /// </summary>
    public void MarkNotDuplicates(DuplicateGroup group)
    {
        ArgumentNullException.ThrowIfNull(group);
        var folders = group.Files.GroupBy(f => f.Work).ToDictionary(g => g.Key, g => g.First().Folder);
        var updated = new Dictionary<Ulid, WorkSidecar>();
        WorkSidecar Current(Ulid work) => updated.TryGetValue(work, out var s)
            ? s
            : SidecarStore.Read(Path.Combine(_root, folders[work], SidecarStore.FileName));

        for (var i = 0; i < group.Files.Count; i++)
        {
            for (var j = i + 1; j < group.Files.Count; j++)
            {
                var (a, b) = (group.Files[i], group.Files[j]);
                if (a.Work == b.Work)
                {
                    var pair = string.CompareOrdinal(a.Name, b.Name) <= 0 ? new FilePair(a.Name, b.Name) : new FilePair(b.Name, a.Name);
                    var sidecar = Current(a.Work);
                    if (!sidecar.NotDuplicates.Files.Contains(pair))
                    {
                        updated[a.Work] = sidecar with { NotDuplicates = sidecar.NotDuplicates with { Files = [.. sidecar.NotDuplicates.Files, pair] } };
                    }
                }
                else
                {
                    foreach (var (work, other) in new[] { (a.Work, b.Work), (b.Work, a.Work) })
                    {
                        var sidecar = Current(work);
                        if (!sidecar.NotDuplicates.Works.Contains(other))
                        {
                            updated[work] = sidecar with { NotDuplicates = sidecar.NotDuplicates with { Works = [.. sidecar.NotDuplicates.Works, other] } };
                        }
                    }
                }
            }
        }

        foreach (var (work, sidecar) in updated)
        {
            var path = Path.Combine(_root, folders[work], SidecarStore.FileName);
            Journal.Replace(path, Bytes(sidecar, path));
        }

        if (updated.Count > 0)
        {
            LibraryEditor.Reconcile(_root);
        }
    }

    /// <summary>Marks two works as "not a duplicate" (F-DUP-6): on both sides with the ID of the other work. Nothing else moves.</summary>
    public void MarkNotDuplicates(ProbableDuplicate pair)
    {
        ArgumentNullException.ThrowIfNull(pair);
        DuplicateFile Representative(ProbableWork work) => new(work.Id, work.Title, work.Folder, string.Empty, work.Files.FirstOrDefault() ?? work.Folder, 0, null, null);
        MarkNotDuplicates(new DuplicateGroup(DuplicateKind.Probable, string.Empty, [Representative(pair.A), Representative(pair.B)], 0));
    }

    /// <summary>Closes the journal, if any change was made.</summary>
    public void Finish() => _journal?.Finish();

    private static byte[] Bytes(WorkSidecar sidecar, string path)
    {
        SidecarFormat.Validate(sidecar, path);
        return new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(SidecarFormat.Serialize(sidecar));
    }
}
