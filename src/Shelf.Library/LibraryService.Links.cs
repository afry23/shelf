using Shelf.Index;
using Shelf.Core.Model;

namespace Shelf.Library;

public sealed partial class LibraryService
{
    /// <summary>The works a person linked with this one by hand, with the note of this side (F-LNK-5).</summary>
    /// <exception cref="LibraryException">NotFound: no such work.</exception>
    public LinksResult GetLinks(string id)
    {
        var work = Find(id);
        try
        {
            var links = LibraryLinks.List(root, work.Id);
            return new LinksResult(
                work.Id.ToString(),
                work.Title,
                links.Count,
                [.. links.Select(l => new LinkItem(l.Id.ToString(), l.Title, [.. l.Authors], l.Year, l.Kind, l.Note))]);
        }
        catch (IndexException e)
        {
            throw new LibraryException(e.Message);
        }
    }

    /// <summary>
    /// Links two works (F-LNK-1, F-LNK-2). A note that is null leaves the note as it is, an empty one removes it.
    /// </summary>
    /// <exception cref="LibraryException">Invalid: the works are the same or a note is not one line of 200 characters. NotFound: no such work.</exception>
    public LinkOutcome LinkWorks(string id, string other, string? note, string? otherNote)
    {
        var first = Find(id);
        var second = Find(other);
        lock (_writeGate)
        {
            try
            {
                return new LinkOutcome(first.Id.ToString(), second.Id.ToString(), LibraryLinks.Add(root, first, second, note, otherNote).Changed);
            }
            catch (Exception e) when (e is LibraryEditException or SidecarException or IndexException)
            {
                throw new LibraryException(e.Message);
            }
        }
    }

    /// <summary>Removes the link between two works from both (F-LNK-2).</summary>
    /// <exception cref="LibraryException">NotFound: no such work, or the works are not linked.</exception>
    public LinkOutcome UnlinkWorks(string id, string other)
    {
        var first = Find(id);
        var second = Find(other);
        lock (_writeGate)
        {
            try
            {
                return new LinkOutcome(first.Id.ToString(), second.Id.ToString(), LibraryLinks.Remove(root, first, second).Changed);
            }
            catch (Exception e) when (e is LibraryEditException or SidecarException or IndexException)
            {
                throw new LibraryException(e.Message, e is LibraryEditException ? LibraryErrorKind.NotFound : LibraryErrorKind.Invalid);
            }
        }
    }
}
