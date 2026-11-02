using System.CommandLine;
using System.Text.Json;
using Shelf.Core.Model;
using Shelf.Core.Search;
using Shelf.Index;

namespace Shelf.Cli.Commands;

/// <summary><c>shelf notes list|add|bookmark|rm|export</c>: the highlights, notes and bookmarks of a work (F-ANN-6, F-RDR-6). Highlights are made in the reader.</summary>
internal static class NotesCommand
{
    public static Command Create(Option<bool> json, TextWriter stdout, TextWriter stderr, CliEnvironment environment)
    {
        var command = new Command("notes", "Highlights, notes and bookmarks of a work: list, add, bookmark, rm, export, resurface");
        command.Subcommands.Add(List(json, stdout, stderr, environment));
        command.Subcommands.Add(Add(json, stdout, stderr, environment));
        command.Subcommands.Add(Bookmark(json, stdout, stderr, environment));
        command.Subcommands.Add(Remove(json, stdout, stderr, environment));
        command.Subcommands.Add(Export(json, stdout, stderr, environment));
        command.Subcommands.Add(Resurface(json, stdout, stderr, environment));
        command.Subcommands.Add(Files(json, stdout, stderr, environment));
        return command;
    }

    /// <summary><c>shelf notes files</c>: the notes files of a work (F-NOTEF-6). They are added with <c>shelf add --notes</c>.</summary>
    private static Command Files(Option<bool> json, TextWriter stdout, TextWriter stderr, CliEnvironment environment)
    {
        var work = WorkSupport.Argument();
        var library = LibraryContext.CreateOption();
        var command = new Command("files", "List the notes files of a work (add one with shelf add --notes)");
        command.Arguments.Add(work);
        command.Options.Add(library);
        command.SetAction(parseResult =>
        {
            var context = LibraryContext.Open(parseResult.GetValue(library), environment, stderr);
            var found = WorkSupport.Resolve(context, environment, parseResult.GetRequiredValue(work));
            var files = LibraryEditor.Read(context.Root, found).Files.Where(f => f.Role == FileRole.Notes).ToList();
            if (parseResult.GetValue(json))
            {
                stdout.WriteLine(JsonOutput.Write(writer =>
                {
                    writer.WriteStartArray();
                    foreach (var file in files)
                    {
                        writer.WriteStartObject();
                        JsonOutput.Property(writer, "file", file.Name);
                        JsonOutput.Property(writer, "role", "notes");
                        writer.WriteEndObject();
                    }

                    writer.WriteEndArray();
                }));
            }
            else if (files.Count == 0)
            {
                stdout.WriteLine($"No notes files for {found.Title}");
            }
            else
            {
                foreach (var file in files)
                {
                    stdout.WriteLine(file.Name);
                }
            }

            return ExitCodes.Success;
        });
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

    private static Command Export(Option<bool> json, TextWriter stdout, TextWriter stderr, CliEnvironment environment)
    {
        var work = new Argument<string?>("work")
        {
            Description = "ID of the work, or the start of it (not with --all)",
            Arity = ArgumentArity.ZeroOrOne,
        };
        var all = new Option<bool>("--all") { Description = "Export all works that have highlights or notes, not one work" };
        var query = new Option<string?>("--query") { Description = "With --all: only the works that match, with the same words and filters as search" };
        var format = new Option<string>("--format")
        {
            Description = "With --all: markdown (one <cite key>.md per work) or readwise-csv (one CSV for the Readwise import)",
            DefaultValueFactory = _ => "markdown",
        };
        var output = new Option<string?>("--out") { Description = "Write the files into this folder (with --all and markdown), the CSV to this file (readwise-csv), or <cite key>.md into this folder instead of standard output" };
        var library = LibraryContext.CreateOption();
        var command = new Command("export", "Write the metadata, highlights and notes of a work as Markdown, or of all works (--all)");
        command.Arguments.Add(work);
        command.Options.Add(all);
        command.Options.Add(query);
        command.Options.Add(format);
        command.Options.Add(output);
        command.Options.Add(library);
        command.SetAction(parseResult =>
        {
            var reference = parseResult.GetValue(work);
            var everything = parseResult.GetValue(all);
            if (everything && reference is not null)
            {
                stderr.WriteLine("error: give either a work or --all, not both");
                return ExitCodes.Error;
            }

            if (!everything && reference is null)
            {
                stderr.WriteLine("error: give a work, or --all for all works");
                return ExitCodes.Error;
            }

            if (!everything && parseResult.GetValue(query) is not null)
            {
                stderr.WriteLine("error: --query needs --all");
                return ExitCodes.Error;
            }

            if (everything)
            {
                return ExportAll(parseResult.GetValue(json), stdout, stderr, environment, parseResult.GetValue(library), parseResult.GetValue(query), parseResult.GetValue(format)!, parseResult.GetValue(output));
            }

            var context = LibraryContext.Open(parseResult.GetValue(library), environment, stderr);
            var found = WorkSupport.Resolve(context, environment, reference!);
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
            WriteAtomically(path, text);
            stdout.WriteLine($"Wrote {path}");
            return ExitCodes.Success;
        });
        return command;
    }

    private static int ExportAll(bool json, TextWriter stdout, TextWriter stderr, CliEnvironment environment, string? libraryOption, string? queryText, string format, string? output)
    {
        var markdown = string.Equals(format, "markdown", StringComparison.OrdinalIgnoreCase);
        var csv = string.Equals(format, "readwise-csv", StringComparison.OrdinalIgnoreCase);
        if (!markdown && !csv)
        {
            stderr.WriteLine($"error: unknown format '{format}'; use markdown or readwise-csv");
            return ExitCodes.Error;
        }

        if (markdown && output is null)
        {
            stderr.WriteLine("error: --out <folder> is needed to export the notes of all works as Markdown (one file per work)");
            return ExitCodes.Error;
        }

        SearchQuery? query = null;
        if (!string.IsNullOrWhiteSpace(queryText))
        {
            try
            {
                query = QueryParser.Parse(queryText);
            }
            catch (QueryException e)
            {
                stderr.WriteLine($"error: {e.Message}");
                return ExitCodes.Error;
            }
        }

        var context = LibraryContext.Open(libraryOption, environment, stderr);
        var works = LibraryNotes.Collect(context.Root, query);
        var written = new List<string>();
        var skipped = new List<string>();
        if (csv)
        {
            var text = ReadwiseCsv.Render(works.Select(w => (w.Sidecar, w.Annotations)));
            if (output is null)
            {
                stdout.Write(text);
                return ExitCodes.Success;
            }

            var file = Path.GetFullPath(output, environment.WorkingDirectory);
            WriteAtomically(file, text);
            written.Add(file);
        }
        else
        {
            var folder = Path.GetFullPath(output!, environment.WorkingDirectory);
            foreach (var w in works)
            {
                if (string.IsNullOrWhiteSpace(w.Sidecar.CiteKey))
                {
                    skipped.Add(w.Sidecar.Title);
                    continue;
                }

                var file = Path.Combine(folder, WorkExport.FileName(w.Sidecar));
                WriteAtomically(file, WorkExport.Render(w.Sidecar, w.Annotations));
                written.Add(file);
            }
        }

        if (json)
        {
            stdout.WriteLine(JsonOutput.Write(writer =>
            {
                writer.WriteStartObject();
                JsonOutput.Property(writer, "format", csv ? "readwise-csv" : "markdown");
                writer.WriteNumber("works", csv ? works.Count : written.Count);
                WorkSupport.WriteStrings(writer, "files", written);
                WorkSupport.WriteStrings(writer, "skipped", skipped);
                writer.WriteEndObject();
            }));
            return ExitCodes.Success;
        }

        stdout.WriteLine(csv
            ? $"Wrote {written[0]}"
            : $"Exported {TextFormat.Plural(written.Count, "work", "works")} to {Path.GetFullPath(output!, environment.WorkingDirectory)}");
        foreach (var title in skipped)
        {
            stdout.WriteLine($"Skipped '{title}': it has no cite key");
        }

        return ExitCodes.Success;
    }

    private static void WriteAtomically(string path, string text)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + ".tmp";
        File.WriteAllText(temporary, text, new System.Text.UTF8Encoding(false));
        File.Move(temporary, path, overwrite: true);
    }

