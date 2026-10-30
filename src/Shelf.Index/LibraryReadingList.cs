using Shelf.Core.Model;

namespace Shelf.Index;

/// <summary>
/// Puts works on the reading list and takes them off (F-RLS-1). The time is in <c>book.json</c> (<c>readingList</c>), so the list
/// goes with the work; the index mirrors it. A work that becomes read leaves the list in <see cref="WorkSidecar.WithReadStatus"/>.
/// </summary>
public static class LibraryReadingList
{
    /// <summary>
    /// Puts the work on the list or takes it off. A work on the list keeps its time when it is put on again; a work that is
    /// not on it is not changed by taking it off.
    /// </summary>
    /// <returns>Whether the sidecar changed, and the sidecar as it is now.</returns>
    /// <exception cref="LibraryEditException">The work is read already (only for putting it on the list).</exception>
    public static (bool Changed, WorkSidecar Sidecar) Set(string root, LibraryWork work, bool add)
    {
        var sidecar = LibraryEditor.Read(root, work);
        if (add && sidecar.ReadStatus == ReadStatus.Read)
        {
            throw new LibraryEditException($"'{sidecar.Title}' is already read; mark it unread first.");
        }

        if (add == (sidecar.ReadingListAt is not null))
        {
            return (false, sidecar);
        }

        var at = add ? DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds()) : (DateTimeOffset?)null;
        LibraryEditor.Update(root, work, s => s with { ReadingListAt = at });
        return (true, sidecar with { ReadingListAt = at });
    }
}
