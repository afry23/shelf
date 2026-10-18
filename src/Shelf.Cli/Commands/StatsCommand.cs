using System.CommandLine;
using System.Text.Json;
using Shelf.Core.Review;
using Shelf.Index;

namespace Shelf.Cli.Commands;

/// <summary><c>shelf stats</c>: what the library holds, counted.</summary>
internal static class StatsCommand
{
    private const int LabelWidth = 16;

    public static Command Create(Option<bool> json, TextWriter stdout, TextWriter stderr, CliEnvironment environment)
    {
        var library = LibraryContext.CreateOption();
        var command = new Command("stats", "Show statistics of the library");
        command.Options.Add(library);
        command.SetAction(parseResult =>
        {
            var context = LibraryContext.Open(parseResult.GetValue(library), environment, stderr);
            LibraryStatistics stats;
            using (var connection = IndexDatabase.OpenRead(IndexDatabase.PathFor(context.Root)))
            {
                stats = LibraryStatistics.Load(connection);
            }

            var queue = ReviewQueue.Load(ReviewQueue.PathFor(context.Root));
            var kinds = Enum.GetValues<ReviewKind>().ToDictionary(k => k, k => queue.Entries.Count(e => e.Kind == k));
            if (parseResult.GetValue(json))
            {
                stdout.WriteLine(JsonOutput.Write(writer => WriteJson(writer, stats, queue.Entries.Count, kinds)));
            }
            else
            {
                WriteText(stdout, Palette.For(environment, json: false), stats, queue.Entries.Count, kinds);
            }

            return ExitCodes.Success;
        });
        return command;
    }

    private static void WriteText(TextWriter stdout, Palette palette, LibraryStatistics stats, int review, Dictionary<ReviewKind, int> kinds)
    {
        void Line(string label, string value) => stdout.WriteLine($"{palette.Bold(label.PadRight(LabelWidth))}{value}");

        void Section(string heading, IEnumerable<(string Name, string Value, string? Extra)> rows)
        {
            var list = rows.ToList();
            stdout.WriteLine();
            stdout.WriteLine(palette.Bold(heading));
            var width = list.Count == 0 ? 0 : list.Max(r => r.Name.Length);
            var valueWidth = list.Count == 0 ? 0 : list.Max(r => r.Value.Length);
            foreach (var (name, value, extra) in list)
            {
                var line = $"  {name.PadRight(width)}  {value.PadLeft(valueWidth)}";
                stdout.WriteLine(extra is null ? line : $"{line}  {extra}");
            }
        }

        Line("Works", TextFormat.Number(stats.Works));
        Line("Files", TextFormat.Number(stats.Files));
        Line("Size", TextFormat.Size(stats.Bytes));
        Line("Authors", TextFormat.Number(stats.Authors));
        Line("Tags", TextFormat.Number(stats.Tags));
        Section("Formats", stats.Formats.Select(f => (f.Format, TextFormat.Number(f.Files), (string?)TextFormat.Size(f.Bytes))));
        Section("Read status", [("unread", TextFormat.Number(stats.Unread), null), ("reading", TextFormat.Number(stats.Reading), null), ("read", TextFormat.Number(stats.Read), null)]);
        if (stats.Languages.Count > 0)
        {
            Section("Languages", stats.Languages.Select(l => (l.Language, TextFormat.Number(l.Works), (string?)null)));
        }

        stdout.WriteLine();
        Line("Review queue", TextFormat.Number(review));
        foreach (var (kind, count) in kinds.Where(k => k.Value > 0))
        {
            stdout.WriteLine($"  {ReviewQueue.KindName(kind).PadRight(18)}{TextFormat.Number(count).PadLeft(3)}");
        }
    }

    private static void WriteJson(Utf8JsonWriter writer, LibraryStatistics stats, int review, Dictionary<ReviewKind, int> kinds)
    {
        writer.WriteStartObject();
        JsonOutput.Property(writer, "works", stats.Works);
        JsonOutput.Property(writer, "files", stats.Files);
        writer.WriteNumber("bytes", stats.Bytes);
        JsonOutput.Property(writer, "authors", stats.Authors);
        JsonOutput.Property(writer, "tags", stats.Tags);
        writer.WriteStartArray("formats");
        foreach (var format in stats.Formats)
        {
            writer.WriteStartObject();
            JsonOutput.Property(writer, "format", format.Format);
            JsonOutput.Property(writer, "files", format.Files);
            writer.WriteNumber("bytes", format.Bytes);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteStartObject("readStatus");
        JsonOutput.Property(writer, "unread", stats.Unread);
        JsonOutput.Property(writer, "reading", stats.Reading);
        JsonOutput.Property(writer, "read", stats.Read);
        writer.WriteEndObject();
        writer.WriteStartArray("languages");
        foreach (var language in stats.Languages)
        {
            writer.WriteStartObject();
            JsonOutput.Property(writer, "language", language.Language);
            JsonOutput.Property(writer, "works", language.Works);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteStartObject("reviewQueue");
        JsonOutput.Property(writer, "total", review);
        JsonOutput.Property(writer, "grouping", kinds[ReviewKind.Grouping]);
        JsonOutput.Property(writer, "extractionError", kinds[ReviewKind.ExtractionError]);
        JsonOutput.Property(writer, "weakMetadata", kinds[ReviewKind.WeakMetadata]);
        writer.WriteEndObject();
        writer.WriteEndObject();
    }
}
