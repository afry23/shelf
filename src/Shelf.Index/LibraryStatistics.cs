using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace Shelf.Index;

public sealed record FormatStatistic(string Format, int Files, long Bytes);

public sealed record LanguageStatistic(string Language, int Works);

/// <summary>What the index holds, counted (<c>shelf stats</c>).</summary>
public sealed record LibraryStatistics(
    int Works,
    int Files,
    long Bytes,
    int Authors,
    int Tags,
    int Unread,
    int Reading,
    int Read,
    IReadOnlyList<FormatStatistic> Formats,
    IReadOnlyList<LanguageStatistic> Languages)
{
    public static LibraryStatistics Load(SqliteConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);

        var authors = new HashSet<string>(StringComparer.Ordinal);
        foreach (var json in Strings(connection, "SELECT authors_json FROM works"))
        {
            using var document = JsonDocument.Parse(json);
            foreach (var author in document.RootElement.EnumerateArray())
            {
                authors.Add(author.GetString()!);
            }
        }

        var formats = new List<FormatStatistic>();
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT format, COUNT(*), SUM(size) FROM files GROUP BY format ORDER BY COUNT(*) DESC, format";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                formats.Add(new FormatStatistic(reader.GetString(0), reader.GetInt32(1), reader.GetInt64(2)));
            }
        }

        var languages = new List<LanguageStatistic>();
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT language, COUNT(*) FROM works WHERE language IS NOT NULL GROUP BY language ORDER BY COUNT(*) DESC, language";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                languages.Add(new LanguageStatistic(reader.GetString(0), reader.GetInt32(1)));
            }
        }

        return new LibraryStatistics(
            Count(connection, "SELECT COUNT(*) FROM works"),
            Count(connection, "SELECT COUNT(*) FROM files"),
            Convert.ToInt64(IndexDatabase.Scalar(connection, "SELECT COALESCE(SUM(size), 0) FROM files"), System.Globalization.CultureInfo.InvariantCulture),
            authors.Count,
            Count(connection, "SELECT COUNT(*) FROM tags"),
            Count(connection, "SELECT COUNT(*) FROM works WHERE read_status = 'unread'"),
            Count(connection, "SELECT COUNT(*) FROM works WHERE read_status = 'reading'"),
            Count(connection, "SELECT COUNT(*) FROM works WHERE read_status = 'read'"),
            formats,
            languages);
    }

    private static int Count(SqliteConnection connection, string sql) => (int)(long)IndexDatabase.Scalar(connection, sql)!;

    private static List<string> Strings(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        var values = new List<string>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            values.Add(reader.GetString(0));
        }

        return values;
    }
}
