namespace Shelf.Organize;

/// <summary>
/// The journals of finished runs, and the old versions of sidecars they keep for undo, would grow without end. Only
/// the newest <see cref="DefaultKeep"/> journals are kept, each with its <c>&lt;timestamp&gt;.backup/</c> folder; older
/// ones are deleted when a new run begins. A run that is older than that can no longer be undone. Files that such a
/// run moved to the trash stay there until <c>shelf trash empty</c>.
/// </summary>
public static class JournalRetention
{
    public const int DefaultKeep = 50;

    /// <summary>Deletes the oldest journals beyond <paramref name="keep"/>; journals that are still running are never deleted.</summary>
    /// <returns>The number of journals deleted.</returns>
    public static int Prune(string root, int keep = DefaultKeep)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(keep, 1);
        var directory = LibraryPaths.JournalDirectory(FileJournal.Normalize(root));
        if (!Directory.Exists(directory))
        {
            return 0;
        }

        var removed = 0;
        foreach (var path in Directory.EnumerateFiles(directory, "*.json").Order(StringComparer.Ordinal).SkipLast(keep))
        {
            if (JournalFormat.Parse(path).State == JournalState.Running)
            {
                continue;
            }

            File.Delete(path);
            var backup = Path.ChangeExtension(path, ".backup");
            if (Directory.Exists(backup))
            {
                Directory.Delete(backup, recursive: true);
            }

            removed++;
        }

        return removed;
    }
}
