using Shelf.Index;

namespace Shelf.Library;

public sealed partial class LibraryService
{
    /// <summary>The library in numbers and what was read (F-STA-2). Nothing is asked and nothing is changed.</summary>
    /// <exception cref="LibraryException">The index is missing or has another version.</exception>
    public StatsResult Stats()
    {
        LibraryStatistics stats;
        try
        {
            using var connection = IndexDatabase.OpenRead(IndexDatabase.PathFor(root));
            stats = LibraryStatistics.Load(connection);
        }
        catch (IndexException e)
        {
            throw new LibraryException(e.Message);
        }

        static ReadingItem Item(ReadingActivity a) => new(
            a.Id.ToString(),
            a.Title,
            a.Status,
            a.LastReadAt is { } time ? Time(time) : null);

        return new StatsResult(
            stats.Works,
            stats.Files,
            stats.Bytes,
            stats.Authors,
            stats.Tags,
            new StatusCounts(stats.Unread, stats.Reading, stats.Read),
            [.. stats.Formats.Select(f => new FormatCount(f.Format, f.Files, f.Bytes))],
            [.. stats.Languages.Select(l => new LanguageCount(l.Language, l.Works))],
            stats.ReadWithoutDate,
            [.. stats.FinishedByMonth.Select(m => new MonthCount(m.Month, m.Works))],
            [.. stats.CurrentlyReading.Select(Item)],
            [.. stats.Recent.Select(Item)]);
    }
}
