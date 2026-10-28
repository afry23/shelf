using System.Text;
using Shelf.Core.Model;
using Shelf.Core.Review;
using Shelf.Organize;

namespace Shelf.Index;

/// <summary>What <see cref="LibraryDeleter"/> did, or would do.</summary>
/// <param name="Files">The names of the files of the work that go to the trash.</param>
/// <param name="WorkRemoved">The work goes too: its sidecar, cover and notes are moved to the trash with its last file.</param>
public sealed record DeleteResult(Ulid Work, IReadOnlyList<string> Files, bool WorkRemoved)
{
    public int Trashed => Files.Count;
}

/// <summary>
/// Deleting a file or a whole work (F-DUP-7). Nothing is deleted for good: everything goes to <c>.shelf/trash/</c> through
/// one journal that <c>shelf undo</c> can reverse (F-DUP-4). Deleting the last file of a work deletes the work.
/// </summary>
public static class LibraryDeleter
{
    /// <summary>What deleting this file would do; nothing is changed.</summary>
    /// <param name="file">A file name of the work, or the path of the file.</param>
    /// <exception cref="LibraryEditException"/>
    public static DeleteResult PlanFile(string root, LibraryWork work, string file, string workingDirectory)
    {
        var sidecar = LibraryEditor.Read(root, work);
        var entry = LibraryEditor.FindFile(root, work, sidecar, file, workingDirectory)
            ?? throw new LibraryEditException($"{work.Id} has no file '{file}'");
        return new DeleteResult(work.Id, [entry.Name], WorkRemoved: sidecar.Files.Count < 2);
    }

    /// <summary>What deleting this work would do; nothing is changed.</summary>
    public static DeleteResult PlanWork(string root, LibraryWork work) =>
        new(work.Id, [.. LibraryEditor.Read(root, work).Files.Select(f => f.Name)], WorkRemoved: true);

    /// <summary>Moves one file of a work to the trash. The last file of a work takes the work along.</summary>
    /// <param name="file">A file name of the work, or the path of the file.</param>
    /// <exception cref="LibraryEditException"/>
    public static DeleteResult DeleteFile(string root, LibraryWork work, string file, string workingDirectory)
    {
        var plan = PlanFile(root, work, file, workingDirectory);
        if (plan.WorkRemoved)
        {
            return DeleteWork(root, work);
        }

        var name = plan.Files[0];
        var sidecarPath = LibraryEditor.SidecarPath(root, work);
        var sidecar = SidecarStore.Read(sidecarPath);
        var directory = Path.Combine(root, work.Directory);
        var journal = FileJournal.Begin(root, "delete", undoable: true);
        try
        {
            journal.Trash(Path.Combine(directory, name));
            var updated = sidecar with
            {
                Files = [.. sidecar.Files.Where(f => f.Name != name)],
                NotDuplicates = sidecar.NotDuplicates with
                {
                    Files = [.. sidecar.NotDuplicates.Files.Where(p => p.A != name && p.B != name)],
                },
            };
            SidecarFormat.Validate(updated, sidecarPath);
            journal.Replace(sidecarPath, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(SidecarFormat.Serialize(updated)));
            DropNotes(root, work, journal, name);
        }
        finally
        {
            journal.Finish();
        }

        DropFromQueue(root, work.Id, IndexWriter.JoinRelative(work.Directory, name));
        LibraryEditor.Reconcile(root);
        return plan;
    }

    /// <summary>Moves a work with all its files, its sidecar, cover and notes to the trash.</summary>
    /// <exception cref="LibraryEditException"/>
    public static DeleteResult DeleteWork(string root, LibraryWork work)
    {
        var sidecar = LibraryEditor.Read(root, work);
        var directory = Path.Combine(root, work.Directory);
        var journal = FileJournal.Begin(root, "delete", undoable: true);
        try
        {
            foreach (var file in sidecar.Files)
            {
                journal.Trash(Path.Combine(directory, file.Name));
            }

            if (sidecar.Cover is { } cover)
            {
                journal.Trash(Path.Combine(directory, cover));
            }

            journal.Trash(Path.Combine(directory, AnnotationsFormat.FileName));
            journal.Trash(LibraryEditor.SidecarPath(root, work));
        }
        finally
        {
            journal.Finish();
        }

        if (work.Directory != ".")
        {
            LibraryEditor.RemoveFolderIfEmpty(root, directory);
        }

        DropFromQueue(root, work.Id, file: null);
        LibraryEditor.Reconcile(root);
        return new DeleteResult(work.Id, [.. sidecar.Files.Select(f => f.Name)], WorkRemoved: true);
    }

    /// <summary>The notes of the deleted file go; those of the other files stay. The old file is kept for undo.</summary>
    private static void DropNotes(string root, LibraryWork work, FileJournal journal, string name)
    {
        var path = LibraryAnnotations.PathOf(root, work);
        AnnotationsSidecar notes;
        try
        {
            notes = AnnotationsStore.Read(path);
        }
        catch (SidecarException)
        {
            return; // notes that cannot be read stay as they are
        }

        var staying = notes.Annotations.Where(a => a.File != name).ToList();
        if (staying.Count == notes.Annotations.Count)
        {
            return;
        }

        if (staying.Count == 0 && notes.UnknownFields.Count == 0)
        {
            journal.Trash(path);
            return;
        }

        var rest = notes with { Annotations = staying };
        AnnotationsFormat.Validate(rest, path);
        journal.Replace(path, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(AnnotationsFormat.Serialize(rest)));
    }

    /// <summary>Review entries about the deleted file, or all about the deleted work, are settled; candidates that name the work go.</summary>
    private static void DropFromQueue(string root, Ulid work, string? file)
    {
        var queue = LibraryEditor.LoadQueue(root);
        var entries = new List<ReviewEntry>();
        foreach (var entry in queue.Entries)
        {
            if (file is null ? entry.Work == work : entry.File == file)
            {
                continue;
            }

            if (file is null && entry.Candidates is { } candidates && candidates.Any(c => c.Work == work))
            {
                var rest = candidates.Where(c => c.Work != work).ToList();
                if (entry.Kind == ReviewKind.Grouping && rest.Count == 0)
                {
                    continue;
                }

                entries.Add(entry with { Candidates = rest });
                continue;
            }

            entries.Add(entry);
        }

        if (entries.Count != queue.Entries.Count || file is null)
        {
            LibraryEditor.SaveQueue(root, new ReviewQueue(entries));
        }
    }
}
