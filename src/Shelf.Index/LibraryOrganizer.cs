using System.Text;
using Shelf.Core.Model;
using Shelf.Core.Organization;
using Shelf.Core.Review;
using Shelf.Organize;

namespace Shelf.Index;

/// <summary>One file that moves (or would move). Paths are library-relative with '/'.</summary>
public sealed record OrganizeChange(Ulid Work, string From, string To);

/// <summary>A move that did not happen because its target is taken (F-ORG-7).</summary>
public sealed record OrganizeCollision(Ulid Work, string From, string To, string Reason);

/// <param name="Applied">False for a dry run.</param>
/// <param name="Files">Files that moved (or would move); sidecars and covers that go along are not counted.</param>
/// <param name="Works">Works that moved (or would move).</param>
/// <param name="Unchanged">Files that already are where the pattern puts them.</param>
/// <param name="Collisions">Moves that were skipped; a work with a collision is left as it is, as a whole.</param>
public sealed record OrganizeResult(
    bool Applied,
    int Files,
    int Works,
    int Unchanged,
    IReadOnlyList<OrganizeChange> Changes,
    IReadOnlyList<OrganizeCollision> Collisions);

/// <summary>
/// Moves the files of every work to the place that the pattern gives (F-ORG-5). The folder of a work is the folder of
/// its first file; the sidecar and the cover go with it. A target that exists, or that another file of the same run
/// takes first, is never overwritten: the whole work stays where it is and the collision is reported (F-ORG-7).
/// Every applied run is one undoable journal (F-ORG-6), including the changed sidecars and the review queue.
/// </summary>
public static class LibraryOrganizer
{
    private const string TargetExists = "target exists";
    private const string TargetTaken = "target taken by another file of this run";

