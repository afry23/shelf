using Shelf.Core.Configuration;
using Shelf.Index;

namespace Shelf.Semantic;

/// <param name="Note">What to tell about the vectors (none yet, or not complete), or null.</param>
public sealed record ProbableDuplicatesResult(IReadOnlyList<ProbableDuplicate> Pairs, string? Note);

/// <summary>
/// Probable duplicates (F-DUP-3): the text fingerprints of the index and, if the library has embeddings, the vectors of the works.
/// Without embeddings only the fingerprints decide, and the note says so.
/// </summary>
public static class ProbableDuplicates
{
    /// <exception cref="IndexException">There is no usable index.</exception>
    /// <exception cref="VectorStoreException">The vectors of the index cannot be used.</exception>
    public static ProbableDuplicatesResult Find(string root, GroupingSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        using var connection = IndexDatabase.OpenRead(IndexDatabase.PathFor(root));
        var store = VectorStore.OpenExisting(connection);
        var coverage = SemanticSearch.Coverage(connection, store);
        var empty = store is null || store.Count(VectorKind.Work) == 0;
        var similar = empty ? [] : SemanticSearch.SimilarPairs(connection, store!, settings.ProbableCosine);
        return new ProbableDuplicatesResult(LibraryDuplicates.FindProbable(root, settings, similar), coverage.Note(empty));
    }
}
