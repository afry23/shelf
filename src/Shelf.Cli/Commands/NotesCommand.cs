using System.CommandLine;
using System.Text.Json;
using Shelf.Core.Model;
using Shelf.Index;

namespace Shelf.Cli.Commands;

/// <summary><c>shelf notes list|add|rm|export</c>: the highlights and notes of a work (F-ANN-6). Highlights are made in the reader.</summary>
internal static class NotesCommand
{
    public static Command Create(Option<bool> json, TextWriter stdout, TextWriter stderr, CliEnvironment environment)
    {
        var command = new Command("notes", "Highlights and notes of a work: list, add, rm, export");
        command.Subcommands.Add(List(json, stdout, stderr, environment));
        command.Subcommands.Add(Add(json, stdout, stderr, environment));
        command.Subcommands.Add(Remove(json, stdout, stderr, environment));
        command.Subcommands.Add(Export(stdout, stderr, environment));
        return command;
    }

    private static Command List(Option<bool> json, TextWriter stdout, TextWriter stderr, CliEnvironment environment)
    {
        var work = WorkSupport.Argument();
        var library = LibraryContext.CreateOption();
        var command = new Command("list", "List the highlights and notes of a work");
        command.Arguments.Add(work);
        command.Options.Add(library);
        command.SetAction(parseResult =>
        {
            var context = LibraryContext.Open(parseResult.GetValue(library), environment, stderr);
            var found = WorkSupport.Resolve(context, environment, parseResult.GetRequiredValue(work));
            var annotations = LibraryAnnotations.List(context.Root, found);
            if (parseResult.GetValue(json))
            {
                stdout.WriteLine(JsonOutput.Write(writer => WriteJson(writer, found, annotations)));
            }
            else if (annotations.Count == 0)
            {
                stdout.WriteLine($"No highlights or notes for {found.Title}");
            }
            else
            {
                stdout.WriteLine($"{found.Title}: {TextFormat.Plural(annotations.Count, "highlight or note", "highlights and notes")}");
                foreach (var a in annotations)
                {
                    stdout.WriteLine($"  {a.Id}  {Describe(a)}");
                    if (a.Quote is { } quote)
                    {
                        stdout.WriteLine($"    > {OneLine(quote.Exact)}");
                    }

                    if (a.Note is { } note)
                    {
                        foreach (var line in note.ReplaceLineEndings("\n").Split('\n'))
                        {
                            stdout.WriteLine($"    {line}");
                        }
                    }
                }
            }

            return ExitCodes.Success;
        });
        return command;
    }

    private static Command Add(Option<bool> json, TextWriter stdout, TextWriter stderr, CliEnvironment environment)
    {
        var work = WorkSupport.Argument();
        var text = new Argument<string>("text") { Description = "The note, in Markdown" };
        var file = new Option<string?>("--file") { Description = "The file the note belongs to (needed if the work has more than one)" };
        var library = LibraryContext.CreateOption();
        var command = new Command("add", "Add a note to a file of a work");
        command.Arguments.Add(work);
        command.Arguments.Add(text);
        command.Options.Add(file);
        command.Options.Add(library);
        command.SetAction(parseResult =>
        {
            var note = parseResult.GetRequiredValue(text);
            if (string.IsNullOrWhiteSpace(note))
            {
                stderr.WriteLine("error: the text of the note must not be empty");
                return ExitCodes.Usage;
            }

            var context = LibraryContext.Open(parseResult.GetValue(library), environment, stderr);
            var found = WorkSupport.Resolve(context, environment, parseResult.GetRequiredValue(work));
            var files = LibraryEditor.Read(context.Root, found).Files;
            var name = parseResult.GetValue(file);
            if (name is null)
            {
                if (files.Count != 1)
                {
                    stderr.WriteLine($"error: {found.Title} has {files.Count} files; name one with --file ({string.Join(", ", files.Select(f => f.Name))})");
                    return ExitCodes.Usage;
                }

                name = files[0].Name;
            }
            else if (!files.Any(f => f.Name == name))
            {
                stderr.WriteLine($"error: {name}: no such file in {found.Title} ({string.Join(", ", files.Select(f => f.Name))})");
                return ExitCodes.Error;
            }

            var now = DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            var created = new Annotation(Ulid.New(), name, AnnotationKind.Note, null, null, null, note, now, now);
            LibraryAnnotations.Add(context.Root, found, created);
            if (parseResult.GetValue(json))
            {
                stdout.WriteLine(JsonOutput.Object(("id", created.Id.ToString()), ("file", name)));
            }
            else
            {
                stdout.WriteLine($"Added note {created.Id} to {name}");
            }

            return ExitCodes.Success;
        });
        return command;
    }

