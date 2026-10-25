using System.CommandLine;
using System.Globalization;
using System.Text.Json;
using Shelf.Index;

namespace Shelf.Cli.Commands;

/// <summary>
/// <c>shelf enrich &lt;work&gt; [--apply]</c> (F-IMP-6): looks a work up in Open Library and Google Books. The only command that goes
/// online besides <c>model install</c> and <c>import --from</c>; a preview unless <c>--apply</c>, and only empty fields are filled
/// unless <c>--overwrite</c>.
/// </summary>
internal static class EnrichCommand
{
    private static readonly string[] SourceNames = ["openlibrary", "google"];

    public static Command Create(Option<bool> json, TextWriter stdout, TextWriter stderr, CliEnvironment environment)
    {
        var reference = WorkSupport.Argument();
        var apply = new Option<bool>("--apply") { Description = "Write the metadata (without it, only show what would change)" };
        var overwrite = new Option<bool>("--overwrite") { Description = "Replace values that are already set (needs --apply)" };
        var pick = new Option<int?>("--pick") { Description = "Candidate to take when several books match the title and author" };
        var noCover = new Option<bool>("--no-cover") { Description = "Do not load the cover" };
        var source = new Option<string?>("--source") { Description = "Ask only this source: openlibrary or google" };
        var library = LibraryContext.CreateOption();

        var command = new Command("enrich", "Load the metadata of a work from the internet (preview unless --apply)");
        command.Arguments.Add(reference);
        foreach (var option in new Option[] { apply, overwrite, pick, noCover, source, library })
        {
            command.Options.Add(option);
        }

        command.SetAction(parseResult =>
        {
            var only = parseResult.GetValue(source)?.Trim().ToLowerInvariant();
            var number = parseResult.GetValue(pick);
            var doApply = parseResult.GetValue(apply);
            var doOverwrite = parseResult.GetValue(overwrite);
            var problem =
                only is not null && !SourceNames.Contains(only) ? "--source must be openlibrary or google"
                : number is < 1 ? "--pick must be 1 or greater"
                : doOverwrite && !doApply ? "--overwrite needs --apply"
                : null;
            if (problem is not null)
            {
                stderr.WriteLine($"error: {problem}");
                return ExitCodes.Usage;
            }

            var context = LibraryContext.Open(parseResult.GetValue(library), environment, stderr);
            var work = WorkSupport.Resolve(context, environment, parseResult.GetRequiredValue(reference));
            var sidecar = LibraryEditor.Read(context.Root, work);
            var sources = environment.MetadataSources.Where(s => only is null || s.Name == only).ToList();
            var useJson = parseResult.GetValue(json);

            EnrichmentPreview preview;
            try
            {
                preview = EnrichmentService.Preview(sidecar, sources, doOverwrite, number, environment.Cancellation);
            }
            catch (EnrichmentException e)
            {
                foreach (var warning in e.Warnings)
                {
                    stderr.WriteLine($"warning: {warning}");
                }

                stderr.WriteLine($"error: {e.Message}");
                if (e.Candidates.Count > 0)
                {
                    WriteCandidates(stdout, e.Candidates, useJson);
                }

                return ExitCodes.Error;
            }

            foreach (var warning in preview.Warnings)
            {
                stderr.WriteLine($"warning: {warning}");
            }

            EnrichmentResult? result = null;
            if (doApply)
            {
                result = EnrichmentService.Apply(context.Root, work, preview, !parseResult.GetValue(noCover), environment.Cancellation);
                foreach (var warning in result.Warnings)
                {
                    stderr.WriteLine($"warning: {warning}");
                }
            }

            if (useJson)
            {
                stdout.WriteLine(JsonOutput.Write(writer => WriteJson(writer, work.Id.ToString(), preview, result)));
            }
            else
            {
                WriteText(stdout, work.Id.ToString(), sidecar.Title, preview, result);
            }

            return ExitCodes.Success;
        });
        return command;
    }

