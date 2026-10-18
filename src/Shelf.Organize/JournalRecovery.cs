namespace Shelf.Organize;

/// <param name="Journals">Unfinished journals that were closed.</param>
/// <param name="Completed">Pending steps whose effect was found on disk and are now <c>done</c>.</param>
/// <param name="Dropped">Pending steps that never happened and were removed.</param>
/// <param name="Problems">Steps that could not be explained, for example a file that is gone from both places.</param>
public sealed record RecoveryReport(int Journals, int Completed, int Dropped, IReadOnlyList<string> Problems);

/// <summary>
/// Closes journals that a crash left <c>running</c>: each pending step is compared with the disk, so the journal
/// describes exactly what happened and <see cref="JournalUndo"/> can still revert it.
/// </summary>
public static class JournalRecovery
{
    /// <exception cref="JournalException"/>
    public static RecoveryReport Recover(string root)
    {
        root = FileJournal.Normalize(root);
        var directory = LibraryPaths.JournalDirectory(root);
        if (!Directory.Exists(directory))
        {
            return new RecoveryReport(0, 0, 0, []);
        }

        int journals = 0, completed = 0, dropped = 0;
        var problems = new List<string>();
        foreach (var path in Directory.EnumerateFiles(directory, "*.json").Order(StringComparer.Ordinal))
        {
            var data = JournalFormat.Parse(path);
            if (data.State != JournalState.Running)
            {
                continue;
            }

            for (var i = data.Ops.Count - 1; i >= 0; i--)
            {
                var op = data.Ops[i];
                if (op.Status != JournalOpStatus.Pending)
                {
                    continue;
                }

                var happened = op.Kind == JournalOpKind.Mkdir
                    ? Directory.Exists(LibraryPaths.Absolute(root, op.To))
                    : !File.Exists(LibraryPaths.Absolute(root, op.From)) && File.Exists(LibraryPaths.Absolute(root, op.To));
                if (happened)
                {
                    data.Ops[i] = op with { Status = JournalOpStatus.Done };
                    completed++;
                    continue;
                }

                if (op.Kind != JournalOpKind.Mkdir && !File.Exists(LibraryPaths.Absolute(root, op.From)))
                {
                    problems.Add($"{op.From}: gone from both places after a crash ({System.IO.Path.GetFileName(path)})");
                }

                data.Ops.RemoveAt(i);
                dropped++;
            }

            data.State = JournalState.Finished;
            LibraryPaths.WriteAtomic(path, JournalFormat.Serialize(data));
            journals++;
        }

        return new RecoveryReport(journals, completed, dropped, problems);
    }
}
