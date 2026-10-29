using System.CommandLine;
using System.Text.Json;
using Shelf.Core.Model;
using Shelf.Index;

namespace Shelf.Cli.Commands;

/// <summary>
/// <c>shelf import-bib &lt;file&gt;</c> (F-PAP-6): the entries of a BibTeX file are matched to the works of the library and fill
/// their empty fields and cite keys. A preview unless <c>--apply</c>; nothing goes online.
/// </summary>
internal static class ImportBibCommand
{
    public static Command Create(Option<bool> json, TextWriter stdout, TextWriter stderr, CliEnvironment environment)
    {
        var file = new Argument<string>("file") { Description = "The BibTeX file (.bib), e.g. an export of Zotero or Mendeley" };
        var apply = new Option<bool>("--apply") { Description = "Write the changes (without it: a preview)" };
        var overwrite = new Option<bool>("--overwrite") { Description = "With --apply: values and cite keys of the file replace those of the works" };
        var library = LibraryContext.CreateOption();
        var command = new Command("import-bib", "Take over DOIs, venues and cite keys from a BibTeX file into the works that match its entries");
        command.Arguments.Add(file);
        command.Options.Add(apply);
        command.Options.Add(overwrite);
        command.Options.Add(library);
        command.SetAction(parseResult =>
        {
            var doApply = parseResult.GetValue(apply);
            var doOverwrite = parseResult.GetValue(overwrite);
            if (doOverwrite && !doApply)
            {
                stderr.WriteLine("error: --overwrite needs --apply");
                return ExitCodes.Usage;
            }

            var path = Path.GetFullPath(parseResult.GetRequiredValue(file), environment.WorkingDirectory);
            string text;
            try
            {
                text = File.ReadAllText(path);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                stderr.WriteLine($"error: cannot read {path}: {e.Message}");
                return ExitCodes.Error;
            }

            var entries = BibTeX.Parse(text);
            if (entries.Count == 0)
            {
                stderr.WriteLine($"error: no BibTeX entries found in {path}");
                return ExitCodes.Error;
            }

            var context = LibraryContext.Open(parseResult.GetValue(library), environment, stderr);
            var result = LibraryBibImport.Run(context.Root, entries, doOverwrite, doApply);
            if (parseResult.GetValue(json))
            {
                stdout.WriteLine(JsonOutput.Write(writer => WriteJson(writer, result)));
            }
            else
            {
                WriteText(stdout, result);
            }

            return ExitCodes.Success;
        });
        return command;
    }

    private static void WriteText(TextWriter stdout, BibImportResult result)
    {
        foreach (var item in result.Entries)
        {
            if (item.Work is not { } work)
            {
                stdout.WriteLine(item.Problem);
                continue;
            }

            stdout.WriteLine($"{item.Entry.Key} -> {work.Id} {work.Title} (matched by {item.Match})");
            foreach (var change in item.Changes)
            {
                stdout.WriteLine(change.Action switch
                {
                    FieldAction.Fill => $"  + {change.Field}: {change.Online}",
                    FieldAction.Overwrite => $"  ~ {change.Field}: {change.Current} -> {change.Online}",
                    _ => $"  = {change.Field}: {change.Current} (kept, bib: {change.Online})",
                });
            }

            foreach (var note in item.Notes)
            {
                stdout.WriteLine($"  ! {note}");
            }
        }

        stdout.WriteLine($"{result.Matched} of {result.Entries.Count} entries matched");
        if (result.Applied)
        {
            stdout.WriteLine($"Changed {TextFormat.Plural(result.Written, "work", "works")}");
        }
        else if (result.Written > 0)
        {
            stdout.WriteLine("Nothing written; run again with --apply");
        }
    }

    private static void WriteJson(Utf8JsonWriter writer, BibImportResult result)
    {
        writer.WriteStartObject();
        writer.WriteBoolean("applied", result.Applied);
        writer.WriteNumber("entries", result.Entries.Count);
        writer.WriteNumber("matched", result.Matched);
        writer.WriteNumber("written", result.Written);
        writer.WriteStartArray("results");
        foreach (var item in result.Entries)
        {
            writer.WriteStartObject();
            JsonOutput.Property(writer, "key", item.Entry.Key);
            if (item.Work is { } work)
            {
                JsonOutput.Property(writer, "id", work.Id.ToString());
                JsonOutput.Property(writer, "match", item.Match!);
                writer.WriteStartArray("changes");
                foreach (var change in item.Changes)
                {
                    writer.WriteStartObject();
                    JsonOutput.Property(writer, "field", change.Field);
                    JsonOutput.Property(writer, "action", change.Action.ToString().ToLowerInvariant());
                    JsonOutput.Property(writer, "bib", change.Online);
                    writer.WriteEndObject();
                }

                writer.WriteEndArray();
            }
            else
            {
                JsonOutput.Property(writer, "problem", item.Problem!);
            }

            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    }
}
