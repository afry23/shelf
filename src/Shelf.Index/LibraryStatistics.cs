using System.Text.Json;
using Microsoft.Data.Sqlite;
using Shelf.Core.Model;

namespace Shelf.Index;

public sealed record FormatStatistic(string Format, int Files, long Bytes);

public sealed record LanguageStatistic(string Language, int Works);

/// <summary>The works set to read in a month (<c>2026-10</c>), by the date of finishing (F-STA-1).</summary>
public sealed record MonthStatistic(string Month, int Works);

/// <summary>A work with the last time a file of it was moved in the reader (F-RDR-4), or null if none was.</summary>
public sealed record ReadingActivity(Ulid Id, string Title, string Status, DateTimeOffset? LastReadAt);

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
    IReadOnlyList<LanguageStatistic> Languages,
    int ReadWithoutDate,
    IReadOnlyList<MonthStatistic> FinishedByMonth,
    IReadOnlyList<ReadingActivity> CurrentlyReading,
    IReadOnlyList<ReadingActivity> Recent)
{
    /// <summary>How many works the lists "reading" and "recent" hold.</summary>
    public const int ActivityLimit = 10;

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

        var months = new List<MonthStatistic>();
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT substr(finished_at, 1, 7), COUNT(*) FROM works WHERE read_status = 'read' AND finished_at IS NOT NULL GROUP BY 1 ORDER BY 1";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                months.Add(new MonthStatistic(reader.GetString(0), reader.GetInt32(1)));
            }
        }

        // The last move of a reader in any file of the work; a work nobody opened has none.
        var activity = new List<ReadingActivity>();
        using (var command = connection.CreateCommand())
        {
            command.CommandText =
                "SELECT w.id, w.title, w.read_status, MAX(rp.updated_at) FROM works w "
                + "LEFT JOIN files f ON f.work_id = w.id LEFT JOIN reading_progress rp ON rp.file_id = f.id GROUP BY w.id";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                activity.Add(new ReadingActivity(
                    Ulid.Parse(reader.GetString(0)),
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.IsDBNull(3) ? null : DateTimeOffset.Parse(reader.GetString(3), System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal)));
            }
        }

        var newestFirst = activity
            .OrderByDescending(a => a.LastReadAt ?? DateTimeOffset.MinValue)
            .ThenBy(a => a.Title, StringComparer.Ordinal)
            .ThenBy(a => a.Id);

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
            languages,
            Count(connection, "SELECT COUNT(*) FROM works WHERE read_status = 'read' AND finished_at IS NULL"),
            months,
            [.. newestFirst.Where(a => a.Status == "reading").Take(ActivityLimit)],
            [.. newestFirst.Where(a => a.LastReadAt is not null).Take(ActivityLimit)]);
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
