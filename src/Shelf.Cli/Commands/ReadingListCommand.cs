using System.CommandLine;
using Shelf.Core.Model;
using Shelf.Core.Search;
using Shelf.Index;

namespace Shelf.Cli.Commands;

/// <summary><c>shelf readlist add|rm|show</c> (F-RLS-1 to F-RLS-3).</summary>
internal static class ReadingListCommand
{
    public static Command Create(Option<bool> json, TextWriter stdout, TextWriter stderr, CliEnvironment environment)
    {
        var command = new Command("readlist", "Manage the reading list: add, rm, show");
        command.Subcommands.Add(Change("add", "Put works (books, papers, articles) on the reading list", json, stdout, stderr, environment, add: true));
        command.Subcommands.Add(Change("rm", "Take works off the reading list", json, stdout, stderr, environment, add: false));
        command.Subcommands.Add(Show(json, stdout, stderr, environment));
        return command;
    }

    private static Command Change(string name, string description, Option<bool> json, TextWriter stdout, TextWriter stderr, CliEnvironment environment, bool add)
    {
        var works = new Argument<string[]>("work")
        {
            Description = "ID of a work (or the start of it), or the path of its folder or of one of its files",
            Arity = ArgumentArity.OneOrMore,
        };
        var library = LibraryContext.CreateOption();
        var command = new Command(name, description);
        command.Arguments.Add(works);
        command.Options.Add(library);
        command.SetAction(parseResult =>
        {
            var context = LibraryContext.Open(parseResult.GetValue(library), environment, stderr);
            var done = new List<(LibraryWork Work, bool Changed, WorkSidecar Sidecar)>();
            var failed = false;
            foreach (var reference in parseResult.GetRequiredValue(works))
            {
                try
                {
                    var work = WorkSupport.Resolve(context, environment, reference);
                    var (changed, sidecar) = LibraryReadingList.Set(context.Root, work, add);
                    done.Add((work, changed, sidecar));
                }
                catch (LibraryEditException e)
                {
                    stderr.WriteLine($"error: {e.Message}");
                    failed = true;
                }
            }

            if (parseResult.GetValue(json))
            {
                stdout.WriteLine(JsonOutput.Write(writer =>
                {
                    writer.WriteStartObject();
                    writer.WriteStartArray("works");
                    foreach (var (work, changed, sidecar) in done)
                    {
                        writer.WriteStartObject();
                        JsonOutput.Property(writer, "id", work.Id.ToString());
                        JsonOutput.Property(writer, "title", sidecar.Title);
                        JsonOutput.Property(writer, "onReadingList", sidecar.ReadingListAt is not null);
                        JsonOutput.Property(writer, "changed", changed);
                        writer.WriteEndObject();
                    }

                    writer.WriteEndArray();
                    writer.WriteEndObject();
                }));
            }
            else
            {
                foreach (var (_, changed, sidecar) in done)
                {
                    var verb = (add, changed) switch
                    {
                        (true, true) => "Put on the reading list",
                        (true, false) => "Already on the reading list",
                        (false, true) => "Taken off the reading list",
                        _ => "Not on the reading list",
                    };
                    stdout.WriteLine($"{verb}: {sidecar.Title}");
                }
            }

            return failed ? ExitCodes.Error : ExitCodes.Success;
        });
        return command;
    }

    private static Command Show(Option<bool> json, TextWriter stdout, TextWriter stderr, CliEnvironment environment)
    {
        var library = LibraryContext.CreateOption();
        var command = new Command("show", "Show the reading list, the work put on first at the top");
        command.Options.Add(library);
        command.SetAction(parseResult =>
        {
            var context = LibraryContext.Open(parseResult.GetValue(library), environment, stderr);
            using var connection = IndexDatabase.OpenRead(IndexDatabase.PathFor(context.Root));
            var page = LibraryBrowse.Browse(connection, QueryParser.Parse("readinglist:yes"), new BrowseOptions(WorkSort.ReadingList, false, 0, BrowseOptions.MaxLimit));

            if (parseResult.GetValue(json))
            {
                stdout.WriteLine(JsonOutput.Write(writer =>
                {
                    writer.WriteStartObject();
                    JsonOutput.Property(writer, "total", page.Total);
                    writer.WriteStartArray("works");
                    foreach (var w in page.Items)
                    {
                        writer.WriteStartObject();
                        JsonOutput.Property(writer, "id", w.Id.ToString());
                        JsonOutput.Property(writer, "title", w.Title);
                        writer.WriteStartArray("authors");
                        foreach (var author in w.Authors)
                        {
                            writer.WriteStringValue(author);
                        }

                        writer.WriteEndArray();
                        JsonOutput.Property(writer, "readStatus", w.ReadStatus);
                        JsonOutput.Property(writer, "readingListAt", w.ReadingListAt);
                        writer.WriteEndObject();
                    }

                    writer.WriteEndArray();
                    writer.WriteEndObject();
                }));
            }
            else if (page.Items.Count == 0)
            {
                stdout.WriteLine("The reading list is empty.");
            }
            else
            {
                foreach (var w in page.Items)
                {
                    var authors = w.Authors.Count == 0 ? string.Empty : "  " + string.Join(", ", w.Authors);
                    stdout.WriteLine($"{w.Id}  {w.Title}{authors}");
                }
            }

            return ExitCodes.Success;
        });
        return command;
    }
}
