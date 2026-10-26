using System.CommandLine;
using System.Globalization;
using Shelf.Core.Model;
using Shelf.Index;

namespace Shelf.Cli.Commands;

/// <summary><c>shelf edit &lt;work&gt; --title ...</c>: changes the metadata of a work in its sidecar.</summary>
internal static class EditCommand
{
    private static readonly string[] Statuses = ["unread", "reading", "read"];

    public static Command Create(Option<bool> json, TextWriter stdout, TextWriter stderr, CliEnvironment environment)
    {
        var reference = WorkSupport.Argument();
        var title = Text("--title", "New title");
        var subtitle = Text("--subtitle", "Subtitle; \"\" removes it");
        var authors = Many("--author", "Author (repeat for several); replaces all authors");
        var year = Text("--year", "Year of publication; \"\" removes it");
        var language = Text("--language", "Language code such as en or de; \"\" removes it");
        var isbn = Text("--isbn", "ISBN-10 or ISBN-13; \"\" removes it");
        var publisher = Text("--publisher", "Publisher; \"\" removes it");
        var series = Text("--series", "Series; \"\" removes it");
        var seriesIndex = Text("--series-index", "Volume within the series; \"\" removes it");
        var edition = Text("--edition", "Edition; \"\" removes it");
        var citeKey = Text("--cite-key", "Citation key for BibTeX; \"\" removes it");
        var description = Text("--description", "Description; \"\" removes it");
        var tags = Many("--tag", "Tag (repeat for several); replaces all tags, \"\" removes them all");
        var addTags = Many("--add-tag", "Tag to add (repeatable)");
        var removeTags = Many("--remove-tag", "Tag to remove (repeatable)");
        var addRelated = Many("--add-related", "Work that is another edition of this one (repeatable); marked on both works");
        var removeRelated = Many("--remove-related", "Work that is not another edition of this one (repeatable)");
        var rating = Text("--rating", "Rating from 1 to 5; \"\" removes it");
        var status = Text("--status", "unread, reading or read");
        var library = LibraryContext.CreateOption();

        var command = new Command("edit", "Change the metadata of a work");
        command.Arguments.Add(reference);
        foreach (var option in new Option[] { title, subtitle, authors, year, language, isbn, publisher, series, seriesIndex, edition, citeKey, description, tags, addTags, removeTags, addRelated, removeRelated, rating, status, library })
        {
            command.Options.Add(option);
        }

        command.SetAction(parseResult =>
        {
            bool Given(Option option) => parseResult.GetResult(option) is not null;
            string? Value(Option<string?> option) => parseResult.GetValue(option) is { } v && v.Trim().Length > 0 ? v.Trim() : null;

            var changed = new List<string>();
            var edits = new List<Func<WorkSidecar, WorkSidecar>>();
            string? problem = null;
            void Fail(string option, string message) => problem ??= $"{option}: {message}";

            if (Given(title))
            {
                var value = Value(title);
                if (value is null) { Fail("--title", "must not be empty"); }
                else { changed.Add("title"); edits.Add(s => s with { Title = value }); }
            }

            if (Given(subtitle)) { var v = Value(subtitle); changed.Add("subtitle"); edits.Add(s => s with { Subtitle = v }); }

            if (Given(authors))
            {
                var values = parseResult.GetValue(authors)!.Select(a => a.Trim()).ToList();
                if (values.Any(a => a.Length == 0)) { Fail("--author", "must not be empty"); }
                else { changed.Add("authors"); edits.Add(s => s with { Authors = values }); }
            }

            if (Given(year))
            {
                var v = Value(year);
                if (v is not null && !(int.TryParse(v, NumberStyles.None, CultureInfo.InvariantCulture, out var y) && y is >= 1 and <= 9999)) { Fail("--year", $"'{v}' is not a year"); }
                else { var y2 = v is null ? (int?)null : int.Parse(v, CultureInfo.InvariantCulture); changed.Add("year"); edits.Add(s => s with { Year = y2 }); }
            }

            if (Given(language)) { var v = Value(language); changed.Add("language"); edits.Add(s => s with { Language = v }); }

            if (Given(isbn))
            {
                var v = Value(isbn);
                var digits = v is null ? null : new string([.. v.Where(c => c is not ('-' or ' '))]).ToUpperInvariant();
                if (v is null) { changed.Add("isbn"); edits.Add(s => s with { Isbn10 = null, Isbn13 = null }); }
                else if (digits!.Length == 13 && Isbn.IsValid13(digits)) { changed.Add("isbn"); edits.Add(s => s with { Isbn10 = null, Isbn13 = digits }); }
                else if (digits.Length == 10 && Isbn.IsValid10(digits)) { changed.Add("isbn"); edits.Add(s => s with { Isbn10 = digits, Isbn13 = Isbn.ToIsbn13(digits) }); }
                else { Fail("--isbn", $"'{v}' is not a valid ISBN"); }
            }

            if (Given(publisher)) { var v = Value(publisher); changed.Add("publisher"); edits.Add(s => s with { Publisher = v }); }
            if (Given(series)) { var v = Value(series); changed.Add("series"); edits.Add(s => s with { Series = v }); }

            if (Given(seriesIndex))
            {
                var v = Value(seriesIndex);
                if (v is not null && !double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out _)) { Fail("--series-index", $"'{v}' is not a number"); }
                else { var n = v is null ? (double?)null : double.Parse(v, CultureInfo.InvariantCulture); changed.Add("series-index"); edits.Add(s => s with { SeriesIndex = n }); }
            }

            if (Given(edition)) { var v = Value(edition); changed.Add("edition"); edits.Add(s => s with { Edition = v }); }
            if (Given(citeKey))
            {
                var v = Value(citeKey);
                if (v is not null && !CiteKeys.IsValid(v)) { Fail("--cite-key", $"'{v}' {CiteKeys.Rule}"); }
                else { changed.Add("cite-key"); edits.Add(s => s with { CiteKey = v }); }
            }

            if (Given(description)) { var v = Value(description); changed.Add("description"); edits.Add(s => s with { Description = v }); }

            var replacement = Given(tags) ? parseResult.GetValue(tags)!.Select(t => t.Trim()).Where(t => t.Length > 0).ToList() : null;
            var additions = parseResult.GetValue(addTags)?.Select(t => t.Trim()).ToList() ?? [];
            var removals = parseResult.GetValue(removeTags)?.Select(t => t.Trim()).ToList() ?? [];
            if (additions.Any(t => t.Length == 0)) { Fail("--add-tag", "must not be empty"); }
            else if (replacement is not null || additions.Count > 0 || removals.Count > 0)
            {
                changed.Add("tags");
                edits.Add(s =>
                {
                    IEnumerable<string> result = replacement ?? s.Tags;
                    return s with { Tags = [.. result.Concat(additions).Where(t => !removals.Contains(t)).Distinct(StringComparer.Ordinal)] };
                });
            }

            if (Given(rating))
            {
                var v = Value(rating);
                if (v is not null && !(int.TryParse(v, NumberStyles.None, CultureInfo.InvariantCulture, out var r) && r is >= 1 and <= 5)) { Fail("--rating", "must be between 1 and 5"); }
                else { var r2 = v is null ? (int?)null : int.Parse(v, CultureInfo.InvariantCulture); changed.Add("rating"); edits.Add(s => s with { Rating = r2 }); }
            }

            if (Given(status))
            {
                var v = Value(status)?.ToLowerInvariant();
                if (v is null || !Statuses.Contains(v)) { Fail("--status", "must be unread, reading or read"); }
                else { var st = Enum.Parse<ReadStatus>(v, ignoreCase: true); changed.Add("status"); edits.Add(s => s with { ReadStatus = st }); }
            }

            var relatedAdd = parseResult.GetValue(addRelated) ?? [];
            var relatedRemove = parseResult.GetValue(removeRelated) ?? [];
            if (relatedAdd.Length + relatedRemove.Length > 0)
            {
                changed.Add("related");
            }

            if (problem is null && changed.Count == 0)
            {
                problem = "nothing to change; give at least one option such as --title";
            }

            if (problem is not null)
            {
                stderr.WriteLine($"error: {problem}");
                return ExitCodes.Usage;
            }

            var context = LibraryContext.Open(parseResult.GetValue(library), environment, stderr);
            var work = WorkSupport.Resolve(context, environment, parseResult.GetRequiredValue(reference));
            if (edits.Count > 0)
            {
                LibraryEditor.Update(context.Root, work, sidecar => edits.Aggregate(sidecar, (current, edit) => edit(current)));
            }

            foreach (var reference in relatedAdd)
            {
                LibraryEditor.MarkRelated(context.Root, work, WorkSupport.Resolve(context, environment, reference));
            }

            foreach (var reference in relatedRemove)
            {
                LibraryEditor.UnmarkRelated(context.Root, work, WorkSupport.Resolve(context, environment, reference));
            }

            if (parseResult.GetValue(json))
            {
                stdout.WriteLine(JsonOutput.Write(writer =>
                {
                    writer.WriteStartObject();
                    JsonOutput.Property(writer, "id", work.Id.ToString());
                    WorkSupport.WriteStrings(writer, "changed", changed);
                    writer.WriteEndObject();
                }));
            }
            else
            {
                stdout.WriteLine($"Updated {work.Id}: {string.Join(", ", changed)}");
            }

            return ExitCodes.Success;
        });
        return command;
    }

    private static Option<string?> Text(string name, string description) => new(name) { Description = description };

    private static Option<string[]> Many(string name, string description) => new(name) { Description = description };
}
