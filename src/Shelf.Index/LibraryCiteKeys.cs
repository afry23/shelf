using Microsoft.Data.Sqlite;
using Shelf.Core.Model;

namespace Shelf.Index;

/// <summary>A cite key that was given to a work.</summary>
public sealed record CiteKeyAssignment(LibraryWork Work, string Key);

/// <summary>Cite keys that are built from author, year and title and are unique in the library.</summary>
public static class LibraryCiteKeys
{
    /// <summary>True if a work other than <paramref name="exceptId"/> has the key.</summary>
    public static bool IsTaken(SqliteConnection connection, string key, string? exceptId = null)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT 1 FROM works WHERE cite_key = $key AND id <> $except LIMIT 1";
        command.Parameters.AddWithValue("$key", key);
        command.Parameters.AddWithValue("$except", exceptId ?? string.Empty);
        return command.ExecuteScalar() is not null;
    }

    /// <summary>The key for a work: built from its metadata, not used by another work. Null if the author is unknown.</summary>
    public static string? ForNew(SqliteConnection connection, WorkSidecar sidecar)
    {
        ArgumentNullException.ThrowIfNull(sidecar);
        return CiteKeys.Suggest(sidecar.Authors, sidecar.Year, sidecar.Title) is { } suggested
            ? CiteKeys.MakeUnique(suggested, key => IsTaken(connection, key, sidecar.Id.ToString()))
            : null;
    }

    /// <summary>The key that <see cref="Assign"/> would give to the work; nothing is written. Null if the author is unknown.</summary>
    /// <exception cref="SidecarException">The sidecar is invalid.</exception>
    /// <exception cref="IndexException">There is no usable index.</exception>
    public static string? Suggest(string root, LibraryWork work)
    {
        var sidecar = LibraryEditor.Read(root, work);
        using var connection = IndexDatabase.OpenRead(IndexDatabase.PathFor(root));
        return ForNew(connection, sidecar);
    }

    /// <summary>
    /// Gives a key to <paramref name="only"/> (replacing its key), or to every work that has none. Works with an unknown author
    /// get none. The sidecars are written first, then the index is brought in line by one scan.
    /// </summary>
    /// <exception cref="SidecarException">A sidecar is invalid.</exception>
    /// <exception cref="IndexException">There is no usable index.</exception>
    public static IReadOnlyList<CiteKeyAssignment> Assign(string root, LibraryWork? only)
    {
        var owners = new Dictionary<string, string>(StringComparer.Ordinal);
        var works = new List<LibraryWork>();
        using (var connection = IndexDatabase.OpenRead(IndexDatabase.PathFor(root)))
        {
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT id, dir_path, title, cite_key FROM works ORDER BY id";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var work = new LibraryWork(Ulid.Parse(reader.GetString(0)), reader.GetString(1), reader.GetString(2));
                if (!reader.IsDBNull(3))
                {
                    owners[reader.GetString(3)] = reader.GetString(0);
                }
                else if (only is null)
                {
                    works.Add(work);
                }
            }
        }

        if (only is not null)
        {
            works.Add(only);
        }

        var assigned = new List<CiteKeyAssignment>();
        foreach (var work in works)
        {
            var path = LibraryEditor.SidecarPath(root, work);
            var sidecar = SidecarStore.Read(path);
            if (CiteKeys.Suggest(sidecar.Authors, sidecar.Year, sidecar.Title) is not { } suggested)
            {
                continue;
            }

            var id = work.Id.ToString();
            var key = CiteKeys.MakeUnique(suggested, k => owners.TryGetValue(k, out var owner) && owner != id);
            if (sidecar.CiteKey is { } old && owners.TryGetValue(old, out var oldOwner) && oldOwner == id)
            {
                owners.Remove(old);
            }

            owners[key] = id;
            if (sidecar.CiteKey != key)
            {
                SidecarStore.Write(path, sidecar with { CiteKey = key });
            }

            assigned.Add(new CiteKeyAssignment(work, key));
        }

        if (assigned.Count > 0)
        {
            LibraryEditor.Reconcile(root);
        }

        return assigned;
    }
}
