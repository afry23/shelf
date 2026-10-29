using System.CommandLine;
using System.Text.Json;
using Shelf.Core.Model;
using Shelf.Index;

namespace Shelf.Cli.Commands;

/// <summary><c>shelf refs list|fetch</c>: the references of a paper and the papers that cite it (F-PAP-11, F-PAP-12).</summary>
internal static class RefsCommand
{
    public static Command Create(Option<bool> json, TextWriter stdout, TextWriter stderr, CliEnvironment environment)
    {
        var command = new Command("refs", "References of a paper and the works that cite it: list, fetch");
        command.Subcommands.Add(List(json, stdout, stderr, environment));
        command.Subcommands.Add(Fetch(json, stdout, stderr, environment));
        return command;
    }

    private static Command List(Option<bool> json, TextWriter stdout, TextWriter stderr, CliEnvironment environment)
    {
        var work = WorkSupport.Argument();
        var library = LibraryContext.CreateOption();
        var command = new Command("list", "List the references that were loaded for a work and the works of the library that cite it (asks nobody)");
        command.Arguments.Add(work);
        command.Options.Add(library);
        command.SetAction(parseResult =>
        {
            var context = LibraryContext.Open(parseResult.GetValue(library), environment, stderr);
            var found = WorkSupport.Resolve(context, environment, parseResult.GetRequiredValue(work));
            ReferenceListing listing;
            try
            {
                listing = LibraryReferences.Describe(context.Root, found);
            }
            catch (SidecarException e)
            {
                stderr.WriteLine($"error: {e.Message}");
                return ExitCodes.Error;
            }

            if (parseResult.GetValue(json))
            {
                stdout.WriteLine(JsonOutput.Write(writer => WriteJson(writer, found, listing)));
                return ExitCodes.Success;
            }

            if (listing.References.Count == 0)
            {
                stdout.WriteLine($"{found.Title}: No references loaded (shelf refs fetch)");
            }
            else
            {
                stdout.WriteLine($"{found.Title}: {TextFormat.Plural(listing.References.Count, "reference", "references")}, {listing.InLibrary} in the library");
                foreach (var entry in listing.References)
                {
                    stdout.WriteLine("  " + Describe(entry));
                }
            }

            if (listing.CitedBy.Count == 0)
            {
                stdout.WriteLine("Cited by no work in the library");
            }
            else
            {
                stdout.WriteLine($"Cited by {TextFormat.Plural(listing.CitedBy.Count, "work", "works")} in the library:");
                foreach (var citing in listing.CitedBy)
                {
                    stdout.WriteLine($"  {citing.Id}  {citing.Title}{(citing.Year is { } year ? $" ({year})" : string.Empty)}");
                }
            }

            return ExitCodes.Success;
        });
        return command;
    }

