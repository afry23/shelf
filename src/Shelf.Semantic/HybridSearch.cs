using Microsoft.Data.Sqlite;
using Shelf.Core.Model;
using Shelf.Core.Search;
using Shelf.Index;

namespace Shelf.Semantic;

/// <summary>Reciprocal Rank Fusion (F-SEM-5): a key scores <c>1 / (k + rank)</c> in every list it is in, and the scores add up.</summary>
public static class Rrf
{
    public const int K = 60;

    /// <summary>
    /// Fuses ranked lists (best first, each key at most once). The score is scaled so that a key at the top of every list
    /// that has entries scores 1 and the lowest possible score is near 0; an empty list (a full-text search that found
    /// nothing) does not lower the others.
    /// </summary>
    public static List<(T Key, double Score)> Fuse<T>(IReadOnlyList<IReadOnlyList<T>> rankings)
        where T : notnull
    {
        var scores = new Dictionary<T, double>();
        var order = new Dictionary<T, int>();
        foreach (var ranking in rankings)
        {
            for (var i = 0; i < ranking.Count; i++)
            {
                scores[ranking[i]] = scores.GetValueOrDefault(ranking[i]) + (1.0 / (K + i + 1));
                order.TryAdd(ranking[i], order.Count);
            }
        }

        var best = rankings.Count(r => r.Count > 0) / (double)(K + 1);
        return [.. scores
            .OrderByDescending(s => s.Value)
            .ThenBy(s => order[s.Key])
            .Select(s => (s.Key, best == 0 ? 0 : s.Value / best))];
    }
}

/// <summary>
/// Full-text and semantic search in one ranking (F-SEM-5). Works are fused by their id, passages by their place (file and
/// location), since the two searches cut a file into different pieces. The full-text side takes plain words; the
/// vector side may be missing (an index without embeddings), then the full-text order stands alone.
/// </summary>
public static class HybridSearch
{
    // Candidates taken from each side before they are fused; enough that a hit far down one list can still be lifted by the other.
    private const int MinCandidates = 50;

    public static List<SemanticHit> SearchWorks(SqliteConnection connection, VectorStore? store, float[]? query, SearchQuery words, int limit)
    {
        var candidates = Math.Max(MinCandidates, limit * 3);
        var text = LibrarySearch.Search(connection, words, candidates, hitsPerWork: 0);
        var meaning = store is not null && query is not null ? SemanticSearch.SearchWorks(connection, store, query, candidates) : [];

        var works = new Dictionary<Ulid, SemanticWork>();
        foreach (var hit in text)
        {
            works[hit.WorkId] = new SemanticWork(hit.WorkId, hit.Title, hit.Authors, hit.Year, hit.Language, hit.Formats);
        }

        foreach (var hit in meaning)
        {
            works.TryAdd(hit.Work.Id, hit.Work);
        }

        return [.. Rrf
            .Fuse<Ulid>([[.. text.Select(h => h.WorkId)], [.. meaning.Select(h => h.Work.Id)]])
            .Take(limit)
            .Select(f => new SemanticHit(works[f.Key], f.Score, null))];
    }

    public static List<SemanticHit> SearchPassages(SqliteConnection connection, VectorStore? store, float[]? query, SearchQuery words, int limit)
    {
        var candidates = Math.Max(MinCandidates, limit * 3);
        var text = new List<SemanticHit>();
        foreach (var work in LibrarySearch.Search(connection, words, candidates, LibrarySearch.DefaultHitsPerWork))
        {
            var semanticWork = new SemanticWork(work.WorkId, work.Title, work.Authors, work.Year, work.Language, work.Formats);
            text.AddRange(work.Hits.Select(h => new SemanticHit(semanticWork, 0, new PassagePlace(h.File, h.Format, h.Locator, h.Where, h.Snippet))));
        }

        var meaning = store is not null && query is not null ? SemanticSearch.SearchPassages(connection, store, query, candidates) : [];

        var hits = new Dictionary<(string File, string Locator), SemanticHit>();
        foreach (var hit in text.Concat(meaning))
        {
            hits.TryAdd((hit.Passage!.File, hit.Passage.Locator), hit);
        }

        static List<(string, string)> Keys(IEnumerable<SemanticHit> list) =>
            [.. list.Select(h => (h.Passage!.File, h.Passage.Locator)).Distinct()];

        return [.. Rrf
            .Fuse<(string File, string Locator)>([Keys(text), Keys(meaning)])
            .Take(limit)
            .Select(f => hits[f.Key] with { Score = f.Score })];
    }
}
