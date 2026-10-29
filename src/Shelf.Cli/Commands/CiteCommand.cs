using System.CommandLine;
using Shelf.Core.Model;
using Shelf.Index;

namespace Shelf.Cli.Commands;

/// <summary><c>shelf cite &lt;work&gt; &lt;quote&gt;</c>: a passage as a quote with its citation (F-ANN-8). Nothing is written.</summary>
internal static class CiteCommand
{
    public static Command Create(Option<bool> json, TextWriter stdout, TextWriter stderr, CliEnvironment environment)
    {
        var work = WorkSupport.Argument();
        var quote = new Argument<string>("quote") { Description = "The passage, as it stands in the book" };
        var locator = new Option<string?>("--locator") { Description = "The place: page=N for a PDF, an epubcfi(...) for an EPUB" };
        var file = new Option<string?>("--file") { Description = "The file the place is in (needed with --locator if the work has more than one)" };
        var bibtex = new Option<bool>("--bibtex") { Description = "Add the BibTeX entry of the work" };
        var library = LibraryContext.CreateOption();
        var command = new Command("cite", "Print a passage as a Markdown quote with the cite key and the place of the work");
        command.Arguments.Add(work);
        command.Arguments.Add(quote);
        command.Options.Add(locator);
        command.Options.Add(file);
        command.Options.Add(bibtex);
        command.Options.Add(library);
        command.SetAction(parseResult =>
        {
            var passage = parseResult.GetRequiredValue(quote);
            if (string.IsNullOrWhiteSpace(passage) || passage.Length > QuoteFormat.MaxQuoteLength)
            {
                stderr.WriteLine($"error: the quote must be the passage, at most {QuoteFormat.MaxQuoteLength} characters");
                return ExitCodes.Usage;
            }

            var context = LibraryContext.Open(parseResult.GetValue(library), environment, stderr);
            var found = WorkSupport.Resolve(context, environment, parseResult.GetRequiredValue(work));
            var sidecar = LibraryEditor.Read(context.Root, found);
            var place = parseResult.GetValue(locator);
            if (place is not null)
            {
                var files = sidecar.Files;
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

                if (Locators.Problem(Path.GetExtension(name).TrimStart('.').ToLowerInvariant(), place) is { } problem)
                {
                    stderr.WriteLine($"error: {problem}");
                    return ExitCodes.Usage;
                }
            }
            else if (parseResult.GetValue(file) is { } named && !sidecar.Files.Any(f => f.Name == named))
            {
                stderr.WriteLine($"error: {named}: no such file in {found.Title} ({string.Join(", ", sidecar.Files.Select(f => f.Name))})");
                return ExitCodes.Error;
            }

            if (string.IsNullOrWhiteSpace(sidecar.CiteKey))
            {
                stderr.WriteLine($"error: {WorkExport.MissingCiteKey} (shelf citekeys)");
                return ExitCodes.Error;
            }

            var text = QuoteFormat.Render(sidecar.CiteKey, passage, place);
            var entry = CitationFormats.Render(CitationFormat.BibTeX, [sidecar]);
            if (parseResult.GetValue(json))
            {
                stdout.WriteLine(JsonOutput.Write(writer =>
                {
                    writer.WriteStartObject();
                    JsonOutput.Property(writer, "id", found.Id.ToString());
                    JsonOutput.Property(writer, "citeKey", sidecar.CiteKey);
                    JsonOutput.Property(writer, "place", QuoteFormat.Place(place));
                    JsonOutput.Property(writer, "text", text);
                    JsonOutput.Property(writer, "bibtex", entry);
                    writer.WriteEndObject();
                }));
            }
            else
            {
                stdout.WriteLine(text);
                if (parseResult.GetValue(bibtex))
                {
                    stdout.WriteLine();
                    stdout.Write(entry);
                }
            }

            return ExitCodes.Success;
        });
        return command;
    }
}