    private static Command Remove(Option<bool> json, TextWriter stdout, TextWriter stderr, CliEnvironment environment)
    {
        var work = WorkSupport.Argument();
        var id = new Argument<string>("id") { Description = "ID of the highlight or note, as shown by notes list" };
        var library = LibraryContext.CreateOption();
        var command = new Command("rm", "Remove a highlight or a note");
        command.Arguments.Add(work);
        command.Arguments.Add(id);
        command.Options.Add(library);
        command.SetAction(parseResult =>
        {
            var context = LibraryContext.Open(parseResult.GetValue(library), environment, stderr);
            var found = WorkSupport.Resolve(context, environment, parseResult.GetRequiredValue(work));
            var given = parseResult.GetRequiredValue(id);
            if (!Ulid.TryParse(given, out var ulid) || !LibraryAnnotations.Delete(context.Root, found, ulid))
            {
                stderr.WriteLine($"error: no annotation '{given}' in {found.Title}");
                return ExitCodes.Error;
            }

            stdout.WriteLine(parseResult.GetValue(json) ? JsonOutput.Object(("removed", ulid.ToString())) : $"Removed {ulid}");
            return ExitCodes.Success;
        });
        return command;
    }

    private static Command Export(TextWriter stdout, TextWriter stderr, CliEnvironment environment)
    {
        var work = WorkSupport.Argument();
        var library = LibraryContext.CreateOption();
        var command = new Command("export", "Write the highlights and notes of a work as Markdown");
        command.Arguments.Add(work);
        command.Options.Add(library);
        command.SetAction(parseResult =>
        {
            var context = LibraryContext.Open(parseResult.GetValue(library), environment, stderr);
            var found = WorkSupport.Resolve(context, environment, parseResult.GetRequiredValue(work));
            var sidecar = LibraryEditor.Read(context.Root, found);
            stdout.WriteLine($"# {sidecar.Title}");
            stdout.WriteLine();
            stdout.WriteLine($"*{string.Join(", ", sidecar.Authors)}*");
            foreach (var a in LibraryAnnotations.List(context.Root, found))
            {
                stdout.WriteLine();
                if (a.Quote is { } quote)
                {
                    foreach (var line in quote.Exact.ReplaceLineEndings("\n").Split('\n'))
                    {
                        stdout.WriteLine($"> {line}");
                    }

                    stdout.WriteLine();
                }

                stdout.WriteLine($"*{(a.Kind == AnnotationKind.Note ? "note" : $"highlight, {Locators.Describe(a.Locator ?? string.Empty)}")}, {a.File}*");
                if (a.Note is { } note)
                {
                    stdout.WriteLine();
                    stdout.WriteLine(note);
                }
            }

            return ExitCodes.Success;
        });
        return command;
    }

    private static string Describe(Annotation a) => a.Kind == AnnotationKind.Note
        ? $"note ({a.File})"
        : $"highlight, {Locators.Describe(a.Locator ?? string.Empty)}, {a.Color} ({a.File})";

    private static string OneLine(string text) => string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private static void WriteJson(Utf8JsonWriter writer, LibraryWork work, IReadOnlyList<Annotation> annotations)
    {
        writer.WriteStartObject();
        JsonOutput.Property(writer, "id", work.Id.ToString());
        JsonOutput.Property(writer, "title", work.Title);
        writer.WriteStartArray("annotations");
        foreach (var a in annotations)
        {
            writer.WriteStartObject();
            JsonOutput.Property(writer, "id", a.Id.ToString());
            JsonOutput.Property(writer, "file", a.File);
            JsonOutput.Property(writer, "kind", AnnotationsFormat.KindName(a.Kind));
            JsonOutput.Property(writer, "locator", a.Locator);
            if (a.Quote is { } quote)
            {
                writer.WriteStartObject("quote");
                JsonOutput.Property(writer, "exact", quote.Exact);
                JsonOutput.Property(writer, "prefix", quote.Prefix);
                JsonOutput.Property(writer, "suffix", quote.Suffix);
                writer.WriteEndObject();
            }

            JsonOutput.Property(writer, "color", a.Color);
            JsonOutput.Property(writer, "note", a.Note);
            JsonOutput.Property(writer, "createdAt", WorkSupport.Time(a.CreatedAt));
            JsonOutput.Property(writer, "updatedAt", WorkSupport.Time(a.UpdatedAt));
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    }
}
