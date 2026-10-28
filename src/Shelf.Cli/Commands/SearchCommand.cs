using System.CommandLine;
using System.Globalization;
using Shelf.Core.Search;
using Shelf.Index;

namespace Shelf.Cli.Commands;

/// <summary><c>shelf search &lt;query&gt;</c>: full-text search over metadata and content (F-SRC-1 to F-SRC-5).</summary>
internal static class SearchCommand
{
    public static Command Create(Option<bool> json, TextWriter stdout, TextWriter stderr, CliEnvironment environment)
    {
        var words = new Argument<string[]>("query")
        {
            Description = "Words, \"phrases\", prefix*, -excluded, and author: title: tag: publisher: lang: year: format: status: filters",
            Arity = ArgumentArity.OneOrMore,
        };
        var limit = new Option<int>("--limit")
        {
            Description = "Largest number of works to show",
            DefaultValueFactory = _ => LibrarySearch.DefaultLimit,
        };
        limit.Validators.Add(result =>
        {
            if (result.GetValueOrDefault<int>() is < 1 or > 1000)
            {
                result.AddError("--limit must be between 1 and 1000");
            }
        });
        var titleOnly = new Option<bool>("--title-only") { Description = "Look only at the titles and subtitles, not at authors, tags, descriptions or the text of the books" };
        var library = LibraryContext.CreateOption();
        var command = new Command("search", "Search titles, authors, tags and the text of the books");
        command.Arguments.Add(words);
        command.Options.Add(limit);
        command.Options.Add(titleOnly);
        command.Options.Add(library);
        command.SetAction(parseResult =>
        {
            var text = string.Join(' ', parseResult.GetRequiredValue(words));
            SearchQuery query;
            try
            {
                query = QueryParser.Parse(text);
            }
            catch (QueryException e)
            {
                stderr.WriteLine($"error: {e.Message}");
                return ExitCodes.Usage;
            }

            if (query.IsEmpty)
            {
                stderr.WriteLine("error: the query has nothing to search for");
                return ExitCodes.Usage;
            }

            var context = LibraryContext.Open(parseResult.GetValue(library), environment, stderr);
            using var connection = IndexDatabase.OpenRead(IndexDatabase.PathFor(context.Root));
            var hits = LibrarySearch.Search(connection, query, parseResult.GetValue(limit), titleOnly: parseResult.GetValue(titleOnly));
            var fuzzy = false;
            if (hits.Count == 0)
            {
                hits = LibrarySearch.SearchFuzzy(connection, query, parseResult.GetValue(limit), parseResult.GetValue(titleOnly));
                fuzzy = hits.Count > 0;
                if (fuzzy)
                {
                    stderr.WriteLine("No exact match, showing similar titles and authors.");
                }
            }

            if (parseResult.GetValue(json))
            {
                stdout.WriteLine(JsonOutput.Write(writer => WriteJson(writer, text, hits, fuzzy)));
            }
            else if (hits.Count == 0)
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

            return ExitCodes.Success;
        });
        return command;
    }

    private static void WriteText(TextWriter stdout, Palette palette, int number, SearchHit hit)
    {
        var line = $"{number.ToString(CultureInfo.InvariantCulture)}. {palette.Bold(hit.Title)} - {string.Join(", ", hit.Authors)}";
        if (hit.Year is { } year)
        {
            line += $" ({year.ToString(CultureInfo.InvariantCulture)})";
        }

        stdout.WriteLine($"{line} [{string.Join(", ", hit.Formats)}]  {palette.Dim(hit.WorkId.ToString())}");
        foreach (var content in hit.Hits)
        {
            stdout.WriteLine($"   {palette.Dim($"{content.Where}  {Path.GetFileName(content.File)}:")} {content.Snippet}");
        }
    }

    private static void WriteJson(System.Text.Json.Utf8JsonWriter writer, string query, List<SearchHit> hits, bool fuzzy)
    {
        writer.WriteStartObject();
        JsonOutput.Property(writer, "query", query);
        if (fuzzy)
        {
            writer.WriteBoolean("fuzzy", true);
        }

        JsonOutput.Property(writer, "total", hits.Count);
        writer.WriteStartArray("results");
        foreach (var hit in hits)
        {
            writer.WriteStartObject();
            JsonOutput.Property(writer, "id", hit.WorkId.ToString());
            JsonOutput.Property(writer, "title", hit.Title);
            writer.WriteStartArray("authors");
            foreach (var author in hit.Authors)
            {
                writer.WriteStringValue(author);
            }

            writer.WriteEndArray();
            if (hit.Year is { } year)
            {
                JsonOutput.Property(writer, "year", year);
            }

            JsonOutput.Property(writer, "language", hit.Language);
            writer.WriteStartArray("formats");
            foreach (var format in hit.Formats)
            {
                writer.WriteStringValue(format);
            }

            writer.WriteEndArray();
            JsonOutput.Property(writer, "score", Math.Round(hit.Score, 2));
            writer.WriteStartArray("hits");
            foreach (var content in hit.Hits)
            {
                writer.WriteStartObject();
                JsonOutput.Property(writer, "file", content.File);
                JsonOutput.Property(writer, "format", content.Format);
                JsonOutput.Property(writer, "locator", content.Locator);
                JsonOutput.Property(writer, "where", content.Where);
                JsonOutput.Property(writer, "snippet", content.Snippet);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    }
}
