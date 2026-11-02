using System.Globalization;
using System.Text;
using Shelf.Core.Model;
using Shelf.Core.Search;
using Shelf.Organize;

namespace Shelf.Index;

/// <summary>One field of a work that a bulk edit changes: the value before and after, or null for "none" (F-BLK-1).</summary>
public sealed record BulkFieldChange(string Field, string? From, string? To);

/// <summary>A work that a bulk edit changes, with its changed fields.</summary>
public sealed record BulkWorkChange(Ulid Work, string Title, IReadOnlyList<BulkFieldChange> Fields);

/// <summary>What a bulk edit does or would do. <paramref name="Matched"/> counts the selected works, <paramref name="Changes"/> those that change.</summary>
public sealed record BulkEditOutcome(int Matched, IReadOnlyList<BulkWorkChange> Changes, bool Applied);

/// <summary>
/// One change for many works (F-BLK-1, F-BLK-2): the works are selected by queries that are joined by OR, the change is shown
/// first and written on request, all in one journal, so that <c>shelf undo</c> takes it back as a whole.
/// </summary>
public static class LibraryBulkEdit
{
    /// <summary>The fields that a bulk edit may change, named as in <c>book.json</c> and the web API.</summary>
    public static IReadOnlyList<string> Fields { get; } = ["authors", "year", "language", "publisher", "series", "edition", "rating", "readStatus", "tags"];

    /// <summary>The works that at least one of the queries finds, each once, in the order of their first appearance.</summary>
    /// <exception cref="IndexException">There is no usable index.</exception>
    public static IReadOnlyList<Ulid> Select(string root, IEnumerable<SearchQuery> queries)
    {
        ArgumentNullException.ThrowIfNull(queries);
        var seen = new HashSet<Ulid>();
        var selected = new List<Ulid>();
        foreach (var query in queries)
        {
            foreach (var id in LibraryTags.Find(root, query))
            {
                if (seen.Add(id))
                {
                    selected.Add(id);
                }
            }
        }

        return selected;
    }

    /// <summary>
    /// Applies <paramref name="edit"/> to the sidecar of each work. Only the <paramref name="fields"/> are compared to find out what
    /// changes. Without <paramref name="apply"/> nothing is written. Every new sidecar is checked first; one that is refused changes nothing.
    /// </summary>
    /// <exception cref="LibraryEditException">A work does not exist.</exception>
    /// <exception cref="SidecarException">A sidecar would become invalid.</exception>
    public static BulkEditOutcome Run(string root, IReadOnlyCollection<Ulid> works, IReadOnlyList<string> fields, Func<WorkSidecar, WorkSidecar> edit, bool apply, string label)
    {
        ArgumentNullException.ThrowIfNull(works);
        ArgumentNullException.ThrowIfNull(fields);
        ArgumentNullException.ThrowIfNull(edit);
        root = LibraryTags.Normalize(root);
        var directories = LibraryTags.Directories(root);
        var changes = new List<BulkWorkChange>();
        var replacements = new List<(string Path, byte[] Content)>();
        foreach (var id in works)
        {
            if (!directories.TryGetValue(id, out var directory))
            {
                throw new LibraryEditException($"no work {id}");
            }

            var path = Path.Combine(root, directory, SidecarStore.FileName);
            var before = SidecarStore.Read(path);
            var after = edit(before);
            SidecarFormat.Validate(after, path);
            var changed = fields
                .Select(f => new BulkFieldChange(f, Value(before, f), Value(after, f)))
                .Where(c => !string.Equals(c.From, c.To, StringComparison.Ordinal))
                .ToList();
            if (changed.Count == 0)
            {
                continue;
            }

            changes.Add(new BulkWorkChange(id, before.Title, changed));
            replacements.Add((path, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(SidecarFormat.Serialize(after))));
        }

        if (!apply || replacements.Count == 0)
        {
            return new BulkEditOutcome(works.Count, changes, Applied: false);
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
        return new BulkEditOutcome(works.Count, changes, Applied: true);
    }

    /// <summary>The value of a field as text, or null if the work has none.</summary>
    public static string? Value(WorkSidecar work, string field)
    {
        ArgumentNullException.ThrowIfNull(work);
        var text = field switch
        {
            "authors" => string.Join("; ", work.Authors),
            "year" => work.Year?.ToString(CultureInfo.InvariantCulture),
            "language" => work.Language,
            "publisher" => work.Publisher,
            "series" => work.Series,
            "edition" => work.Edition,
            "rating" => work.Rating?.ToString(CultureInfo.InvariantCulture),
            "readStatus" => work.ReadStatus.ToString().ToLowerInvariant(),
            "tags" => string.Join(", ", work.Tags),
            _ => throw new ArgumentException($"unknown field '{field}'", nameof(field)),
        };
        return string.IsNullOrEmpty(text) ? null : text;
    }
}