    /// <exception cref="IndexException">There is no usable index.</exception>
    /// <exception cref="JournalException"/>
    /// <exception cref="LibraryEditException"/>
    public static OrganizeResult Organize(string root, PathPattern pattern, bool apply)
    {
        ArgumentException.ThrowIfNullOrEmpty(root);
        ArgumentNullException.ThrowIfNull(pattern);
        root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));

        var claimed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var plans = new List<WorkPlan>();
        var collisions = new List<OrganizeCollision>();
        var unchanged = 0;
        foreach (var (id, directory) in ListWorks(root))
        {
            var plan = Plan(root, id, directory, pattern, claimed, collisions);
            if (plan is null)
            {
                continue;
            }

            unchanged += plan.Unchanged;
            if (plan.Moves.Count > 0)
            {
                plans.Add(plan);
            }
        }

        var changes = plans.SelectMany(p => p.Moves.Where(m => m.IsFile).Select(m => new OrganizeChange(p.Id, m.From, m.To))).ToList();
        if (apply && plans.Count > 0)
        {
            Apply(root, plans);
        }

        return new OrganizeResult(apply, changes.Count, plans.Count, unchanged, changes, collisions);
    }

    /// <summary>Reverts the last undoable run and brings the index in line; <c>null</c> if there is nothing to undo.</summary>
    /// <exception cref="JournalException"/>
    /// <exception cref="IndexException">There is no usable index.</exception>
    public static UndoResult? Undo(string root)
    {
        ArgumentException.ThrowIfNullOrEmpty(root);
        root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        var result = JournalUndo.Undo(root);
        if (result is not null)
        {
            LibraryEditor.Reconcile(root);
        }

        return result;
    }

    private static List<(Ulid Id, string Directory)> ListWorks(string root)
    {
        using var connection = IndexDatabase.OpenRead(IndexDatabase.PathFor(root));
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, dir_path FROM works";
        using var reader = command.ExecuteReader();
        var works = new List<(Ulid, string)>();
        while (reader.Read())
        {
            works.Add((Ulid.Parse(reader.GetString(0)), reader.GetString(1)));
        }

        return [.. works.OrderBy(w => w.Item2, StringComparer.Ordinal)];
    }

    private static WorkPlan? Plan(string root, Ulid id, string directory, PathPattern pattern, HashSet<string> claimed, List<OrganizeCollision> collisions)
    {
        var sidecarPath = Path.Combine(root, directory, SidecarStore.FileName);
        if (!File.Exists(sidecarPath))
        {
            return null;
        }

        var sidecar = SidecarStore.Read(sidecarPath);
        var existing = sidecar.Files.Where(f => File.Exists(Path.Combine(root, directory, f.Name))).ToList();
        if (existing.Count == 0)
        {
            return null;
        }

        string Render(SidecarFile file) => pattern.Render(new PatternInput(
            sidecar.Title,
            sidecar.Authors,
            sidecar.Year,
            sidecar.Series,
            sidecar.SeriesIndex,
            sidecar.Language,
            sidecar.Tags.FirstOrDefault(),
            Path.GetExtension(file.Name)));

        var newDirectory = DirectoryOf(Render(existing[0]));
        var moves = new List<Move>();
        var renames = new Dictionary<string, string>(StringComparer.Ordinal);
        var unchanged = 0;
        foreach (var file in existing)
        {
            var rendered = Render(file);
            var name = rendered[(rendered.LastIndexOf('/') + 1)..];
            var from = IndexWriter.JoinRelative(directory, file.Name);
            var to = IndexWriter.JoinRelative(newDirectory, name);
            renames[file.Name] = name;
            if (string.Equals(from, to, StringComparison.Ordinal))
            {
                unchanged++;
            }
            else
            {
                moves.Add(new Move(from, to, IsFile: true));
            }
        }

        var directoryChanges = !string.Equals(directory, newDirectory, StringComparison.Ordinal);
        if (directoryChanges)
        {
            moves.Add(new Move(IndexWriter.JoinRelative(directory, SidecarStore.FileName), IndexWriter.JoinRelative(newDirectory, SidecarStore.FileName), IsFile: false));
            if (File.Exists(Path.Combine(root, directory, AnnotationsFormat.FileName)))
            {
                moves.Add(new Move(IndexWriter.JoinRelative(directory, AnnotationsFormat.FileName), IndexWriter.JoinRelative(newDirectory, AnnotationsFormat.FileName), IsFile: false));
            }

            if (sidecar.Cover is { } cover && File.Exists(Path.Combine(root, directory, cover)))
            {
                moves.Add(new Move(IndexWriter.JoinRelative(directory, cover), IndexWriter.JoinRelative(newDirectory, cover), IsFile: false));
            }
        }

        var blocked = new List<OrganizeCollision>();
        var own = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var move in moves)
        {
            var absolute = LibraryPathsAbsolute(root, move.To);
            if (File.Exists(absolute) || Directory.Exists(absolute))
            {
                blocked.Add(new OrganizeCollision(id, move.From, move.To, TargetExists));
            }
            else if (claimed.Contains(move.To) || !own.Add(move.To))
            {
                blocked.Add(new OrganizeCollision(id, move.From, move.To, TargetTaken));
            }
        }

        if (blocked.Count > 0)
        {
            collisions.AddRange(blocked);
            return null;
        }

        claimed.UnionWith(own);
        return new WorkPlan(id, directory, newDirectory, sidecar, moves, renames, unchanged);
    }

    private static void Apply(string root, List<WorkPlan> plans)
    {
        var journal = FileJournal.Begin(root, "organize", undoable: true);
        try
        {
            var moved = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var plan in plans)
            {
                ApplyWork(root, journal, plan, moved);
            }

            UpdateReviewQueue(root, journal, moved);
        }
        finally
        {
            // Whatever moved is in the journal, so that undo can revert it even if a step failed.
            journal.Finish();
        }

        LibraryEditor.Reconcile(root);
    }

    private static void ApplyWork(string root, FileJournal journal, WorkPlan plan, Dictionary<string, string> moved)
    {
        foreach (var move in plan.Moves.Where(m => m.IsFile))
        {
            LibraryEditor.Move(journal, LibraryPathsAbsolute(root, move.From), LibraryPathsAbsolute(root, move.To));
            moved[move.From] = move.To;
        }

        var sidecarPath = LibraryPathsAbsolute(root, IndexWriter.JoinRelative(plan.OldDirectory, SidecarStore.FileName));
        if (plan.Renames.Any(r => r.Key != r.Value))
        {
            journal.Replace(sidecarPath, Encoding.UTF8.GetBytes(SidecarFormat.Serialize(Renamed(plan.Sidecar, plan.Renames))));
        }

        RenameInAnnotations(root, journal, plan);
        foreach (var move in plan.Moves.Where(m => !m.IsFile))
        {
            LibraryEditor.Move(journal, LibraryPathsAbsolute(root, move.From), LibraryPathsAbsolute(root, move.To));
        }

        if (plan.OldDirectory != ".")
        {
            LibraryEditor.RemoveFolderIfEmpty(root, Path.Combine(root, plan.OldDirectory));
        }
    }

    /// <summary>The annotations name their file; a renamed file keeps them (an annotations.json that cannot be read is left as it is).</summary>
    private static void RenameInAnnotations(string root, FileJournal journal, WorkPlan plan)
    {
        if (!plan.Renames.Any(r => r.Key != r.Value))
        {
            return;
        }

        var path = LibraryPathsAbsolute(root, IndexWriter.JoinRelative(plan.OldDirectory, AnnotationsFormat.FileName));
        if (!File.Exists(path))
        {
            return;
        }

        AnnotationsSidecar annotations;
        try
        {
            annotations = AnnotationsStore.Read(path);
        }
        catch (SidecarException)
        {
            return;
        }

        var renamed = annotations with { Annotations = [.. annotations.Annotations.Select(a => a with { File = plan.Renames.GetValueOrDefault(a.File, a.File) })] };
        journal.Replace(path, Encoding.UTF8.GetBytes(AnnotationsFormat.Serialize(renamed)));
    }

    private static WorkSidecar Renamed(WorkSidecar sidecar, Dictionary<string, string> renames)
    {
        string Name(string name) => renames.GetValueOrDefault(name, name);
        return sidecar with
        {
            Files = [.. sidecar.Files.Select(f => f with { Name = Name(f.Name) })],
            NotDuplicates = new NotDuplicates(
                sidecar.NotDuplicates.Works,
                [.. sidecar.NotDuplicates.Files.Select(p => new FilePair(Name(p.A), Name(p.B)))]),
        };
    }

    private static void UpdateReviewQueue(string root, FileJournal journal, Dictionary<string, string> moved)
    {
        var path = ReviewQueue.PathFor(root);
        var queue = ReviewQueue.Load(path);
        if (!queue.Entries.Any(e => e.File is { } f && moved.ContainsKey(f)))
        {
            return;
        }

        var updated = new ReviewQueue(queue.Entries.Select(e => e.File is { } f && moved.TryGetValue(f, out var to) ? e with { File = to } : e));
        journal.Replace(path, Encoding.UTF8.GetBytes(updated.Serialize()));
    }

    private static string DirectoryOf(string relative)
    {
        var slash = relative.LastIndexOf('/');
        return slash < 0 ? "." : relative[..slash];
    }

    private static string LibraryPathsAbsolute(string root, string relative) => Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));

    private sealed record Move(string From, string To, bool IsFile);

    private sealed record WorkPlan(
        Ulid Id,
        string OldDirectory,
        string NewDirectory,
        WorkSidecar Sidecar,
        List<Move> Moves,
        Dictionary<string, string> Renames,
        int Unchanged);
}