    private static Command Resurface(Option<bool> json, TextWriter stdout, TextWriter stderr, CliEnvironment environment)
    {
        var count = new Option<int>("--count")
        {
            Description = $"How many highlights, 1 to {Core.Model.Resurface.MaxCount}",
            DefaultValueFactory = _ => Core.Model.Resurface.DefaultCount,
        };
        var date = new Option<string?>("--date") { Description = "The day as yyyy-MM-dd (default: today, UTC); the same day gives the same highlights" };
        var library = LibraryContext.CreateOption();
        var command = new Command("resurface", "Show a few highlights of the library for a day");
        command.Options.Add(count);
        command.Options.Add(date);
        command.Options.Add(library);
        command.SetAction(parseResult =>
        {
            var number = parseResult.GetValue(count);
            if (number is < 1 or > Core.Model.Resurface.MaxCount)
            {
                stderr.WriteLine($"error: --count must be between 1 and {Core.Model.Resurface.MaxCount}");
                return ExitCodes.Error;
            }

            var day = DateOnly.FromDateTime(DateTime.UtcNow);
            if (parseResult.GetValue(date) is { } text && !Core.Model.Resurface.TryParseDate(text, out day))
            {
                stderr.WriteLine($"error: --date must be a date as yyyy-MM-dd, not '{text}'");
                return ExitCodes.Error;
            }

            var context = LibraryContext.Open(parseResult.GetValue(library), environment, stderr);
            var picked = LibraryNotes.Resurface(context.Root, day, number);
            var dayText = day.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
            if (parseResult.GetValue(json))
            {
                stdout.WriteLine(JsonOutput.Write(writer =>
                {
                    writer.WriteStartObject();
                    JsonOutput.Property(writer, "date", dayText);
                    writer.WriteStartArray("highlights");
                    foreach (var (work, a) in picked)
                    {
                        writer.WriteStartObject();
                        JsonOutput.Property(writer, "id", a.Id.ToString());
                        JsonOutput.Property(writer, "work", work.Work.Id.ToString());
                        JsonOutput.Property(writer, "title", work.Sidecar.Title);
                        WorkSupport.WriteStrings(writer, "authors", work.Sidecar.Authors);
                        JsonOutput.Property(writer, "file", a.File);
                        JsonOutput.Property(writer, "locator", a.Locator);
                        JsonOutput.Property(writer, "quote", a.Quote!.Exact);
                        JsonOutput.Property(writer, "color", a.Color);
                        JsonOutput.Property(writer, "note", a.Note);
                        JsonOutput.Property(writer, "createdAt", WorkSupport.Time(a.CreatedAt));
                        writer.WriteEndObject();
                    }

                    writer.WriteEndArray();
                    writer.WriteEndObject();
                }));
                return ExitCodes.Success;
            }

            if (picked.Count == 0)
            {
                stdout.WriteLine("No highlights yet: select text in the reader to make one");
                return ExitCodes.Success;
            }

            foreach (var (work, a) in picked)
            {
                var place = a.Locator is null ? string.Empty : $", {Locators.Describe(a.Locator)}";
                stdout.WriteLine($"{work.Sidecar.Title}{place}");
                foreach (var line in a.Quote!.Exact.ReplaceLineEndings("\n").Split('\n'))
                {
                    stdout.WriteLine($"  > {line}");
                }

                if (a.Note is { Length: > 0 } note)
                {
                    foreach (var line in note.ReplaceLineEndings("\n").Split('\n'))
                    {
                        stdout.WriteLine($"  {line}");
                    }
                }

                stdout.WriteLine();
            }

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
