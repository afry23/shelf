using System.CommandLine;
using System.Globalization;
using System.Text.Json;
using Shelf.Core.Model;
using Shelf.Core.Review;
using Shelf.Index;

namespace Shelf.Cli.Commands;

/// <summary>
/// <c>shelf review</c> (F-IMP-7): lists what import was unsure about; <c>review accept</c> merges a work into a
/// candidate, <c>review dismiss</c> keeps the works apart (or just clears an entry of another kind).
/// </summary>
internal static class ReviewCommand
{
    public static Command Create(Option<bool> json, TextWriter stdout, TextWriter stderr, CliEnvironment environment)
    {
        var library = LibraryContext.CreateOption();
        var command = new Command("review", "List the uncertain assignments of the import and decide them");
        command.Options.Add(library);
        command.Subcommands.Add(CreateAccept(json, stdout, stderr, environment));
        command.Subcommands.Add(CreateDismiss(json, stdout, stderr, environment));
        command.SetAction(parseResult =>
        {
            var context = LibraryContext.Open(parseResult.GetValue(library), environment, stderr);
            var queue = ReviewQueue.Load(ReviewQueue.PathFor(context.Root));
            var useJson = parseResult.GetValue(json);
            var ids = queue.Entries.SelectMany(e => (e.Work is { } w ? [w] : Array.Empty<Ulid>()).Concat(e.Candidates?.Select(c => c.Work) ?? [])).ToList();
            var labels = WorkLookup.Labels(context.Root, ids);
            if (useJson)
            {
                stdout.WriteLine(JsonOutput.Write(writer => WriteJson(writer, queue)));
            }
            else
            {
                WriteText(stdout, Palette.For(environment, useJson), queue, labels);
            }

            return ExitCodes.Success;
        });
        return command;
    }

    private static Command CreateAccept(Option<bool> json, TextWriter stdout, TextWriter stderr, CliEnvironment environment)
    {
        var entryArgument = EntryArgument();
        var into = new Option<string?>("--into") { Description = "Work to merge into (default: the best candidate of the entry)" };
        var library = LibraryContext.CreateOption();
        var command = new Command("accept", "Merge the work of a grouping entry into a candidate");
        command.Arguments.Add(entryArgument);
        command.Options.Add(into);
        command.Options.Add(library);
        command.SetAction(parseResult =>
        {
            var context = LibraryContext.Open(parseResult.GetValue(library), environment, stderr);
            var entry = Find(context, parseResult.GetRequiredValue(entryArgument));
            if (entry.Kind != ReviewKind.Grouping)
            {
                throw new LibraryEditException($"only a grouping entry can be accepted (this one is {Article(ReviewQueue.KindName(entry.Kind))})");
            }

            var source = entry.Work ?? throw new LibraryEditException("the entry names no work to merge");
            var targetReference = parseResult.GetValue(into)
                ?? entry.Candidates?.OrderByDescending(c => c.Confidence).FirstOrDefault()?.Work.ToString()
                ?? throw new LibraryEditException("the entry has no candidate; name the work with --into");
            var sourceWork = WorkSupport.Resolve(context, environment, source.ToString());
            var targetWork = WorkSupport.Resolve(context, environment, targetReference);
            var result = LibraryEditor.Merge(context.Root, targetWork, sourceWork);
            LibraryEditor.RemoveReviewEntry(context.Root, entry.Id);
            WorkSupport.WriteMerge(stdout, stderr, parseResult.GetValue(json), result);
            return ExitCodes.Success;
        });
        return command;
    }

    private static Command CreateDismiss(Option<bool> json, TextWriter stdout, TextWriter stderr, CliEnvironment environment)
    {
        var entryArgument = EntryArgument();
        var library = LibraryContext.CreateOption();
        var command = new Command("dismiss", "Settle an entry without changing the works (grouping entries are remembered as not duplicates)");
        command.Arguments.Add(entryArgument);
        command.Options.Add(library);
        command.SetAction(parseResult =>
        {
            var context = LibraryContext.Open(parseResult.GetValue(library), environment, stderr);
            var entry = Find(context, parseResult.GetRequiredValue(entryArgument));
            if (entry.Kind == ReviewKind.Grouping && entry.Work is { } work && entry.Candidates is { Count: > 0 } candidates)
            {
                var self = WorkSupport.Resolve(context, environment, work.ToString());
                foreach (var candidate in candidates)
                {
                    LibraryEditor.MarkNotDuplicates(context.Root, self, WorkSupport.Resolve(context, environment, candidate.Work.ToString()));
                }
            }

            LibraryEditor.RemoveReviewEntry(context.Root, entry.Id);
            var about = entry.File is { } file ? $"about {file}" : entry.Id.ToString();
            if (parseResult.GetValue(json))
            {
                stdout.WriteLine(JsonOutput.Object(("dismissed", entry.Id.ToString())));
            }
            else
            {
                stdout.WriteLine($"Dismissed the entry {about}");
            }

            return ExitCodes.Success;
        });
        return command;
    }

