namespace Shelf.Organize;

public sealed record UndoSkip(string Path, string Reason);

/// <param name="Journal">File name of the reverted journal.</param>
/// <param name="Reverted">Number of files moved back.</param>
/// <param name="Skipped">Files that were not touched, with the reason (taken, missing or changed since).</param>
public sealed record UndoResult(string Journal, string Label, int Reverted, IReadOnlyList<UndoSkip> Skipped);

/// <summary>Reverts the last undoable run (F-ORG-6) and refuses to overwrite or to move a file that changed since.</summary>
public static class JournalUndo
{
    /// <summary>The label of the run that <see cref="Undo"/> would revert now (<c>organize</c>, <c>tag rename</c>, ...), or <c>null</c>.</summary>
    /// <exception cref="JournalException"/>
    public static string? NextLabel(string root)
    {
        root = FileJournal.Normalize(root);
        JournalRecovery.Recover(root);
        var directory = LibraryPaths.JournalDirectory(root);
        if (!Directory.Exists(directory))
        {
            return null;
        }

        foreach (var path in Directory.EnumerateFiles(directory, "*.json").Order(StringComparer.Ordinal).Reverse())
        {
            var data = JournalFormat.Parse(path);
            if (data.Undoable && data.State == JournalState.Finished && data.Ops.Any(o => o.Status == JournalOpStatus.Done))
            {
                return data.Label;
            }
        }

        return null;
    }

    /// <returns><c>null</c> if there is nothing to undo.</returns>
    /// <exception cref="JournalException"/>
    public static UndoResult? Undo(string root)
    {
        root = FileJournal.Normalize(root);
        JournalRecovery.Recover(root);
        var directory = LibraryPaths.JournalDirectory(root);
        if (!Directory.Exists(directory))
        {
            return null;
        }

        foreach (var path in Directory.EnumerateFiles(directory, "*.json").Order(StringComparer.Ordinal).Reverse())
        {
            var data = JournalFormat.Parse(path);
            if (data.Undoable && data.State == JournalState.Finished && data.Ops.Any(o => o.Status == JournalOpStatus.Done))
            {
                return Revert(root, path, data);
            }
        }

        return null;
    }

    private static UndoResult Revert(string root, string path, JournalData data)
    {
        var skipped = new List<UndoSkip>();
        var reverted = 0;
        for (var i = data.Ops.Count - 1; i >= 0; i--)
        {
            var op = data.Ops[i];
            if (op.Status != JournalOpStatus.Done)
            {
                continue;
            }

            if (op.Kind == JournalOpKind.Mkdir)
            {
                var folder = LibraryPaths.Absolute(root, op.To);
                if (Directory.Exists(folder) && !Directory.EnumerateFileSystemEntries(folder).Any())
                {
                    Directory.Delete(folder);
                }

                data.Ops[i] = op with { Status = JournalOpStatus.Undone };
                continue;
            }

            var current = LibraryPaths.Absolute(root, op.To);
            if (op.Kind == JournalOpKind.Create)
            {
                var problem = !File.Exists(current) ? "missing" : LibraryPaths.Hash(current) != op.Sha256 ? "changed since" : null;
                if (problem is not null)
                {
                    skipped.Add(new UndoSkip(op.To, problem));
                    continue;
                }

                File.Delete(current);
                data.Ops[i] = op with { Status = JournalOpStatus.Undone };
                reverted++;
                LibraryPaths.WriteAtomic(path, JournalFormat.Serialize(data));
                continue;
            }

            var original = LibraryPaths.Absolute(root, op.From);
            string? reason = null;
            if (!File.Exists(current))
            {
                reason = "missing";
            }
            else if (File.Exists(original) || Directory.Exists(original))
            {
                reason = "target taken";
            }
            else if (op.Sha256 is not null && LibraryPaths.Hash(current) != op.Sha256)
            {
                reason = "changed since";
            }

            if (reason is not null)
            {
                skipped.Add(new UndoSkip(op.To, reason));
                continue;
            }

            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(original)!);
            File.Move(current, original, overwrite: false);
            data.Ops[i] = op with { Status = JournalOpStatus.Undone };
            reverted++;
            LibraryPaths.WriteAtomic(path, JournalFormat.Serialize(data));
        }

        if (data.Ops.All(o => o.Status == JournalOpStatus.Undone))
        {
            data.State = JournalState.Undone;
        }

        LibraryPaths.WriteAtomic(path, JournalFormat.Serialize(data));
        PruneEmpty(LibraryPaths.TrashDirectory(root));
        return new UndoResult(System.IO.Path.GetFileName(path), data.Label, reverted, skipped);
    }

    private static void PruneEmpty(string directory)
    {
        if (!Directory.Exists(directory))
        {
            return;
        }

        foreach (var child in Directory.EnumerateDirectories(directory))
        {
            PruneEmpty(child);
            if (!Directory.EnumerateFileSystemEntries(child).Any())
            {
                Directory.Delete(child);
            }
        }
    }
}
