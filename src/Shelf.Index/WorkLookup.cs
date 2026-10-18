using Microsoft.Data.Sqlite;
using Shelf.Core.Model;

namespace Shelf.Index;

/// <summary>A command could not do what was asked of the library; the message says why.</summary>
public sealed class LibraryEditException(string message) : Exception(message);

/// <summary>A work of the index, located by its folder (library-relative, with <c>/</c>).</summary>
public sealed record LibraryWork(Ulid Id, string Directory, string Title);

/// <summary>Finds the work that a command line argument names.</summary>
public static class WorkLookup
{
    /// <summary>
    /// A reference is the ID of a work or the start of one (at least one character of the alphabet of IDs; it must name
    /// exactly one work), or the path of a work folder or of a file in it, relative to the working directory or to
    /// the library root.
    /// </summary>
    /// <exception cref="LibraryEditException">No work, or several works, match.</exception>
    /// <exception cref="IndexException">There is no usable index.</exception>
    public static LibraryWork Resolve(string root, string reference, string workingDirectory)
    {
        ArgumentException.ThrowIfNullOrEmpty(root);
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentException.ThrowIfNullOrEmpty(workingDirectory);
        root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        using var connection = IndexDatabase.OpenRead(IndexDatabase.PathFor(root));

        if (LooksLikeId(reference))
        {
            var prefix = reference.ToUpperInvariant();
            var found = Query(connection, "SELECT id, dir_path, title FROM works WHERE substr(id, 1, $n) = $p ORDER BY id", ("$n", prefix.Length), ("$p", prefix));
            if (found.Count == 1)
            {
                return found[0];
            }

            if (found.Count > 1)
            {
                throw new LibraryEditException($"'{reference}' matches {found.Count} works; use more of the ID");
            }
        }

        if (reference.Length > 0)
        {
            foreach (var baseDirectory in new[] { workingDirectory, root })
            {
                var full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(reference, baseDirectory));
                var relative = Path.GetRelativePath(root, full);
                if (Path.IsPathRooted(relative) || relative == ".." || relative.StartsWith("../", StringComparison.Ordinal) || relative.StartsWith("..\\", StringComparison.Ordinal) || relative == ".")
                {
                    continue;
                }

                relative = relative.Replace('\\', '/');
                var work = Query(connection, "SELECT id, dir_path, title FROM works WHERE dir_path = $d", ("$d", relative)).FirstOrDefault()
                    ?? Query(connection, "SELECT w.id, w.dir_path, w.title FROM files f JOIN works w ON w.id = f.work_id WHERE f.rel_path = $p", ("$p", relative)).FirstOrDefault();
                if (work is not null)
                {
                    return work;
                }
            }
        }

        throw new LibraryEditException($"no work matches '{reference}'");
    }

    /// <summary>"Title" or "Title (Year)" of works, for output. Works the index does not know, or no index at all, give no entry.</summary>
    public static Dictionary<Ulid, string> Labels(string root, IEnumerable<Ulid> ids)
    {
        var labels = new Dictionary<Ulid, string>();
        try
        {
            using var connection = IndexDatabase.OpenRead(IndexDatabase.PathFor(Path.GetFullPath(root)));
            foreach (var id in ids.Distinct())
            {
                using var command = connection.CreateCommand();
                command.CommandText = "SELECT title, year FROM works WHERE id = $id";
                command.Parameters.AddWithValue("$id", id.ToString());
                using var reader = command.ExecuteReader();
                if (reader.Read())
                {
                    labels[id] = reader.IsDBNull(1) ? reader.GetString(0) : $"{reader.GetString(0)} ({reader.GetInt32(1).ToString(System.Globalization.CultureInfo.InvariantCulture)})";
                }
            }
        }
        catch (IndexException)
        {
            // Without an index there are no titles; the IDs are still shown.
        }

        return labels;
    }

    private static bool LooksLikeId(string text) =>
        text.Length is >= 1 and <= Ulid.TextLength && text.All(c => char.IsAsciiDigit(c) || (char.IsAsciiLetter(c) && "ILOUilou".IndexOf(c, StringComparison.Ordinal) < 0));

    private static List<LibraryWork> Query(SqliteConnection connection, string sql, params (string Name, object Value)[] parameters)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        var works = new List<LibraryWork>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            works.Add(new LibraryWork(Ulid.Parse(reader.GetString(0)), reader.GetString(1), reader.GetString(2)));
        }

        return works;
    }
}
