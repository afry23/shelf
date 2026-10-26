using System.CommandLine;
using System.Globalization;
using System.Text.Json;
using Shelf.Core.Model;
using Shelf.Index;

namespace Shelf.Cli.Commands;

/// <summary><c>shelf show &lt;work&gt;</c>: everything the sidecar says about one work.</summary>
internal static class ShowCommand
{
    private const int LabelWidth = 10;

    public static Command Create(Option<bool> json, TextWriter stdout, TextWriter stderr, CliEnvironment environment)
    {
        var reference = WorkSupport.Argument();
        var library = LibraryContext.CreateOption();
        var command = new Command("show", "Show the details of a work");
        command.Arguments.Add(reference);
        command.Options.Add(library);
        command.SetAction(parseResult =>
        {
            var context = LibraryContext.Open(parseResult.GetValue(library), environment, stderr);
            var work = WorkSupport.Resolve(context, environment, parseResult.GetRequiredValue(reference));
            var sidecar = LibraryEditor.Read(context.Root, work);
            var related = WorkLookup.Labels(context.Root, sidecar.Related);
            var files = sidecar.Files.Select(f => (File: f, Info: new FileInfo(LibraryEditor.FilePath(context.Root, work, f.Name)))).ToList();
            if (parseResult.GetValue(json))
            {
                stdout.WriteLine(JsonOutput.Write(writer => WriteJson(writer, sidecar, work, files, related)));
            }
            else
            {
                WriteText(stdout, Palette.For(environment, json: false), sidecar, work, files, related);
            }

            return ExitCodes.Success;
        });
        return command;
    }

    private static void WriteText(TextWriter stdout, Palette palette, WorkSidecar s, LibraryWork work, List<(SidecarFile File, FileInfo Info)> files, Dictionary<Ulid, string> related)
    {
        void Line(string label, string? value)
        {
            if (value is not null)
            {
                stdout.WriteLine($"{palette.Dim(label.PadRight(LabelWidth))}{value}");
            }
        }

        stdout.WriteLine(palette.Bold(s.Title));
        Line("ID", s.Id.ToString());
        Line("Subtitle", s.Subtitle);
        Line("Authors", string.Join(", ", s.Authors));
        Line("Year", s.Year?.ToString(CultureInfo.InvariantCulture));
        Line("Language", s.Language);
        Line("ISBN-13", s.Isbn13);
        Line("ISBN-10", s.Isbn10);
        Line("Publisher", s.Publisher);
        Line("Series", s.Series is null ? null : s.SeriesIndex is { } index ? $"{s.Series} #{index.ToString("0.##", CultureInfo.InvariantCulture)}" : s.Series);
        Line("Edition", s.Edition);
        Line("Cite key", s.CiteKey);
        Line("Tags", s.Tags.Count > 0 ? string.Join(", ", s.Tags) : null);
        Line("Rating", s.Rating is { } rating ? $"{rating.ToString(CultureInfo.InvariantCulture)}/5" : null);
        Line("Status", s.ReadStatus.ToString().ToLowerInvariant());
        Line("Added", s.Added?.UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        Line("Folder", work.Directory);
        var first = true;
        foreach (var id in s.Related.Where(related.ContainsKey))
        {
            Line(first ? "Related" : string.Empty, $"{related[id]}  {id}");
            first = false;
        }

        if (!string.IsNullOrWhiteSpace(s.Description))
        {
            stdout.WriteLine();
            stdout.WriteLine(s.Description);
        }

        stdout.WriteLine();
        stdout.WriteLine(palette.Bold("Files"));
        var width = files.Max(f => f.File.Name.Length);
        foreach (var (file, info) in files)
        {
            var line = $"  {file.Name.PadRight(width)}  {(info.Exists ? TextFormat.Size(info.Length) : "missing")}";
            if (file.Progress is { } progress)
            {
                line += $"  reading position {progress.Locator}";
            }

            stdout.WriteLine(line);
        }
    }

    private static void WriteJson(Utf8JsonWriter writer, WorkSidecar s, LibraryWork work, List<(SidecarFile File, FileInfo Info)> files, Dictionary<Ulid, string> related)
    {
        writer.WriteStartObject();
        JsonOutput.Property(writer, "id", s.Id.ToString());
        JsonOutput.Property(writer, "title", s.Title);
        JsonOutput.Property(writer, "subtitle", s.Subtitle);
        WorkSupport.WriteStrings(writer, "authors", s.Authors);
        JsonOutput.Property(writer, "language", s.Language);
        JsonOutput.Property(writer, "year", s.Year);
        JsonOutput.Property(writer, "isbn10", s.Isbn10);
        JsonOutput.Property(writer, "isbn13", s.Isbn13);
        JsonOutput.Property(writer, "publisher", s.Publisher);
        JsonOutput.Property(writer, "series", s.Series);
        JsonOutput.Property(writer, "seriesIndex", s.SeriesIndex);
        JsonOutput.Property(writer, "edition", s.Edition);
        JsonOutput.Property(writer, "citeKey", s.CiteKey);
        JsonOutput.Property(writer, "description", s.Description);
        WorkSupport.WriteStrings(writer, "tags", s.Tags);
        JsonOutput.Property(writer, "rating", s.Rating);
        JsonOutput.Property(writer, "readStatus", s.ReadStatus.ToString().ToLowerInvariant());
        JsonOutput.Property(writer, "added", s.Added is { } added ? WorkSupport.Time(added) : null);
        JsonOutput.Property(writer, "folder", work.Directory);
        writer.WriteStartArray("related");
        foreach (var id in s.Related.Where(related.ContainsKey))
        {
            writer.WriteStartObject();
            JsonOutput.Property(writer, "id", id.ToString());
            JsonOutput.Property(writer, "title", related[id]);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteStartArray("files");
        foreach (var (file, info) in files)
        {
            writer.WriteStartObject();
            JsonOutput.Property(writer, "name", file.Name);
            JsonOutput.Property(writer, "format", IndexWriterFormat(file.Name));
            if (info.Exists)
            {
                writer.WriteNumber("size", info.Length);
            }

            if (file.Progress is { } progress)
            {
                writer.WriteStartObject("progress");
                JsonOutput.Property(writer, "locator", progress.Locator);
                JsonOutput.Property(writer, "updatedAt", WorkSupport.Time(progress.UpdatedAt));
                writer.WriteEndObject();
            }

            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    internal static string IndexWriterFormat(string fileName) => Path.GetExtension(fileName).TrimStart('.').ToLowerInvariant();
}
