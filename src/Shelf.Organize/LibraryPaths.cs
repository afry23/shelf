using System.Security.Cryptography;

namespace Shelf.Organize;

internal static class LibraryPaths
{
    public static string JournalDirectory(string root) => Path.Combine(root, ".shelf", "journal");

    public static string TrashDirectory(string root) => Path.Combine(root, ".shelf", "trash");

    /// <summary>The path relative to <paramref name="root"/> with '/' separators (PRD 9.3: nothing outside the root).</summary>
    /// <exception cref="JournalException">The path is outside the root.</exception>
    public static string Relative(string root, string fullPath)
    {
        var relative = Path.GetRelativePath(root, fullPath);
        if (relative == ".." || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal) || Path.IsPathRooted(relative))
        {
            throw new JournalException($"'{fullPath}' is outside the library");
        }

        return relative.Replace(Path.DirectorySeparatorChar, '/');
    }

    public static string Absolute(string root, string relative) => Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));

    public static string Hash(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexStringLower(SHA256.HashData(stream));
    }

    /// <summary>Writes through a temporary file, flushed to disk, so a crash leaves the old or the new content.</summary>
    public static void WriteAtomic(string path, byte[] content)
    {
        var temporary = $"{path}.{Guid.NewGuid():N}.tmp";
        using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write))
        {
            stream.Write(content);
            stream.Flush(flushToDisk: true);
        }

        File.Move(temporary, path, overwrite: true);
    }
}
