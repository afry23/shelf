using System.CommandLine;
using Shelf.Index;

namespace Shelf.Cli.Commands;

/// <summary>The <c>--title</c>, <c>--author</c> and <c>--year</c> options of the commands that make a work from a file.</summary>
internal sealed class WorkValuesOptions
{
    private readonly Option<string?> _title = new("--title") { Description = "Title of the new work (default: the file's own)" };
    private readonly Option<string[]> _authors = new("--author") { Description = "Author of the new work (repeat for several; default: the file's own)" };
    private readonly Option<int?> _year = new("--year") { Description = "Year of publication of the new work (default: the file's own)" };

    public void AddTo(Command command)
    {
        command.Options.Add(_title);
        command.Options.Add(_authors);
        command.Options.Add(_year);
    }

    public WorkValues Read(ParseResult parseResult) =>
        new(parseResult.GetValue(_title), parseResult.GetValue(_authors) is { Length: > 0 } authors ? authors : null, parseResult.GetValue(_year));
}
