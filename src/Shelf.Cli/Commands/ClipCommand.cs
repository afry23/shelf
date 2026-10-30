using System.CommandLine;
using Shelf.Index;

namespace Shelf.Cli.Commands;

/// <summary><c>shelf clip &lt;url&gt;</c> and <c>shelf clip --html &lt;file&gt;</c> (F-ART-2, F-ART-3): a web article as Markdown with its images.</summary>
internal static class ClipCommand
{
    public static Command Create(Option<bool> json, TextWriter stdout, TextWriter stderr, CliEnvironment environment)
    {
        var address = new Argument<string?>("url")
        {
            Description = "Address of a public page (https://...); leave out when --html is given",
            Arity = ArgumentArity.ZeroOrOne,
        };
        var html = new Option<string?>("--html")
        {
            Description = "A page saved in a browser (HTML or MHTML), for pages behind a paywall; nothing goes online",
        };
        var url = new Option<string?>("--url")
        {
            Description = "With --html: the address the page came from, if the file does not say",
        };
        var library = LibraryContext.CreateOption();
        var command = new Command("clip", "Keep a web article as Markdown with the images that belong to it, as a new work");
        command.Arguments.Add(address);
        command.Options.Add(html);
        command.Options.Add(url);
        command.Options.Add(library);
        command.SetAction(parseResult =>
        {
            var page = parseResult.GetValue(address);
            var file = parseResult.GetValue(html);
            if ((page is null) == (file is null))
            {
                stderr.WriteLine("error: give either an address or --html <file>");
                return ExitCodes.Usage;
            }

            if (page is not null && parseResult.GetValue(url) is not null)
            {
                stderr.WriteLine("error: --url goes with --html");
                return ExitCodes.Usage;
            }

            var context = LibraryContext.Open(parseResult.GetValue(library), environment, stderr);
            ClipResult result;
            try
            {
                result = page is not null
                    ? LibraryArticles.ClipUrl(context.Root, page, environment.Pages, environment.Cancellation)
                    : LibraryArticles.ClipFile(context.Root, file!, parseResult.GetValue(url), environment.WorkingDirectory);
            }
            catch (LibraryEditException e)
            {
                stderr.WriteLine($"error: {e.Message}");
                return ExitCodes.Error;
            }

            foreach (var warning in result.Warnings)
            {
                stderr.WriteLine($"warning: {warning}");
            }

            if (parseResult.GetValue(json))
            {
                stdout.WriteLine(JsonOutput.Write(writer =>
                {
                    writer.WriteStartObject();
                    JsonOutput.Property(writer, "id", result.Work.ToString());
                    JsonOutput.Property(writer, "title", result.Title);
                    JsonOutput.Property(writer, "file", result.File);
                    JsonOutput.Property(writer, "folder", result.Folder);
                    writer.WriteNumber("images", result.Images);
                    writer.WriteBoolean("existing", result.Existing);
                    writer.WriteEndObject();
                }));
            }
            else if (result.Existing)
            {
                stdout.WriteLine($"Already in the library: {result.Title} as {result.Work}");
            }
            else
            {
                stdout.WriteLine($"Clipped {result.Title} as {result.Work} with {result.Images} image{(result.Images == 1 ? string.Empty : "s")}");
            }

            return ExitCodes.Success;
        });
        return command;
    }
}
