namespace Shelf.Organize;

public sealed record TrashEmptyResult(int Files, long Bytes);

/// <summary>Permanent deletion of <c>.shelf/trash/</c> (F-DUP-4); the only place that deletes for good.</summary>
public static class TrashEmptier
{
    /// <summary>What is in the trash: the number of files and their size.</summary>
    public static TrashEmptyResult Measure(string root)
    {
        var trash = LibraryPaths.TrashDirectory(FileJournal.Normalize(root));
        if (!Directory.Exists(trash))
        {
            return new TrashEmptyResult(0, 0);
        }

        var files = 0;
        long bytes = 0;
        foreach (var file in Directory.EnumerateFiles(trash, "*", SearchOption.AllDirectories))
        {
            files++;
            bytes += new FileInfo(file).Length;
        }

        return new TrashEmptyResult(files, bytes);
    }

    /// <summary>Deletes everything in the trash for good; returns what was there.</summary>
    public static TrashEmptyResult Empty(string root)
    {
        var result = Measure(root);
        var trash = LibraryPaths.TrashDirectory(FileJournal.Normalize(root));
        if (!Directory.Exists(trash))
        {
            return result;
        }

        foreach (var entry in Directory.EnumerateFileSystemEntries(trash))
        {
            if (Directory.Exists(entry))
            {
                Directory.Delete(entry, recursive: true);
            }
            else
            {
                File.Delete(entry);
            }
        }

        return result;
    }
}
