using Shelf.Core.Model;

namespace Shelf.Index;

/// <summary>A work that is linked with another one, with the note of the side that is asked about (F-LNK-5).</summary>
public sealed record LinkedWork(Ulid Id, string Title, IReadOnlyList<string> Authors, int? Year, string Kind, string? Note);

/// <summary>What <see cref="LibraryLinks.Add"/> or <see cref="LibraryLinks.Remove"/> did.</summary>
/// <param name="Changed">A link was made or removed, or a note changed.</param>
public sealed record LinkChange(bool Changed);

/// <summary>
/// Links that a person made between works by hand (F-LNK-1 to F-LNK-4). The link is written on both sidecars, each with the
/// note of its side; the index mirrors it (<c>work_links</c>) and <c>reindex</c> restores it.
/// </summary>
public static class LibraryLinks
{
    /// <summary>
    /// Links two works. On an existing link the notes that are given replace the old ones; <c>null</c> leaves a note as it is,
    /// an empty text removes it.
    /// </summary>
    /// <param name="note">The note of <paramref name="a"/>, about <paramref name="b"/>.</param>
    /// <param name="otherNote">The note of <paramref name="b"/>, about <paramref name="a"/>.</param>
    /// <exception cref="LibraryEditException">The works are the same, or a note is too long or has several lines.</exception>
    public static LinkChange Add(string root, LibraryWork a, LibraryWork b, string? note, string? otherNote)
    {
        if (a.Id == b.Id)
        {
            throw new LibraryEditException("a work cannot be linked to itself");
        }

        var first = Clean(note);
        var second = Clean(otherNote);
        var changed = Apply(root, a, b, first);
        changed |= Apply(root, b, a, second);
        if (changed)
        {
            LibraryEditor.Reconcile(root);
        }

        return new LinkChange(changed);
    }

    /// <summary>Takes the link away from both works.</summary>
    /// <exception cref="LibraryEditException">The works are not linked.</exception>
    public static LinkChange Remove(string root, LibraryWork a, LibraryWork b, bool mustExist = true)
    {
        var changed = false;
        foreach (var (work, other) in new[] { (a, b), (b, a) })
        {
            var path = LibraryEditor.SidecarPath(root, work);
            var sidecar = SidecarStore.Read(path);
            if (sidecar.Links.All(l => l.Work != other.Id))
            {
                continue;
            }

            SidecarStore.Write(path, sidecar with { Links = [.. sidecar.Links.Where(l => l.Work != other.Id)] });
            changed = true;
        }

        if (!changed && mustExist)
        {
            throw new LibraryEditException($"\"{a.Title}\" and \"{b.Title}\" are not linked");
        }

        if (changed)
        {
            LibraryEditor.Reconcile(root);
        }

        return new LinkChange(changed);
    }

