using System.CommandLine;
using System.Text.Json;
using Shelf.Core.Model;
using Shelf.Core.Search;
using Shelf.Index;

namespace Shelf.Cli.Commands;

/// <summary><c>shelf tag add|rm|list|rename|merge|suggest</c> (F-TAG-1 to F-TAG-6).</summary>
internal static class TagCommand
{
    public static Command Create(Option<bool> json, TextWriter stdout, TextWriter stderr, CliEnvironment environment)
    {
        var command = new Command("tag", "Manage tags: add, rm, list, rename, merge, suggest");
        command.Subcommands.Add(Select("add", "Add a tag to works and/or to the works that a search finds", json, stdout, stderr, environment, add: true));
        command.Subcommands.Add(Select("rm", "Remove a tag (only that tag, not the ones below it) from works and/or the works that a search finds", json, stdout, stderr, environment, add: false));
        command.Subcommands.Add(List(json, stdout, stderr, environment));
        command.Subcommands.Add(Suggest(json, stdout, stderr, environment));
        command.Subcommands.Add(Move("rename", "Rename a tag and the tags below it on every work (the new name must be unused)", json, stdout, stderr, environment, merge: false));
        command.Subcommands.Add(Move("merge", "Put a tag and the tags below it into another tag on every work", json, stdout, stderr, environment, merge: true));
        return command;
    }

    private const int DefaultSuggestions = 10;

    private static Command Suggest(Option<bool> json, TextWriter stdout, TextWriter stderr, CliEnvironment environment)
    {
        var reference = WorkSupport.Argument();
        var limit = new Option<int>("--limit")
        {
            Description = "Largest number of tags to suggest",
            DefaultValueFactory = _ => DefaultSuggestions,
        };
        limit.Validators.Add(result =>
        {
            if (result.GetValueOrDefault<int>() is < 1 or > 100)
            {
                result.AddError("--limit must be between 1 and 100");
            }
        });
        var apply = new Option<bool>("--apply") { Description = "Add the suggested tags to the work (can be undone with shelf undo)" };
        var library = LibraryContext.CreateOption();
        var command = new Command("suggest", "Suggest tags from the works that are most like a work, by the meaning of title, description and tags");
        command.Arguments.Add(reference);
        command.Options.Add(limit);
        command.Options.Add(apply);
        command.Options.Add(library);
        command.SetAction(parseResult =>
        {
            var context = LibraryContext.Open(parseResult.GetValue(library), environment, stderr);
            var work = WorkSupport.Resolve(context, environment, parseResult.GetRequiredValue(reference));
            using var connection = IndexDatabase.OpenRead(IndexDatabase.PathFor(context.Root));

            // The vector of the work is in the index: no model is loaded.
            var store = Shelf.Semantic.VectorStore.OpenExisting(connection);
            var suggestions = store is null ? null : Shelf.Semantic.SemanticSearch.SuggestTags(connection, store, work.Id.ToString(), parseResult.GetValue(limit));
            var coverage = Shelf.Semantic.SemanticSearch.Coverage(connection, store);
            var found = suggestions ?? [];

            if (parseResult.GetValue(json))
            {
                stdout.WriteLine(JsonOutput.Write(writer => WriteSuggestionsJson(writer, work, found)));
            }
            else if (found.Count == 0)
            {
                stdout.WriteLine("No tag suggestions.");
            }
            else
            {
                foreach (var suggestion in found)
                {
                    var works = TextFormat.Plural(suggestion.Works, "similar work", "similar works");
                    stdout.WriteLine($"{suggestion.Tag}  ({works}, {suggestion.Score.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)})");
                }
            }

            if (Shelf.Semantic.SemanticSearch.SimilarNote(suggestions is null ? null : [], store, coverage) is { } note)
            {
                stderr.WriteLine($"note: {note}");
            }

            if (parseResult.GetValue(apply) && found.Count > 0)
            {
                // One journal for all tags, so that one shelf undo takes them back.
                var own = LibraryEditor.Read(context.Root, work).Tags;
                LibraryTags.SetTags(context.Root, work.Id, [.. own, .. found.Select(s => s.Tag)]);
                if (!parseResult.GetValue(json))
                {
                    stdout.WriteLine($"Added {TextFormat.Plural(found.Count, "tag", "tags")} to {work.Title}");
                }
            }

            return ExitCodes.Success;
        });
        return command;
    }

