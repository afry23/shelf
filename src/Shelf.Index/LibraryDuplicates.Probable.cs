using System.Text.Json;
using Microsoft.Data.Sqlite;
using Shelf.Core.Configuration;
using Shelf.Core.Grouping;
using Shelf.Core.Model;

namespace Shelf.Index;

/// <summary>A work as it is shown in a probable duplicate: what is needed to tell the two apart.</summary>
/// <param name="Folder">The folder of the work, library-relative.</param>
/// <param name="Files">Library-relative paths with '/'.</param>
public sealed record ProbableWork(Ulid Id, string Title, IReadOnlyList<string> Authors, string Folder, IReadOnlyList<string> Files);

/// <param name="A">The work whose folder sorts first.</param>
/// <param name="Hamming">Smallest SimHash Hamming distance between a file of each work; null if one of them has no fingerprint.</param>
/// <param name="Cosine">Cosine similarity of the work vectors; null if they were not compared or are not close.</param>
public sealed record ProbableDuplicate(ProbableWork A, ProbableWork B, int? Hamming, double? Cosine);

/// <summary>Two works whose vectors are close (<see cref="Cosine"/>).</summary>
public readonly record struct WorkSimilarity(Ulid A, Ulid B, double Cosine);

public static partial class LibraryDuplicates
{
    /// <summary>
    /// Pairs of different works that probably are one book (F-DUP-3): the texts of two files have a SimHash Hamming distance of at
    /// most <see cref="GroupingSettings.ProbableHamming"/>, or the vectors of the works (<paramref name="similar"/>) are close.
    /// Works that share a file with the same content are the business of <see cref="Find"/>; pairs marked as not duplicates are left
    /// out (F-DUP-6). The pairs are for manual review: nothing here suggests to delete anything.
    /// </summary>
    /// <param name="similar">Pairs of works with a high cosine similarity; the caller applies <see cref="GroupingSettings.ProbableCosine"/>.</param>
    /// <exception cref="IndexException">There is no usable index.</exception>
    public static IReadOnlyList<ProbableDuplicate> FindProbable(string root, GroupingSettings settings, IEnumerable<WorkSimilarity>? similar = null)
    {
        ArgumentNullException.ThrowIfNull(settings);
        root = Normalize(root);
        using var connection = IndexDatabase.OpenRead(IndexDatabase.PathFor(root));
        var works = LoadWorks(connection);
        var files = LoadFingerprints(connection);
        var ignores = LoadIgnores(connection);

        var pairs = new Dictionary<(Ulid, Ulid), (int? Hamming, double? Cosine)>();
        foreach (var s in similar ?? [])
        {
            pairs[Ordered(s.A, s.B)] = (null, s.Cosine);
        }

        var withHash = files.Where(f => f.Hash is not null).ToArray();
        var distances = new Dictionary<(Ulid, Ulid), int>();
        foreach (var (i, j, distance) in NearFingerprints([.. withHash.Select(f => f.Hash!.Value)], settings.ProbableHamming))
        {
            if (withHash[i].Work == withHash[j].Work)
            {
                continue;
            }

            var key = Ordered(withHash[i].Work, withHash[j].Work);
            if (!distances.TryGetValue(key, out var best) || distance < best)
            {
                distances[key] = distance;
            }
        }

        foreach (var (key, distance) in distances)
        {
            // A distance above the setting says nothing in favour of a pair that was found by its vectors.
            if (distance <= settings.ProbableHamming)
            {
                pairs[key] = (distance, pairs.TryGetValue(key, out var known) ? known.Cosine : null);
            }
        }

        var sameContent = SharedContent(files);
        var result = new List<ProbableDuplicate>();
        foreach (var ((a, b), (hamming, cosine)) in pairs)
        {
            if (sameContent.Contains((a, b)) || ignores.ContainsWorks(a, b) || !works.TryGetValue(a, out var first) || !works.TryGetValue(b, out var second))
            {
                continue;
            }

            result.Add(string.CompareOrdinal(first.Folder, second.Folder) <= 0
                ? new ProbableDuplicate(first, second, hamming, cosine)
                : new ProbableDuplicate(second, first, hamming, cosine));
        }

        return [.. result
            .OrderBy(p => p.Hamming ?? int.MaxValue)
            .ThenByDescending(p => p.Cosine ?? 0)
            .ThenBy(p => p.A.Folder, StringComparer.Ordinal)
            .ThenBy(p => p.B.Folder, StringComparer.Ordinal)];
    }

