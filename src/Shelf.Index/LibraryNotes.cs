using Shelf.Core.Model;
using Shelf.Core.Search;

namespace Shelf.Index;

/// <summary>A work with its sidecar and its highlights, notes and bookmarks.</summary>
public sealed record WorkNotes(LibraryWork Work, WorkSidecar Sidecar, IReadOnlyList<Annotation> Annotations);

/// <summary>The annotations of many works at once: bulk export and resurface (F-ANN-9, F-ANN-10).</summary>
public static class LibraryNotes
{
    private const int NoLimit = 1_000_000;

    /// <summary>
    /// The works that match <paramref name="query"/> (all works if it is null or empty) that have at least one annotation,
    /// in the order of the library (query: best first).
    /// </summary>
    /// <exception cref="SidecarException">A sidecar or an annotations file is invalid.</exception>
    /// <exception cref="IndexException">There is no usable index.</exception>
    public static IReadOnlyList<WorkNotes> Collect(string root, SearchQuery? query = null)
    {
        var works = new List<LibraryWork>();
        using (var connection = IndexDatabase.OpenRead(IndexDatabase.PathFor(root)))
        {
            if (query is { IsEmpty: false })
            {
                var hits = LibrarySearch.Search(connection, query, NoLimit);
                var order = hits.Select((h, i) => (h.WorkId, i)).GroupBy(x => x.WorkId).ToDictionary(g => g.Key, g => g.Min(x => x.i));
                works.AddRange(LibraryCitations.AllWorks(connection).Where(w => order.ContainsKey(w.Id)));
                works.Sort((a, b) => order[a.Id].CompareTo(order[b.Id]));
            }
            else
            {
                works.AddRange(LibraryCitations.AllWorks(connection));
            }
        }

        var result = new List<WorkNotes>();
        foreach (var work in works)
        {
            var annotations = LibraryAnnotations.List(root, work);
            if (annotations.Count > 0)
            {
                result.Add(new WorkNotes(work, LibraryEditor.Read(root, work), annotations));
            }
        }

        return result;
    }

    /// <summary>
    /// The highlights that come back on <paramref name="date"/> (F-ANN-10): at most <paramref name="count"/>, only highlights with a
    /// quote, the same ones for the same date.
    /// </summary>
    /// <exception cref="SidecarException">A sidecar or an annotations file is invalid.</exception>
    /// <exception cref="IndexException">There is no usable index.</exception>
    public static IReadOnlyList<(WorkNotes Work, Annotation Annotation)> Resurface(string root, DateOnly date, int count)
    {
        var candidates = Collect(root)
            .SelectMany(w => w.Annotations
                .Where(a => a.Kind == AnnotationKind.Highlight && !string.IsNullOrWhiteSpace(a.Quote?.Exact))
                .Select(a => (Work: w, Annotation: a)));
        return Core.Model.Resurface.Pick(candidates, c => c.Annotation.Id.ToString(), date, count);
    }
}