    private static Command Fetch(Option<bool> json, TextWriter stdout, TextWriter stderr, CliEnvironment environment)
    {
        var work = WorkSupport.Argument();
        var apply = new Option<bool>("--apply") { Description = "Store the list next to the work (without it nothing is written)" };
        var library = LibraryContext.CreateOption();
        var command = new Command("fetch", "Ask Crossref for the references of a paper by its DOI; a new list replaces the old one");
        command.Arguments.Add(work);
        command.Options.Add(apply);
        command.Options.Add(library);
        command.SetAction(parseResult =>
        {
            var context = LibraryContext.Open(parseResult.GetValue(library), environment, stderr);
            var found = WorkSupport.Resolve(context, environment, parseResult.GetRequiredValue(work));
            var doi = LibraryEditor.Read(context.Root, found).Doi;
            if (string.IsNullOrWhiteSpace(doi))
            {
                stderr.WriteLine($"error: {found.Title} has no DOI; references are loaded by DOI");
                return ExitCodes.Error;
            }

            IReadOnlyList<Reference> references;
            try
            {
                references = LibraryReferences.Fetch(environment.MetadataSources, doi, environment.Cancellation);
            }
            catch (Exception e) when (e is LibraryEditException or MetadataSourceException)
            {
                stderr.WriteLine($"error: {e.Message}");
                return ExitCodes.Error;
            }

            var entries = LibraryReferences.Match(context.Root, found.Id, references);
            var doApply = parseResult.GetValue(apply);
            if (doApply)
            {
                LibraryReferences.Save(context.Root, found, references, DateTimeOffset.UtcNow);
            }

            var withDoi = references.Count(r => r.Doi is not null);
            var inLibrary = entries.Count(e => e.Work is not null);
            if (parseResult.GetValue(json))
            {
                stdout.WriteLine(JsonOutput.Write(writer =>
                {
                    writer.WriteStartObject();
                    JsonOutput.Property(writer, "id", found.Id.ToString());
                    JsonOutput.Property(writer, "source", LibraryReferences.SourceName);
                    JsonOutput.Property(writer, "total", references.Count);
                    JsonOutput.Property(writer, "withDoi", withDoi);
                    JsonOutput.Property(writer, "inLibrary", inLibrary);
                    JsonOutput.Property(writer, "applied", doApply);
                    writer.WriteEndObject();
                }));
                return ExitCodes.Success;
            }

            stdout.WriteLine($"{found.Title}: from {LibraryReferences.SourceName}");
            if (references.Count == 0)
            {
                stdout.WriteLine($"{LibraryReferences.SourceName} lists no references for {doi}");
            }
            else
            {
                stdout.WriteLine($"{TextFormat.Plural(references.Count, "reference", "references")} ({withDoi} with a DOI, {inLibrary} in the library)");
            }

            stdout.WriteLine(doApply
                ? references.Count == 0 ? "Nothing to store" : $"Stored {TextFormat.Plural(references.Count, "reference", "references")}"
                : "Nothing written; run again with --apply");
            return ExitCodes.Success;
        });
        return command;
    }

    private static string Describe(ReferenceEntry entry)
    {
        var r = entry.Reference;
        var text = r.Title ?? r.Unstructured ?? r.Doi ?? string.Empty;
        var by = new[] { r.Author, r.Year?.ToString(System.Globalization.CultureInfo.InvariantCulture) }.Where(p => !string.IsNullOrEmpty(p));
        var detail = by.Any() ? $" ({string.Join(", ", by)})" : string.Empty;
        var doi = r.Doi is null ? string.Empty : $"  {r.Doi}";
        return entry.Work is { } work
            ? $"[in the library] {work.Id}  {work.Title}{detail}{doi}"
            : $"{text}{detail}{doi}";
    }

    private static void WriteJson(Utf8JsonWriter writer, LibraryWork work, ReferenceListing listing)
    {
        writer.WriteStartObject();
        JsonOutput.Property(writer, "id", work.Id.ToString());
        JsonOutput.Property(writer, "title", work.Title);
        JsonOutput.Property(writer, "total", listing.References.Count);
        JsonOutput.Property(writer, "inLibrary", listing.InLibrary);
        writer.WriteStartArray("references");
        foreach (var entry in listing.References)
        {
            var r = entry.Reference;
            writer.WriteStartObject();
            JsonOutput.Property(writer, "doi", r.Doi);
            JsonOutput.Property(writer, "title", r.Title);
            JsonOutput.Property(writer, "year", r.Year);
            JsonOutput.Property(writer, "author", r.Author);
            JsonOutput.Property(writer, "unstructured", r.Unstructured);
            if (entry.Work is { } linked)
            {
                writer.WriteStartObject("work");
                JsonOutput.Property(writer, "id", linked.Id.ToString());
                JsonOutput.Property(writer, "title", linked.Title);
                writer.WriteEndObject();
            }
            else
            {
                writer.WriteNull("work");
            }

            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteStartArray("citedBy");
        foreach (var citing in listing.CitedBy)
        {
            writer.WriteStartObject();
            JsonOutput.Property(writer, "id", citing.Id.ToString());
            JsonOutput.Property(writer, "title", citing.Title);
            JsonOutput.Property(writer, "year", citing.Year);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    }
}
