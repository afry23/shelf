using System.CommandLine;
using Shelf.Core.Search;

namespace Shelf.Cli.Commands;

/// <summary><c>shelf searches list|add|rm</c>: searches with a name (F-SRC-8). A name written as <c>@name</c> in a query stands for the saved query (F-SRC-9).</summary>
internal static class SearchesCommand
{
    public static Command Create(Option<bool> json, TextWriter stdout, TextWriter stderr, CliEnvironment environment)
    {
        var command = new Command("searches", "Saved searches: list, add, rm. Use one in a query as @name");
        command.Subcommands.Add(List(json, stdout, stderr, environment));
        command.Subcommands.Add(Add(json, stdout, stderr, environment));
        command.Subcommands.Add(Remove(json, stdout, stderr, environment));
        return command;
    }

    private static Command List(Option<bool> json, TextWriter stdout, TextWriter stderr, CliEnvironment environment)
    {
        var library = LibraryContext.CreateOption();
        var command = new Command("list", "List the saved searches");
        command.Options.Add(library);
        command.SetAction(parseResult =>
        {
            var context = LibraryContext.Open(parseResult.GetValue(library), environment, stderr);
            if (!TryLoad(context.Root, stderr, out var set))
            {
                return ExitCodes.Error;
            }

            if (parseResult.GetValue(json))
            {
                stdout.WriteLine(JsonOutput.Write(writer =>
                {
                    writer.WriteStartObject();
                    writer.WriteStartArray("searches");
                    foreach (var item in set.Items)
                    {
                        writer.WriteStartObject();
                        JsonOutput.Property(writer, "name", item.Name);
                        JsonOutput.Property(writer, "query", item.Query);
                        writer.WriteEndObject();
                    }

                    writer.WriteEndArray();
                    writer.WriteEndObject();
                }));
            }
            else if (set.Items.Count == 0)
            {
                stdout.WriteLine("No saved searches. Save one with: shelf searches add <name> <query>");
            }
            else
            {
                foreach (var item in set.Items)
                {
                    stdout.WriteLine($"@{item.Name}  {item.Query}");
                }
            }

            return ExitCodes.Success;
        });
        return command;
    }

    private static Command Add(Option<bool> json, TextWriter stdout, TextWriter stderr, CliEnvironment environment)
    {
        var name = new Argument<string>("name") { Description = "Letters, digits, _ . and -, at most 40; used as @name in a query" };
        var words = new Argument<string[]>("query")
        {
            Description = "The query, with the same words and filters as search",
            Arity = ArgumentArity.OneOrMore,
        };
        var replace = new Option<bool>("--replace") { Description = "Replace the saved search that has this name" };
        var library = LibraryContext.CreateOption();
        var command = new Command("add", "Save a search under a name");
        command.Arguments.Add(name);
        command.Arguments.Add(words);
        command.Options.Add(replace);
        command.Options.Add(library);
        command.SetAction(parseResult =>
        {
            var context = LibraryContext.Open(parseResult.GetValue(library), environment, stderr);
            if (!TryLoad(context.Root, stderr, out var set))
            {
                return ExitCodes.Error;
            }

            try
            {
                var item = set.Add(parseResult.GetRequiredValue(name), string.Join(' ', parseResult.GetRequiredValue(words)), parseResult.GetValue(replace));
                set.Save(context.Root);
                stdout.WriteLine(parseResult.GetValue(json)
                    ? JsonOutput.Object(("name", item.Name), ("query", item.Query))
                    : $"Saved @{item.Name}: {item.Query}");
                return ExitCodes.Success;
            }
            catch (SavedSearchException e)
            {
                stderr.WriteLine($"error: {e.Message}");
                return ExitCodes.Error;
            }
        });
        return command;
    }

    private static Command Remove(Option<bool> json, TextWriter stdout, TextWriter stderr, CliEnvironment environment)
    {
        var name = new Argument<string>("name") { Description = "The name of the saved search" };
        var library = LibraryContext.CreateOption();
        var command = new Command("rm", "Remove a saved search");
        command.Arguments.Add(name);
        command.Options.Add(library);
        command.SetAction(parseResult =>
        {
            var context = LibraryContext.Open(parseResult.GetValue(library), environment, stderr);
            if (!TryLoad(context.Root, stderr, out var set))
            {
                return ExitCodes.Error;
            }

            var key = parseResult.GetRequiredValue(name);
            if (!set.Remove(key))
            {
                stderr.WriteLine($"error: no saved search '{key}'");
                return ExitCodes.Error;
            }

            try
            {
                set.Save(context.Root);
            }
            catch (SavedSearchException e)
            {
                stderr.WriteLine($"error: {e.Message}");
                return ExitCodes.Error;
            }

            stdout.WriteLine(parseResult.GetValue(json) ? JsonOutput.Object(("removed", key)) : $"Removed @{key}");
            return ExitCodes.Success;
        });
        return command;
    }

    private static bool TryLoad(string root, TextWriter stderr, out SavedSearches set)
    {
        try
        {
            set = SavedSearches.Load(root);
            return true;
        }
        catch (SavedSearchException e)
        {
            stderr.WriteLine($"error: {e.Message}");
            set = new SavedSearches();
            return false;
        }
    }
}
