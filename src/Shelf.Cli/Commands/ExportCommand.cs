using System.CommandLine;
using System.Text;
using Shelf.Core.Model;
using Shelf.Core.Search;
using Shelf.Index;

namespace Shelf.Cli.Commands;

/// <summary><c>shelf export [query]</c>: the citations of all works or of a search result as BibTeX, CSL-JSON or RIS (F-PAP-5).</summary>
internal static class ExportCommand
{
    public static Command Create(Option<bool> json, TextWriter stdout, TextWriter stderr, CliEnvironment environment)
    {
        var words = new Argument<string[]>("query")
        {
            Description = "Only the works that match, with the same words and filters as search (tag:, venue:, type:paper, ...); default: all works",
            Arity = ArgumentArity.ZeroOrMore,
        };
        var format = new Option<string>("--format")
        {
            Description = "bibtex, csl-json or ris",
            DefaultValueFactory = _ => "bibtex",
        };
        var output = new Option<string?>("--out") { Description = "Write the citations to this file instead of standard output" };
        var library = LibraryContext.CreateOption();
        var command = new Command("export", "Export citations (BibTeX, CSL-JSON, RIS) of all works or of a search result");
        command.Arguments.Add(words);
        command.Options.Add(format);
        command.Options.Add(output);
        command.Options.Add(library);
        command.SetAction(parseResult =>
        {
            if (!CitationFormats.TryParse(parseResult.GetRequiredValue(format), out var citationFormat))
            {
                stderr.WriteLine($"error: Unknown format '{parseResult.GetRequiredValue(format)}'; use bibtex, csl-json or ris");
                return ExitCodes.Usage;
            }

            SearchQuery? query = null;
            var text = string.Join(' ', parseResult.GetValue(words) ?? []);
            if (text.Length > 0)
            {
                try
                {
                    query = QueryParser.Parse(text);
                }
                catch (QueryException e)
                {
                    stderr.WriteLine($"error: {e.Message}");
                    return ExitCodes.Usage;
                }
            }

            var context = LibraryContext.Open(parseResult.GetValue(library), environment, stderr);
            var export = LibraryCitations.Export(context.Root, citationFormat, query);
            foreach (var title in export.Skipped)
            {
                stderr.WriteLine($"warning: '{title}' has no cite key (the author is unknown) and is not exported");
            }

            if (parseResult.GetValue(output) is { } path)
            {
                File.WriteAllText(path, export.Text, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                stderr.WriteLine($"Exported {export.Works} {(export.Works == 1 ? "work" : "works")} to {path}");
            }
            else
            {
                stdout.Write(export.Text);
            }

            return ExitCodes.Success;
        });
        return command;
    }
}