    /// <summary>The links of a work as the index has them: only works that exist, ordered by title.</summary>
    public static IReadOnlyList<LinkedWork> List(string root, Ulid work)
    {
        using var connection = IndexDatabase.OpenRead(IndexDatabase.PathFor(root));
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT o.id, o.title, o.authors_json, o.year, o.kind, l.note
            FROM work_links l JOIN works o ON o.id = l.other_id
            WHERE l.work_id = $id
            ORDER BY o.title_norm, o.id
            """;
        command.Parameters.AddWithValue("$id", work.ToString());
        using var reader = command.ExecuteReader();
        var result = new List<LinkedWork>();
        while (reader.Read())
        {
            using var authors = System.Text.Json.JsonDocument.Parse(reader.GetString(2));
            result.Add(new LinkedWork(
                Ulid.Parse(reader.GetString(0)),
                reader.GetString(1),
                [.. authors.RootElement.EnumerateArray().Select(a => a.GetString() ?? string.Empty)],
                reader.IsDBNull(3) ? null : reader.GetInt32(3),
                reader.GetString(4),
                reader.IsDBNull(5) ? null : reader.GetString(5)));
        }

        return result;
    }

    /// <summary>The links of the index that point to no work (F-LNK-4), as work ID and the missing ID.</summary>
    public static IReadOnlyList<(Ulid Work, Ulid Missing)> Dangling(string root)
    {
        using var connection = IndexDatabase.OpenRead(IndexDatabase.PathFor(root));
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT work_id, other_id FROM work_links WHERE other_id NOT IN (SELECT id FROM works) ORDER BY work_id, other_id";
        using var reader = command.ExecuteReader();
        var result = new List<(Ulid, Ulid)>();
        while (reader.Read())
        {
            if (Ulid.TryParse(reader.GetString(0), out var work) && Ulid.TryParse(reader.GetString(1), out var missing))
            {
                result.Add((work, missing));
            }
        }

        return result;
    }

    /// <summary>
    /// A work is deleted: the other works forget their link to it (F-LNK-4). Call before the work leaves the index. The index is
    /// brought up to date by the caller.
    /// </summary>
    internal static void ForgetWork(string root, LibraryWork gone)
    {
        foreach (var other in Linked(root, gone.Id))
        {
            Unlink(root, other, gone.Id);
        }
    }

    /// <summary>
    /// A work is merged into another: the works that linked the source link the target instead, and the target keeps its own
    /// links (F-LNK-4). Links between the two works vanish. Returns the links the merged work has.
    /// </summary>
    internal static IReadOnlyList<WorkLink> CarryOnMerge(string root, WorkSidecar source, WorkSidecar target, LibraryWork targetWork)
    {
        var merged = new List<WorkLink>(target.Links.Where(l => l.Work != source.Id));
        foreach (var link in source.Links.Where(l => l.Work != target.Id && merged.All(m => m.Work != l.Work)))
        {
            merged.Add(link);
        }

        foreach (var other in Linked(root, source.Id).Where(o => o.Id != targetWork.Id))
        {
            var path = LibraryEditor.SidecarPath(root, other);
            var sidecar = SidecarStore.Read(path);
            var back = sidecar.Links.FirstOrDefault(l => l.Work == source.Id);
            var links = sidecar.Links.Where(l => l.Work != source.Id).ToList();
            if (back is not null && links.All(l => l.Work != targetWork.Id))
            {
                links.Add(back with { Work = targetWork.Id });
            }

            SidecarStore.Write(path, sidecar with { Links = links });
            if (merged.All(m => m.Work != other.Id))
            {
                merged.Add(new WorkLink(other.Id));
            }
        }

        return merged;
    }

    private static bool Apply(string root, LibraryWork work, LibraryWork other, string? note)
    {
        var path = LibraryEditor.SidecarPath(root, work);
        var sidecar = SidecarStore.Read(path);
        var current = sidecar.Links.FirstOrDefault(l => l.Work == other.Id);
        var wanted = note is null ? current?.Note : note.Length == 0 ? null : note;
        if (current is not null && current.Note == wanted)
        {
            return false;
        }

        var links = sidecar.Links.Where(l => l.Work != other.Id).Append(new WorkLink(other.Id, wanted));
        SidecarStore.Write(path, sidecar with { Links = [.. links] });
        return true;
    }

    private static void Unlink(string root, LibraryWork work, Ulid gone)
    {
        var path = LibraryEditor.SidecarPath(root, work);
        var sidecar = SidecarStore.Read(path);
        SidecarStore.Write(path, sidecar with { Links = [.. sidecar.Links.Where(l => l.Work != gone)] });
    }

    private static List<LibraryWork> Linked(string root, Ulid id)
    {
        using var connection = IndexDatabase.OpenRead(IndexDatabase.PathFor(root));
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT w.id, w.dir_path, w.title FROM work_links l JOIN works w ON w.id = l.work_id WHERE l.other_id = $id ORDER BY w.id";
        command.Parameters.AddWithValue("$id", id.ToString());
        using var reader = command.ExecuteReader();
        var result = new List<LibraryWork>();
        while (reader.Read())
        {
            result.Add(new LibraryWork(Ulid.Parse(reader.GetString(0)), reader.GetString(1), reader.GetString(2)));
        }

        return result;
    }

    /// <summary>Trims a note; an empty note stays empty (it removes the note), none stays none.</summary>
    private static string? Clean(string? note)
    {
        if (note is null)
        {
            return null;
        }

        var text = note.Trim();
        if (text.Length > WorkLink.MaxNoteLength || text.Contains('\n') || text.Contains('\r'))
        {
            throw new LibraryEditException($"a note must be one line of at most {WorkLink.MaxNoteLength} characters");
        }

        return text;
    }
}
