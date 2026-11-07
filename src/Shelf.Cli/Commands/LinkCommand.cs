using System.CommandLine;
using Shelf.Index;

namespace Shelf.Cli.Commands;

/// <summary><c>shelf link</c>, <c>shelf unlink</c> and <c>shelf links</c>: links between works made by hand (F-LNK-2).</summary>
internal static class LinkCommand
{
    private const string Reference = "ID of a work (or the start of it), or the path of its folder or of one of its files";

    public static Command CreateLink(Option<bool> json, TextWriter stdout, TextWriter stderr, CliEnvironment environment)
    {
        var first = new Argument<string>("work") { Description = Reference };
        var second = new Argument<string>("other") { Description = Reference };
        var note = new Option<string?>("--note") { Description = "Why the first work is linked with the second (one line); an empty text removes the note" };
        var otherNote = new Option<string?>("--other-note") { Description = "Why the second work is linked with the first (one line)" };
        var library = LibraryContext.CreateOption();
        var command = new Command("link", "Link two works by hand and say why");
        command.Arguments.Add(first);
        command.Arguments.Add(second);
        command.Options.Add(note);
        command.Options.Add(otherNote);
        command.Options.Add(library);
        command.SetAction(parseResult =>
        {
            var context = LibraryContext.Open(parseResult.GetValue(library), environment, stderr);
            try
            {
                var a = WorkSupport.Resolve(context, environment, parseResult.GetRequiredValue(first));
                var b = WorkSupport.Resolve(context, environment, parseResult.GetRequiredValue(second));
                var change = LibraryLinks.Add(context.Root, a, b, parseResult.GetValue(note), parseResult.GetValue(otherNote));
                Report(parseResult.GetValue(json), stdout, a, b, change.Changed, $"Linked {a.Title} and {b.Title}");
                return ExitCodes.Success;
            }
            catch (LibraryEditException e)
            {
                stderr.WriteLine($"error: {e.Message}");
                return ExitCodes.Error;
            }
        });
        return command;
    }

    public static Command CreateUnlink(Option<bool> json, TextWriter stdout, TextWriter stderr, CliEnvironment environment)
    {
        var first = new Argument<string>("work") { Description = Reference };
        var second = new Argument<string>("other") { Description = Reference };
        var library = LibraryContext.CreateOption();
        var command = new Command("unlink", "Remove the link between two works");
        command.Arguments.Add(first);
        command.Arguments.Add(second);
        command.Options.Add(library);
        command.SetAction(parseResult =>
        {
            var context = LibraryContext.Open(parseResult.GetValue(library), environment, stderr);
            try
            {
                var a = WorkSupport.Resolve(context, environment, parseResult.GetRequiredValue(first));
                var b = WorkSupport.Resolve(context, environment, parseResult.GetRequiredValue(second));
                var change = LibraryLinks.Remove(context.Root, a, b);
                Report(parseResult.GetValue(json), stdout, a, b, change.Changed, $"Unlinked {a.Title} and {b.Title}");
                return ExitCodes.Success;
            }
            catch (LibraryEditException e)
            {
                stderr.WriteLine($"error: {e.Message}");
                return ExitCodes.Error;
            }
        });
        return command;
    }

    public static Command CreateList(Option<bool> json, TextWriter stdout, TextWriter stderr, CliEnvironment environment)
    {
        var reference = WorkSupport.Argument();
        var library = LibraryContext.CreateOption();
        var command = new Command("links", "List the works linked with a work, with the note of its side");
        command.Arguments.Add(reference);
        command.Options.Add(library);
        command.SetAction(parseResult =>
        {
            var context = LibraryContext.Open(parseResult.GetValue(library), environment, stderr);
            try
            {
                var work = WorkSupport.Resolve(context, environment, parseResult.GetRequiredValue(reference));
                var links = LibraryLinks.List(context.Root, work.Id);
                if (parseResult.GetValue(json))
                {
                    stdout.WriteLine(JsonOutput.Write(writer =>
                    {
                        writer.WriteStartObject();
                        JsonOutput.Property(writer, "id", work.Id.ToString());
                        writer.WriteStartArray("links");
                        foreach (var link in links)
                        {
                            writer.WriteStartObject();
                            JsonOutput.Property(writer, "id", link.Id.ToString());
                            JsonOutput.Property(writer, "title", link.Title);
                            WorkSupport.WriteStrings(writer, "authors", link.Authors);
                            JsonOutput.Property(writer, "year", link.Year);
                            JsonOutput.Property(writer, "kind", link.Kind);
                            JsonOutput.Property(writer, "note", link.Note);
                            writer.WriteEndObject();
                        }

                        writer.WriteEndArray();
                        writer.WriteEndObject();
                    }));
                }
                else if (links.Count == 0)
                {
                    stdout.WriteLine("No links");
                }
                else
                {
                    foreach (var link in links)
                    {
                        stdout.WriteLine(Line(link));
                    }
                }

                return ExitCodes.Success;
            }
            catch (LibraryEditException e)
            {
                stderr.WriteLine($"error: {e.Message}");
                return ExitCodes.Error;
            }
        });
        return command;
    }

    /// <summary>One link as a line: title, ID, note (two spaces apart); used by <c>links</c> and <c>show</c>.</summary>
    internal static string Line(LinkedWork link) => $"{link.Title}  {link.Id}" + (link.Note is null ? string.Empty : "  " + link.Note);

    private static void Report(bool json, TextWriter stdout, LibraryWork a, LibraryWork b, bool changed, string text)
    {
        if (json)
        {
            stdout.WriteLine(JsonOutput.Write(writer =>
            {
                writer.WriteStartObject();
                JsonOutput.Property(writer, "id", a.Id.ToString());
                JsonOutput.Property(writer, "other", b.Id.ToString());
                writer.WriteBoolean("changed", changed);
                writer.WriteEndObject();
            }));
        }
        else
        {
            stdout.WriteLine(text);
        }
    }
}
