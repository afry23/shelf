using Shelf.Core.Model;
using Shelf.Core.Review;
using Shelf.Index;

namespace Shelf.Library;

public sealed partial class LibraryService
{
    /// <summary>The uncertain assignments of the import (F-IMP-7), oldest first, with the titles of the works they name.</summary>
    public ReviewList ListReview()
    {
        try
        {
            var queue = ReviewQueue.Load(ReviewQueue.PathFor(root));
            var ids = queue.Entries.SelectMany(e => (e.Work is { } w ? [w] : Array.Empty<Ulid>()).Concat(e.Candidates?.Select(c => c.Work) ?? []));
            var labels = WorkLookup.Labels(root, ids);
            string? Label(Ulid? id) => id is { } known && labels.TryGetValue(known, out var label) ? label : null;
            return new ReviewList(
                queue.Entries.Count,
                [.. queue.Entries.Select(e => new ReviewItem(
                    e.Id.ToString(),
                    ReviewQueue.KindName(e.Kind),
                    ReviewQueue.FormatTime(e.CreatedAt),
                    e.Message,
                    e.File,
                    e.Work?.ToString(),
                    Label(e.Work),
                    [.. (e.Candidates ?? []).Select(c => new ReviewCandidateItem(c.Work.ToString(), Label(c.Work), c.Confidence, [.. c.Signals]))]))]);
        }
        catch (ReviewQueueException e)
        {
            throw new LibraryException(e.Message);
        }
    }

    /// <summary>
    /// Merges the work of a grouping entry into a candidate (<paramref name="into"/>, else the best one) and settles the entry. A merge is
    /// not undoable.
    /// </summary>
    /// <exception cref="LibraryException">NotFound: no such entry or work. Invalid: the entry is not a grouping entry, or names no target.</exception>
    public ReviewOutcome AcceptReview(string entryId, string? into)
    {
        lock (_writeGate)
        {
            try
            {
                var entry = FindEntry(entryId);
                if (entry.Kind != ReviewKind.Grouping)
                {
                    throw new LibraryException($"only a grouping entry can be accepted (this one is {ReviewQueue.KindName(entry.Kind)})");
                }

                var source = entry.Work ?? throw new LibraryException("the entry names no work to merge");
                var target = into
                    ?? entry.Candidates?.OrderByDescending(c => c.Confidence).FirstOrDefault()?.Work.ToString()
                    ?? throw new LibraryException("the entry has no candidate; name the work with into");
                var result = LibraryEditor.Merge(root, Find(target), Find(source.ToString()));
                LibraryEditor.RemoveReviewEntry(root, entry.Id);
                return new ReviewOutcome(entry.Id.ToString(), result.Target.ToString(), result.MovedFiles);
            }
            catch (Exception e) when (e is LibraryEditException or SidecarException or IndexException or ReviewQueueException)
            {
                throw new LibraryException(e.Message);
            }
        }
    }

    /// <summary>Settles an entry without changing the works. A grouping entry is remembered as "not duplicates" for each candidate.</summary>
    /// <exception cref="LibraryException">NotFound: no such entry.</exception>
    public ReviewOutcome DismissReview(string entryId)
    {
        lock (_writeGate)
        {
            try
            {
                var entry = FindEntry(entryId);
                if (entry.Kind == ReviewKind.Grouping && entry.Work is { } work && entry.Candidates is { Count: > 0 } candidates)
                {
                    var self = Find(work.ToString());
                    foreach (var candidate in candidates)
                    {
                        LibraryEditor.MarkNotDuplicates(root, self, Find(candidate.Work.ToString()));
                    }
                }

                LibraryEditor.RemoveReviewEntry(root, entry.Id);
                return new ReviewOutcome(entry.Id.ToString(), null, 0);
            }
            catch (Exception e) when (e is LibraryEditException or SidecarException or IndexException or ReviewQueueException)
            {
                throw new LibraryException(e.Message);
            }
        }
    }

    private ReviewEntry FindEntry(string entryId)
    {
        var queue = ReviewQueue.Load(ReviewQueue.PathFor(root));
        return Ulid.TryParse(entryId, out var id) && queue.Entries.FirstOrDefault(e => e.Id == id) is { } entry
            ? entry
            : throw new LibraryException($"no review entry '{entryId}'", LibraryErrorKind.NotFound);
    }
}
