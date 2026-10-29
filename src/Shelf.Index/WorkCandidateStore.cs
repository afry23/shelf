using System.Text.Json;
using Microsoft.Data.Sqlite;
using Shelf.Core.Grouping;
using Shelf.Core.Model;

namespace Shelf.Index;

/// <summary>Loads the works of the index as candidates for the grouping cascade (F-GRP-1).</summary>
public static class WorkCandidateStore
{
    /// <summary>All works with title, author surnames, year, ISBN and the text fingerprints of their files.</summary>
    public static List<WorkCandidate> Load(SqliteConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);

        var fingerprints = new Dictionary<string, List<ulong>>(StringComparer.Ordinal);
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT work_id, simhash FROM files WHERE simhash IS NOT NULL";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var work = reader.GetString(0);
                if (!fingerprints.TryGetValue(work, out var list))
                {
                    fingerprints[work] = list = [];
                }

                // Stored as a signed 64 bit integer; the bits are the same.
                list.Add(unchecked((ulong)reader.GetInt64(1)));
            }
        }

        var formats = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT DISTINCT work_id, format FROM files";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var work = reader.GetString(0);
                if (!formats.TryGetValue(work, out var list))
                {
                    formats[work] = list = [];
                }

                list.Add(reader.GetString(1));
            }
        }

        // "Not a duplicate" markings of files: the work holding one file must not take a new file named like the other.
        var notDuplicates = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                SELECT d.a_id, d.b_id, fa.work_id, fb.work_id
                FROM dupe_ignores d
                LEFT JOIN files fa ON fa.rel_path = d.a_id
                LEFT JOIN files fb ON fb.rel_path = d.b_id
                WHERE d.kind = 'file'
                """;
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                void Add(object? work, string otherPath)
                {
                    if (work is string id)
                    {
                        if (!notDuplicates.TryGetValue(id, out var list))
                        {
                            notDuplicates[id] = list = [];
                        }

                        list.Add(Path.GetFileName(otherPath));
                    }
                }

                Add(reader.IsDBNull(2) ? null : reader.GetString(2), reader.GetString(1));
                Add(reader.IsDBNull(3) ? null : reader.GetString(3), reader.GetString(0));
            }
        }

        var candidates = new List<WorkCandidate>();
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT id, title_norm, authors_json, year, isbn13, doi, arxiv_id FROM works ORDER BY seq";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var id = reader.GetString(0);
                var authors = JsonSerializerAuthors(reader.GetString(2));
                var signature = new WorkSignature(
                    reader.GetString(1),
                    AuthorNames.SurnameKeys(authors),
                    reader.IsDBNull(3) ? null : reader.GetInt32(3),
                    reader.IsDBNull(4) ? null : reader.GetString(4),
                    reader.IsDBNull(5) ? null : reader.GetString(5),
                    reader.IsDBNull(6) ? null : reader.GetString(6));
                candidates.Add(new WorkCandidate(Ulid.Parse(id), signature, fingerprints.GetValueOrDefault(id), notDuplicates.GetValueOrDefault(id), formats.GetValueOrDefault(id)));
            }
        }

        return candidates;
    }

    private static List<string> JsonSerializerAuthors(string json)
    {
        using var document = JsonDocument.Parse(json);
        return [.. document.RootElement.EnumerateArray().Select(e => e.GetString()!)];
    }
}
