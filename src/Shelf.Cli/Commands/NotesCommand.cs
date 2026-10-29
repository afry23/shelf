using System.CommandLine;
using System.Text.Json;
using Shelf.Core.Model;
using Shelf.Index;

namespace Shelf.Cli.Commands;

/// <summary><c>shelf notes list|add|bookmark|rm|export</c>: the highlights, notes and bookmarks of a work (F-ANN-6, F-RDR-6). Highlights are made in the reader.</summary>
internal static class NotesCommand
{
    public static Command Create(Option<bool> json, TextWriter stdout, TextWriter stderr, CliEnvironment environment)
    {
        var command = new Command("notes", "Highlights, notes and bookmarks of a work: list, add, bookmark, rm, export");
        command.Subcommands.Add(List(json, stdout, stderr, environment));
        command.Subcommands.Add(Add(json, stdout, stderr, environment));
        command.Subcommands.Add(Bookmark(json, stdout, stderr, environment));
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

    private static Command Bookmark(Option<bool> json, TextWriter stdout, TextWriter stderr, CliEnvironment environment)
    {
        var work = WorkSupport.Argument();
        var locator = new Argument<string>("locator") { Description = "The place: page=N for a PDF, an epubcfi(...) for an EPUB" };
        var label = new Argument<string?>("label") { Description = "A short label for the bookmark", Arity = ArgumentArity.ZeroOrOne };
        var file = new Option<string?>("--file") { Description = "The file the bookmark belongs to (needed if the work has more than one)" };
        var library = LibraryContext.CreateOption();
        var command = new Command("bookmark", "Set a bookmark at a place in a file of a work");
        command.Arguments.Add(work);
        command.Arguments.Add(locator);
        command.Arguments.Add(label);
        command.Options.Add(file);
        command.Options.Add(library);
        command.SetAction(parseResult =>
        {
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

            var place = parseResult.GetRequiredValue(locator);
            var format = Path.GetExtension(name).TrimStart('.').ToLowerInvariant();
            if (Locators.Problem(format, place) is { } problem)
            {
                stderr.WriteLine($"error: {problem}");
                return ExitCodes.Usage;
            }

            var text = parseResult.GetValue(label);
            var existing = LibraryAnnotations.List(context.Root, found)
                .FirstOrDefault(a => a.Kind == AnnotationKind.Bookmark && a.File == name && a.Locator == place);
            var bookmark = existing;
            if (existing is null)
            {
                var now = DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
                bookmark = new Annotation(Ulid.New(), name, AnnotationKind.Bookmark, place, null, null, string.IsNullOrWhiteSpace(text) ? null : text, now, now);
                LibraryAnnotations.Add(context.Root, found, bookmark);
            }

            if (parseResult.GetValue(json))
            {
                stdout.WriteLine(JsonOutput.Object(("id", bookmark!.Id.ToString()), ("file", name)));
            }
            else
            {
                stdout.WriteLine(existing is null
                    ? $"Added bookmark {bookmark!.Id} at {Locators.Describe(place)} in {name}"
                    : $"There is a bookmark at {Locators.Describe(place)} in {name} already: {existing.Id}");
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
        var output = new Option<string?>("--out") { Description = "Write <cite key>.md into this folder instead of standard output" };
        var library = LibraryContext.CreateOption();
        var command = new Command("export", "Write the metadata, highlights and notes of a work as Markdown");
        command.Arguments.Add(work);
        command.Options.Add(output);
        command.Options.Add(library);
        command.SetAction(parseResult =>
        {
            var context = LibraryContext.Open(parseResult.GetValue(library), environment, stderr);
            var found = WorkSupport.Resolve(context, environment, parseResult.GetRequiredValue(work));
            var sidecar = LibraryEditor.Read(context.Root, found);
            string name;
            string text;
            try
            {
                name = WorkExport.FileName(sidecar);
                text = WorkExport.Render(sidecar, LibraryAnnotations.List(context.Root, found));
            }
            catch (InvalidOperationException e)
            {
                stderr.WriteLine($"error: {found.Title}: {e.Message}");
                return ExitCodes.Error;
            }

            if (parseResult.GetValue(output) is not { } folder)
            {
                stdout.Write(text);
                return ExitCodes.Success;
            }

            var path = Path.Combine(Path.GetFullPath(folder, environment.WorkingDirectory), name);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var temporary = path + ".tmp";
            File.WriteAllText(temporary, text, new System.Text.UTF8Encoding(false));
            File.Move(temporary, path, overwrite: true);
            stdout.WriteLine($"Wrote {path}");
            return ExitCodes.Success;
        });
        return command;
    }

    private static string Describe(Annotation a) => a.Kind switch
    {
        AnnotationKind.Note => $"note ({a.File})",
        AnnotationKind.Bookmark => $"bookmark, {Locators.Describe(a.Locator ?? string.Empty)} ({a.File})",
        _ => $"highlight, {Locators.Describe(a.Locator ?? string.Empty)}, {a.Color} ({a.File})",
    };

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
