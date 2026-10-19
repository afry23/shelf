using System.CommandLine;
using System.Globalization;
using System.Text.Json;
using Shelf.Core.Model;
using Shelf.Index;
using Shelf.Semantic;

namespace Shelf.Cli.Commands;

/// <summary><c>shelf similar &lt;work&gt;</c>: the works that are most like a work (F-SEM-6).</summary>
internal static class SimilarCommand
{
    private const int DefaultLimit = 10;

    public static Command Create(Option<bool> json, TextWriter stdout, TextWriter stderr, CliEnvironment environment)
    {
        var reference = WorkSupport.Argument();
        var limit = new Option<int>("--limit")
        {
            Description = "Largest number of works to show",
            DefaultValueFactory = _ => DefaultLimit,
        };
        limit.Validators.Add(result =>
        {
            if (result.GetValueOrDefault<int>() is < 1 or > 1000)
            {
                result.AddError("--limit must be between 1 and 1000");
            }
        });
        var library = LibraryContext.CreateOption();
        var command = new Command("similar", "Show the works that are most like a work, by the meaning of title, description and tags");
        command.Arguments.Add(reference);
        command.Options.Add(limit);
        command.Options.Add(library);
        command.SetAction(parseResult =>
        {
            var context = LibraryContext.Open(parseResult.GetValue(library), environment, stderr);
            var work = WorkSupport.Resolve(context, environment, parseResult.GetRequiredValue(reference));
            using var connection = IndexDatabase.OpenRead(IndexDatabase.PathFor(context.Root));

            // The vector of the work is in the index: no model is loaded.
            var store = VectorStore.OpenExisting(connection);
            var hits = store is null ? null : SemanticSearch.SimilarWorks(connection, store, work.Id.ToString(), parseResult.GetValue(limit));
            var coverage = SemanticSearch.Coverage(connection, store);

            if (parseResult.GetValue(json))
            {
                stdout.WriteLine(JsonOutput.Write(writer => WriteJson(writer, work, hits ?? [])));
            }
            else if (hits is null or { Count: 0 })
            {
                stdout.WriteLine("No results.");
            }
            else
            {
                var palette = Palette.For(environment, json: false);
                for (var i = 0; i < hits.Count; i++)
                {
                    WriteText(stdout, palette, i + 1, hits[i]);
                }
            }

            if (SemanticSearch.SimilarNote(hits, store, coverage) is { } note)
            {
                stderr.WriteLine($"note: {note}");
            }

            return ExitCodes.Success;
        });
        return command;
    }

    private static void WriteText(TextWriter stdout, Palette palette, int number, SemanticHit hit)
    {
        var work = hit.Work;
        var line = $"{number.ToString(CultureInfo.InvariantCulture)}. {palette.Bold(work.Title)} - {string.Join(", ", work.Authors)}";
        if (work.Year is { } year)
        {
            line += $" ({year.ToString(CultureInfo.InvariantCulture)})";
        }

        stdout.WriteLine($"{line} [{string.Join(", ", work.Formats)}]  {palette.Dim(work.Id.ToString())}  {hit.Score.ToString("F2", CultureInfo.InvariantCulture)}");
    }

    private static void WriteJson(Utf8JsonWriter writer, LibraryWork work, List<SemanticHit> hits)
    {
        writer.WriteStartObject();
        JsonOutput.Property(writer, "id", work.Id.ToString());
        JsonOutput.Property(writer, "title", work.Title);
        JsonOutput.Property(writer, "total", hits.Count);
        writer.WriteStartArray("results");
        foreach (var hit in hits)
        {
            var similar = hit.Work;
            writer.WriteStartObject();
            JsonOutput.Property(writer, "id", similar.Id.ToString());
            JsonOutput.Property(writer, "title", similar.Title);
            WorkSupport.WriteStrings(writer, "authors", similar.Authors);
            if (similar.Year is { } year)
            {
                JsonOutput.Property(writer, "year", year);
            }

            JsonOutput.Property(writer, "language", similar.Language);
            WorkSupport.WriteStrings(writer, "formats", similar.Formats);
            JsonOutput.Property(writer, "score", Math.Round(hit.Score, 4));
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    }
}