    private static void WriteText(TextWriter stdout, string id, string title, EnrichmentPreview preview, EnrichmentResult? result)
    {
        if (result is not null)
        {
            stdout.WriteLine(result.Fields.Count > 0 ? $"Updated {id}: {string.Join(", ", result.Fields)} (from {preview.Source.Name})" : $"Nothing to change for {id}");
            if (result.Cover is { } cover)
            {
                stdout.WriteLine($"Cover saved as {cover}");
            }

            return;
        }

        stdout.WriteLine($"Online metadata for {id} \"{title}\" from {preview.Source.Name} (matched by {preview.Match})");
        foreach (var change in preview.Changes)
        {
            stdout.WriteLine(change.Action switch
            {
                FieldAction.Fill => $"  + {change.Field}: {Short(change.Online)}",
                FieldAction.Overwrite => $"  ~ {change.Field}: {Short(change.Current)} -> {Short(change.Online)}",
                _ => $"  = {change.Field}: {Short(change.Current)} (kept, online: {Short(change.Online)})",
            });
        }

        stdout.WriteLine(preview.Written.Any() ? "Nothing written; run again with --apply" : "Nothing to change");
    }

    private static string Short(string? text)
    {
        var line = (text ?? string.Empty).ReplaceLineEndings(" ");
        return line.Length <= 80 ? line : line[..79] + "…";
    }

    private static void WriteJson(Utf8JsonWriter writer, string id, EnrichmentPreview preview, EnrichmentResult? result)
    {
        writer.WriteStartObject();
        JsonOutput.Property(writer, "id", id);
        JsonOutput.Property(writer, "source", preview.Source.Name);
        JsonOutput.Property(writer, "match", preview.Match);
        JsonOutput.Property(writer, "applied", result is not null);
        writer.WriteStartArray("changes");
        foreach (var change in preview.Written)
        {
            writer.WriteStartObject();
            JsonOutput.Property(writer, "field", change.Field);
            JsonOutput.Property(writer, "action", change.Action.ToString().ToLowerInvariant());
            JsonOutput.Property(writer, "before", change.Current);
            JsonOutput.Property(writer, "after", change.Online);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteStartArray("kept");
        foreach (var change in preview.Changes.Where(c => !c.IsWritten))
        {
            writer.WriteStartObject();
            JsonOutput.Property(writer, "field", change.Field);
            JsonOutput.Property(writer, "current", change.Current);
            JsonOutput.Property(writer, "online", change.Online);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        if (result is not null)
        {
            JsonOutput.Property(writer, "cover", result.Cover);
        }

        writer.WriteEndObject();
    }

    private static void WriteCandidates(TextWriter stdout, IReadOnlyList<OnlineBook> candidates, bool json)
    {
        if (json)
        {
            stdout.WriteLine(JsonOutput.Write(writer =>
            {
                writer.WriteStartObject();
                writer.WriteStartArray("candidates");
                foreach (var book in candidates)
                {
                    writer.WriteStartObject();
                    JsonOutput.Property(writer, "title", book.Title);
                    WorkSupport.WriteStrings(writer, "authors", book.Authors);
                    JsonOutput.Property(writer, "publisher", book.Publisher);
                    JsonOutput.Property(writer, "year", book.Year);
                    JsonOutput.Property(writer, "isbn13", book.Isbn13);
                    writer.WriteEndObject();
                }

                writer.WriteEndArray();
                writer.WriteEndObject();
            }));
            return;
        }

        for (var i = 0; i < candidates.Count; i++)
        {
            var book = candidates[i];
            var details = string.Join(", ", new[] { string.Join(" & ", book.Authors), book.Publisher, book.Year?.ToString(CultureInfo.InvariantCulture), book.Isbn13 }.Where(d => !string.IsNullOrEmpty(d)));
            stdout.WriteLine($"{(i + 1).ToString(CultureInfo.InvariantCulture)}. {book.Title} ({details})");
        }
    }
}
