namespace Shelf.Cli;

/// <summary>
/// The parts of the process environment that the CLI honors. It is passed in explicitly so that
/// <see cref="ShelfApp"/> never reads the process environment or the static console.
/// </summary>
public sealed class CliEnvironment
{
    private CliEnvironment(bool noColor, bool stdoutIsTerminal)
    {
        NoColor = noColor;
        StdoutIsTerminal = stdoutIsTerminal;
    }

    /// <summary>No <c>NO_COLOR</c> and standard output is not a terminal.</summary>
    public static CliEnvironment Empty { get; } = new(noColor: false, stdoutIsTerminal: false);

    /// <summary>True when <c>NO_COLOR</c> is present and non-empty (no-color.org).</summary>
    public bool NoColor { get; }

    public bool StdoutIsTerminal { get; }

    /// <summary>Colored output is allowed only on a terminal and without <c>NO_COLOR</c>.</summary>
    public bool UseColor => StdoutIsTerminal && !NoColor;

    public static CliEnvironment FromVariables(Func<string, string?> getVariable, bool stdoutIsTerminal)
    {
        ArgumentNullException.ThrowIfNull(getVariable);
        return new CliEnvironment(!string.IsNullOrEmpty(getVariable("NO_COLOR")), stdoutIsTerminal);
    }
}