    /// <summary>Largest distance for which the fingerprints are cut into blocks: below 8 bits a block says too little.</summary>
    private const int LargestBlockedDistance = 7;

    /// <summary>
    /// All pairs of fingerprints with a Hamming distance of at most <paramref name="maximum"/>. Two fingerprints that differ in at
    /// most k bits agree on at least one of k + 1 blocks of bits, so only the fingerprints that share a block are compared; for a
    /// large distance every pair is compared.
    /// </summary>
    internal static IEnumerable<(int First, int Second, int Distance)> NearFingerprints(ulong[] hashes, int maximum)
    {
        if (maximum > LargestBlockedDistance)
        {
            for (var i = 0; i < hashes.Length; i++)
            {
                for (var j = i + 1; j < hashes.Length; j++)
                {
                    var distance = SimHash.Distance(hashes[i], hashes[j]);
                    if (distance <= maximum)
                    {
                        yield return (i, j, distance);
                    }
                }
            }

            yield break;
        }

        var blocks = maximum + 1;
        var found = new HashSet<(int, int)>();
        for (var block = 0; block < blocks; block++)
        {
            var from = block * 64 / blocks;
            var width = ((block + 1) * 64 / blocks) - from;
            var mask = width == 64 ? ulong.MaxValue : (1UL << width) - 1;
            var groups = new Dictionary<ulong, List<int>>();
            for (var i = 0; i < hashes.Length; i++)
            {
                var key = (hashes[i] >> from) & mask;
                if (!groups.TryGetValue(key, out var list))
                {
                    groups[key] = list = [];
                }

                list.Add(i);
            }

            foreach (var group in groups.Values.Where(g => g.Count > 1))
            {
                for (var x = 0; x < group.Count; x++)
                {
                    for (var y = x + 1; y < group.Count; y++)
                    {
                        var distance = SimHash.Distance(hashes[group[x]], hashes[group[y]]);
                        if (distance <= maximum && found.Add((group[x], group[y])))
                        {
                            yield return (group[x], group[y], distance);
                        }
                    }
                }
            }
        }
    }

    private static (Ulid, Ulid) Ordered(Ulid a, Ulid b) => a.CompareTo(b) <= 0 ? (a, b) : (b, a);

    /// <summary>The pairs of works that have a file with the same content: <see cref="Find"/> lists those.</summary>
    private static HashSet<(Ulid, Ulid)> SharedContent(List<FingerprintRow> files)
    {
        var shared = new HashSet<(Ulid, Ulid)>();
        foreach (var same in files.Where(f => f.Sha256 is not null).GroupBy(f => f.Sha256!))
        {
            var owners = same.Select(f => f.Work).Distinct().ToList();
            for (var i = 0; i < owners.Count; i++)
            {
                for (var j = i + 1; j < owners.Count; j++)
                {
                    shared.Add(Ordered(owners[i], owners[j]));
                }
            }
        }

        return shared;
    }

    private static Dictionary<Ulid, ProbableWork> LoadWorks(SqliteConnection connection)
    {
        var paths = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT work_id, rel_path FROM files WHERE role = 'book' ORDER BY rel_path";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                if (!paths.TryGetValue(reader.GetString(0), out var list))
                {
                    paths[reader.GetString(0)] = list = [];
                }

                list.Add(reader.GetString(1));
            }
        }

        var works = new Dictionary<Ulid, ProbableWork>();
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT id, title, authors_json, dir_path FROM works";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var id = reader.GetString(0);
                var authors = Authors(reader.GetString(2));
                works[Ulid.Parse(id)] = new ProbableWork(Ulid.Parse(id), reader.GetString(1), authors, reader.GetString(3), paths.GetValueOrDefault(id) ?? []);
            }
        }

        return works;
    }

    private static string[] Authors(string json)
    {
        using var document = JsonDocument.Parse(json);
        return [.. document.RootElement.EnumerateArray().Select(e => e.GetString()!)];
    }

    private static List<FingerprintRow> LoadFingerprints(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT work_id, sha256, simhash FROM files WHERE role = 'book'";
        using var reader = command.ExecuteReader();
        var rows = new List<FingerprintRow>();
        while (reader.Read())
        {
            rows.Add(new FingerprintRow(
                Ulid.Parse(reader.GetString(0)),
                reader.IsDBNull(1) ? null : reader.GetString(1),
                reader.IsDBNull(2) ? null : unchecked((ulong)reader.GetInt64(2))));
        }

        return rows;
    }

    private sealed record FingerprintRow(Ulid Work, string? Sha256, ulong? Hash);
}
