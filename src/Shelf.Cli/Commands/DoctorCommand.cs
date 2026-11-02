using System.CommandLine;
using Shelf.Library;

namespace Shelf.Cli.Commands;

/// <summary><c>shelf doctor</c>: a health check of the library that changes nothing (F-DOC-1, F-DOC-2).</summary>
internal static class DoctorCommand
{
    public static Command Create(Option<bool> json, TextWriter stdout, TextWriter stderr, CliEnvironment environment)
    {
        var strict = new Option<bool>("--strict") { Description = "Also end with exit code 1 if there are warnings, not only for errors" };
        var library = LibraryContext.CreateOption();
        var command = new Command("doctor", "Check the library for broken and incomplete data and say how to fix it");
        command.Options.Add(strict);
        command.Options.Add(library);
        command.SetAction(parseResult =>
        {
            var context = LibraryContext.Open(parseResult.GetValue(library), environment, stderr);
            var report = LibraryDoctor.Run(context.Root);
            if (parseResult.GetValue(json))
            {
                stdout.WriteLine(JsonOutput.Write(writer => WriteJson(writer, report)));
            }
            else
            {
                WriteText(stdout, report);
            }

            return report.Errors > 0 || (parseResult.GetValue(strict) && report.Warnings > 0) ? ExitCodes.Error : ExitCodes.Success;
        });
        return command;
    }

    private static void WriteText(TextWriter stdout, DoctorReport report)
    {
        stdout.WriteLine($"shelf doctor: {TextFormat.Plural(report.Works, "work", "works")} checked");
        foreach (var check in report.Checks)
        {
            var label = check.Status == "warning" ? "warn" : check.Status;
            stdout.WriteLine($"[{label}] {check.Id}: {check.Summary}");
            foreach (var example in check.Examples)
            {
                var id = example.Id is null ? string.Empty : $" ({example.Id})";
                var detail = example.Detail is null ? string.Empty : $": {example.Detail}";
                stdout.WriteLine($"    {example.Title}{id}{detail}");
            }

            if (check.Count > check.Examples.Length)
            {
                stdout.WriteLine($"    … and {check.Count - check.Examples.Length} more (shelf doctor --json lists the first {LibraryDoctor.MaxExamples} of each check)");
            }

            if (check.Hint is not null)
            {
                stdout.WriteLine($"    fix: {check.Hint}");
            }
        }

        stdout.WriteLine($"Result: {TextFormat.Plural(report.Errors, "error", "errors")}, {TextFormat.Plural(report.Warnings, "warning", "warnings")}, {TextFormat.Plural(report.Notes, "note", "notes")}");
    }

    private static void WriteJson(System.Text.Json.Utf8JsonWriter writer, DoctorReport report)
    {
        writer.WriteStartObject();
        writer.WriteNumber("works", report.Works);
        writer.WriteNumber("errors", report.Errors);
        writer.WriteNumber("warnings", report.Warnings);
        writer.WriteNumber("notes", report.Notes);
        writer.WriteStartArray("checks");
        foreach (var check in report.Checks)
        {
            writer.WriteStartObject();
            JsonOutput.Property(writer, "id", check.Id);
            JsonOutput.Property(writer, "status", check.Status);
            JsonOutput.Property(writer, "summary", check.Summary);
            writer.WriteNumber("count", check.Count);
            writer.WriteStartArray("examples");
            foreach (var example in check.Examples)
            {
                writer.WriteStartObject();
                JsonOutput.Property(writer, "id", example.Id);
                JsonOutput.Property(writer, "title", example.Title);
                JsonOutput.Property(writer, "detail", example.Detail);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            JsonOutput.Property(writer, "hint", check.Hint);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    }
}