    private static Argument<string> EntryArgument() => new("entry")
    {
        Description = "Position in the list, or the ID of the entry (or the start of it)",
    };

    private static string Article(string kind) => kind.StartsWith('e') ? $"an {kind}" : $"a {kind}";

    private static ReviewEntry Find(LibraryContext context, string reference)
    {
        var queue = ReviewQueue.Load(ReviewQueue.PathFor(context.Root));
        if (reference.Length is >= 1 and <= 4 && reference.All(char.IsAsciiDigit))
        {
            var position = int.Parse(reference, CultureInfo.InvariantCulture);
            if (position >= 1 && position <= queue.Entries.Count)
            {
                return queue.Entries[position - 1];
            }
        }
        else if (reference.Length > 0)
        {
            var matches = queue.Entries.Where(e => e.Id.ToString().StartsWith(reference, StringComparison.OrdinalIgnoreCase)).ToList();
            if (matches.Count == 1)
            {
                return matches[0];
            }

            if (matches.Count > 1)
            {
                throw new LibraryEditException($"'{reference}' matches {matches.Count} review entries; use more of the ID");
            }
        }

        throw new LibraryEditException($"no review entry matches '{reference}'");
    }

    private static void WriteText(TextWriter stdout, Palette palette, ReviewQueue queue, Dictionary<Ulid, string> labels)
    {
        if (queue.Entries.Count == 0)
        {
            stdout.WriteLine("Nothing to review.");
            return;
        }

        string Work(Ulid id) => labels.TryGetValue(id, out var label) ? $"{id} {label}" : id.ToString();
        var width = queue.Entries.Max(e => ReviewQueue.KindName(e.Kind).Length);
        for (var i = 0; i < queue.Entries.Count; i++)
        {
            var entry = queue.Entries[i];
            var kind = ReviewQueue.KindName(entry.Kind);
            stdout.WriteLine($"{(i + 1).ToString(CultureInfo.InvariantCulture)}. {palette.Bold(kind)}{new string(' ', width - kind.Length)}  {entry.File}");
            stdout.WriteLine($"   {palette.Dim("id".PadRight(10))}{entry.Id}");
            if (entry.Kind == ReviewKind.Grouping && entry.Work is { } work)
            {
                stdout.WriteLine($"   {palette.Dim("work".PadRight(10))}{Work(work)}");
                foreach (var candidate in entry.Candidates ?? [])
                {
                    stdout.WriteLine($"   {palette.Dim("candidate".PadRight(10))}{Work(candidate.Work)} {candidate.Confidence.ToString("0.##", CultureInfo.InvariantCulture)} {string.Join(", ", candidate.Signals)}");
                }
            }
            else
            {
                stdout.WriteLine($"   {entry.Message}");
            }
        }

        stdout.WriteLine();
        stdout.WriteLine(TextFormat.Plural(queue.Entries.Count, "entry", "entries"));
    }

    private static void WriteJson(Utf8JsonWriter writer, ReviewQueue queue)
    {
        writer.WriteStartObject();
        JsonOutput.Property(writer, "total", queue.Entries.Count);
        writer.WriteStartArray("entries");
        foreach (var entry in queue.Entries)
        {
            writer.WriteStartObject();
            JsonOutput.Property(writer, "id", entry.Id.ToString());
            JsonOutput.Property(writer, "kind", ReviewQueue.KindName(entry.Kind));
            JsonOutput.Property(writer, "createdAt", ReviewQueue.FormatTime(entry.CreatedAt));
            JsonOutput.Property(writer, "message", entry.Message);
            JsonOutput.Property(writer, "file", entry.File);
            JsonOutput.Property(writer, "work", entry.Work?.ToString());
            if (entry.Candidates is { } candidates)
            {
                writer.WriteStartArray("candidates");
                foreach (var candidate in candidates)
                {
                    writer.WriteStartObject();
                    JsonOutput.Property(writer, "work", candidate.Work.ToString());
                    writer.WriteNumber("confidence", candidate.Confidence);
                    WorkSupport.WriteStrings(writer, "signals", candidate.Signals);
                    writer.WriteEndObject();
                }

                writer.WriteEndArray();
            }

            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    }
}
