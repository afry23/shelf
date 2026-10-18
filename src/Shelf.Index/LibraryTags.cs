using System.Text;
using Shelf.Core.Model;
using Shelf.Core.Search;
using Shelf.Organize;

namespace Shelf.Index;

/// <summary>A tag and the number of works that carry exactly this tag (F-TAG-5).</summary>
public sealed record TagCount(string Tag, int Works);

/// <param name="Works">Works that were looked at.</param>
/// <param name="Changed">Works whose tags changed.</param>
public sealed record TagResult(int Works, int Changed);

/// <summary>
/// Tags across the library (F-TAG-2 to F-TAG-5). The tags live in the sidecars; a change replaces each changed
/// sidecar through one undoable journal and brings the index in line once at the end.
/// </summary>
public static class LibraryTags
{
    /// <exception cref="IndexException">There is no usable index.</exception>
    public static IReadOnlyList<TagCount> List(string root)
    {
        using var connection = IndexDatabase.OpenRead(IndexDatabase.PathFor(Normalize(root)));
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT t.path, COUNT(wt.work_id) FROM tags t JOIN work_tags wt ON wt.tag_id = t.id GROUP BY t.id ORDER BY t.path";
        using var reader = command.ExecuteReader();
        var tags = new List<TagCount>();
        while (reader.Read())
        {
            tags.Add(new TagCount(reader.GetString(0), reader.GetInt32(1)));
        }

        return tags;
    }

    /// <summary>The works that a search finds (the search language of <c>shelf search</c>).</summary>
    public static IReadOnlyList<Ulid> Find(string root, SearchQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);
        using var connection = IndexDatabase.OpenRead(IndexDatabase.PathFor(Normalize(root)));
        return [.. LibrarySearch.Search(connection, query, int.MaxValue, hitsPerWork: 1).Select(h => h.WorkId)];
    }

    /// <exception cref="LibraryEditException">No work was named.</exception>
    public static TagResult Add(string root, string tag, IReadOnlyCollection<Ulid> works) =>
        Change(root, $"tag add", works, tags => tags.Contains(tag) ? null : [.. tags, tag]);

    /// <summary>Removes exactly this tag; the tags below it stay.</summary>
    public static TagResult Remove(string root, string tag, IReadOnlyCollection<Ulid> works) =>
        Change(root, "tag rm", works, tags => tags.Contains(tag) ? [.. tags.Where(t => t != tag)] : null);

    /// <summary>Renames the tag and the tags below it on every work. The new name must not be in use.</summary>
    /// <exception cref="LibraryEditException">No work has the tag, or the new name is in use.</exception>
    public static TagResult Rename(string root, string from, string to)
    {
        var existing = AllTags(root);
        RequireTag(existing, from);
        if (existing.Any(t => TagNames.IsSameOrBelow(t, to)))
        {
            throw new LibraryEditException($"tag '{to}' already exists; use tag merge");
        }

        return Move(root, "tag rename", from, to);
    }

    /// <summary>Puts the tag and the tags below it into <paramref name="into"/>, which may exist; a work that has both keeps one.</summary>
    /// <exception cref="LibraryEditException">No work has the tag.</exception>
    public static TagResult Merge(string root, string from, string into)
    {
        RequireTag(AllTags(root), from);
        return Move(root, "tag merge", from, into);
    }

    private static TagResult Move(string root, string label, string from, string to) =>
        Change(
            root,
            label,
            works: null,
            tags => tags.Any(t => TagNames.IsSameOrBelow(t, from))
                ? [.. tags.Select(t => TagNames.IsSameOrBelow(t, from) ? TagNames.Move(t, from, to) : t).Distinct(StringComparer.Ordinal)]
                : null);

    private static void RequireTag(List<string> existing, string tag)
    {
        if (!existing.Any(t => TagNames.IsSameOrBelow(t, tag)))
        {
            throw new LibraryEditException($"no work has the tag '{tag}'");
        }
    }

    private static List<string> AllTags(string root) => [.. List(root).Select(t => t.Tag)];

    /// <param name="works">The works to change, or <c>null</c> for all.</param>
    /// <param name="change">The new tags of a work, or <c>null</c> if it stays as it is.</param>
    private static TagResult Change(string root, string label, IReadOnlyCollection<Ulid>? works, Func<IReadOnlyList<string>, IReadOnlyList<string>?> change)
    {
        root = Normalize(root);
        var directories = Directories(root);
        var selected = works is null ? directories.Keys.ToList() : [.. works];
        var replacements = new List<(string Path, byte[] Content)>();
        foreach (var id in selected)
        {
            if (!directories.TryGetValue(id, out var directory))
            {
                throw new LibraryEditException($"no work {id}");
            }

            var path = Path.Combine(root, directory, SidecarStore.FileName);
            var sidecar = SidecarStore.Read(path);
            if (change(sidecar.Tags) is { } tags)
            {
                var updated = sidecar with { Tags = tags };
                SidecarFormat.Validate(updated, path);
                replacements.Add((path, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(SidecarFormat.Serialize(updated))));
            }
        }

        if (replacements.Count == 0)
        {
            return new TagResult(selected.Count, 0);
        }

        var journal = FileJournal.Begin(root, label, undoable: true);
        try
        {
            foreach (var (path, content) in replacements)
            {
                journal.Replace(path, content);
            }
        }
        finally
        {
            // Whatever was replaced is in the journal, so that undo can revert it even if a step failed.
            journal.Finish();
        }

        LibraryEditor.Reconcile(root);
        return new TagResult(selected.Count, replacements.Count);
    }

    private static Dictionary<Ulid, string> Directories(string root)
    {
        using var connection = IndexDatabase.OpenRead(IndexDatabase.PathFor(root));
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, dir_path FROM works";
        using var reader = command.ExecuteReader();
        var directories = new Dictionary<Ulid, string>();
        while (reader.Read())
        {
            directories[Ulid.Parse(reader.GetString(0))] = reader.GetString(1);
        }

        return directories;
    }

    private static string Normalize(string root)
    {
        ArgumentException.ThrowIfNullOrEmpty(root);
        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
    }
}