    private static void WriteSuggestionsJson(Utf8JsonWriter writer, LibraryWork work, List<Shelf.Semantic.TagSuggestion> suggestions)
    {
        writer.WriteStartObject();
        JsonOutput.Property(writer, "id", work.Id.ToString());
        JsonOutput.Property(writer, "title", work.Title);
        JsonOutput.Property(writer, "total", suggestions.Count);
        writer.WriteStartArray("suggestions");
        foreach (var suggestion in suggestions)
        {
            writer.WriteStartObject();
            JsonOutput.Property(writer, "tag", suggestion.Tag);
            JsonOutput.Property(writer, "works", suggestion.Works);
            JsonOutput.Property(writer, "score", suggestion.Score);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    private static Command Select(string name, string description, Option<bool> json, TextWriter stdout, TextWriter stderr, CliEnvironment environment, bool add)
    {
        var tag = new Argument<string>("tag") { Description = "The tag; '/' separates the levels, e.g. architektur/ddd" };
        var works = new Argument<string[]>("work")
        {
            Description = "Works: " + WorkSupport.Argument("work").Description,
            Arity = ArgumentArity.ZeroOrMore,
        };
        var query = new Option<string?>("--query") { Description = "A search (as for shelf search); every work it finds is included" };
        var library = LibraryContext.CreateOption();
        var command = new Command(name, description);
        command.Arguments.Add(tag);
        command.Arguments.Add(works);
        command.Options.Add(query);
        command.Options.Add(library);
        command.SetAction(parseResult =>
        {
            var given = parseResult.GetRequiredValue(tag);
            if (!TagNames.TryNormalize(given, out var normalized, out var problem))
            {
                stderr.WriteLine($"error: tag '{given}': {problem}");
                return ExitCodes.Usage;
            }

            var references = parseResult.GetValue(works) ?? [];
            var text = parseResult.GetValue(query);
            if (references.Length == 0 && text is null)
            {
                stderr.WriteLine("error: give at least one work or --query");
                return ExitCodes.Usage;
            }

            SearchQuery? search = null;
            if (text is not null)
            {
                try
                {
                    search = QueryParser.Parse(text);
                }
                catch (QueryException e)
                {
                    stderr.WriteLine($"error: {e.Message}");
                    return ExitCodes.Usage;
                }

                if (search.IsEmpty)
                {
                    stderr.WriteLine("error: the query has nothing to search for");
                    return ExitCodes.Usage;
                }
            }

            var context = LibraryContext.Open(parseResult.GetValue(library), environment, stderr);
            var ids = references.Select(r => WorkSupport.Resolve(context, environment, r).Id).ToList();
            if (search is not null)
            {
                ids.AddRange(LibraryTags.Find(context.Root, search));
            }

            ids = [.. ids.Distinct()];
            if (ids.Count == 0)
            {
                stderr.WriteLine("error: no work matches the query");
                return ExitCodes.Error;
            }

            var result = add ? LibraryTags.Add(context.Root, normalized, ids) : LibraryTags.Remove(context.Root, normalized, ids);
            if (parseResult.GetValue(json))
            {
                stdout.WriteLine(JsonOutput.Write(writer =>
                {
                    writer.WriteStartObject();
                    JsonOutput.Property(writer, "tag", normalized);
                    JsonOutput.Property(writer, "works", result.Works);
                    JsonOutput.Property(writer, "changed", result.Changed);
                    writer.WriteEndObject();
                }));
            }
            else
            {
                var unchanged = result.Works - result.Changed;
                var changed = TextFormat.Plural(result.Changed, "work", "works");
                stdout.WriteLine(add
                    ? $"Added '{normalized}' to {changed}{(unchanged > 0 ? $" ({unchanged} already had it)" : string.Empty)}"
                    : $"Removed '{normalized}' from {changed}{(unchanged > 0 ? $" ({unchanged} did not have it)" : string.Empty)}");
            }

            return ExitCodes.Success;
        });
        return command;
    }

    private static Command List(Option<bool> json, TextWriter stdout, TextWriter stderr, CliEnvironment environment)
    {
        var library = LibraryContext.CreateOption();
        var command = new Command("list", "List all tags with the number of works that carry each");
        command.Options.Add(library);
        command.SetAction(parseResult =>
        {
            var context = LibraryContext.Open(parseResult.GetValue(library), environment, stderr);
            var tags = LibraryTags.List(context.Root);
            if (parseResult.GetValue(json))
            {
                stdout.WriteLine(JsonOutput.Write(writer =>
                {
                    writer.WriteStartObject();
                    writer.WriteStartArray("tags");
                    foreach (var tag in tags)
                    {
                        writer.WriteStartObject();
                        JsonOutput.Property(writer, "tag", tag.Tag);
                        JsonOutput.Property(writer, "works", tag.Works);
                        writer.WriteEndObject();
                    }

                    writer.WriteEndArray();
                    writer.WriteEndObject();
                }));
            }
            else if (tags.Count == 0)
            {
                stdout.WriteLine("No tags.");
            }
            else
            {
                var width = tags.Max(t => TextFormat.Number(t.Works).Length);
                foreach (var tag in tags)
                {
                    stdout.WriteLine($"{TextFormat.Number(tag.Works).PadLeft(width)}  {tag.Tag}");
                }
            }

            return ExitCodes.Success;
        });
        return command;
    }

    private static Command Move(string name, string description, Option<bool> json, TextWriter stdout, TextWriter stderr, CliEnvironment environment, bool merge)
    {
        var from = new Argument<string>(merge ? "from" : "old") { Description = "The tag to change" };
        var to = new Argument<string>(merge ? "into" : "new") { Description = merge ? "The tag to put it into (may exist)" : "The new name (must be unused)" };
        var library = LibraryContext.CreateOption();
        var command = new Command(name, description);
        command.Arguments.Add(from);
        command.Arguments.Add(to);
        command.Options.Add(library);
        command.SetAction(parseResult =>
        {
            var names = new List<string>();
            foreach (var given in new[] { parseResult.GetRequiredValue(from), parseResult.GetRequiredValue(to) })
            {
                if (!TagNames.TryNormalize(given, out var normalized, out var problem))
                {
                    stderr.WriteLine($"error: tag '{given}': {problem}");
                    return ExitCodes.Usage;
                }

                names.Add(normalized);
            }

            if (names[0] == names[1])
            {
                stderr.WriteLine("error: the new name is the old one");
                return ExitCodes.Usage;
            }

            var context = LibraryContext.Open(parseResult.GetValue(library), environment, stderr);
            var result = merge ? LibraryTags.Merge(context.Root, names[0], names[1]) : LibraryTags.Rename(context.Root, names[0], names[1]);
            if (parseResult.GetValue(json))
            {
                stdout.WriteLine(JsonOutput.Write(writer =>
                {
                    writer.WriteStartObject();
                    JsonOutput.Property(writer, "from", names[0]);
                    JsonOutput.Property(writer, "to", names[1]);
                    JsonOutput.Property(writer, "changed", result.Changed);
                    writer.WriteEndObject();
                }));
            }
            else
            {
                var changed = TextFormat.Plural(result.Changed, "work", "works");
                stdout.WriteLine(merge
                    ? $"Merged '{names[0]}' into '{names[1]}' in {changed}"
                    : $"Renamed '{names[0]}' to '{names[1]}' in {changed}");
            }

            return ExitCodes.Success;
        });
        return command;
    }
}
