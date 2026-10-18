using System.CommandLine;
using System.Globalization;
using System.Text.Json;
using Shelf.Core.Model;
using Shelf.Index;

namespace Shelf.Cli.Commands;

/// <summary>What the commands that take a work have in common.</summary>
internal static class WorkSupport
{
    public static Argument<string> Argument(string name = "work") => new(name)
    {
        Description = "ID of the work (or the start of it), or the path of its folder or of one of its files",
    };

    public static LibraryWork Resolve(LibraryContext context, CliEnvironment environment, string reference) =>
        WorkLookup.Resolve(context.Root, reference, environment.WorkingDirectory);

    /// <summary>"Merged A into B: 2 files moved", with a warning when the folder of A was not empty.</summary>
    public static void WriteMerge(TextWriter stdout, TextWriter stderr, bool json, MergeResult result)
    {
        if (result.LeftoverFolder is { } folder)
        {
            stderr.WriteLine($"warning: {folder} still holds files that shelf does not know; the folder was kept");
        }

        if (json)
        {
            stdout.WriteLine(JsonOutput.Write(writer =>
            {
                writer.WriteStartObject();
                JsonOutput.Property(writer, "id", result.Target.ToString());
                JsonOutput.Property(writer, "merged", result.Source.ToString());
                JsonOutput.Property(writer, "movedFiles", result.MovedFiles);
                writer.WriteEndObject();
            }));
        }
        else
        {
            stdout.WriteLine($"Merged {result.Source} into {result.Target}: {TextFormat.Plural(result.MovedFiles, "file", "files")} moved");
        }
    }

    public static void WriteStrings(Utf8JsonWriter writer, string name, IEnumerable<string> values)
    {
        writer.WriteStartArray(name);
        foreach (var value in values)
        {
            writer.WriteStringValue(value);
        }

        writer.WriteEndArray();
    }

    public static string Time(DateTimeOffset time) => time.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
}
