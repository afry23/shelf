using Microsoft.Extensions.Logging;

namespace Shelf.Cli;

/// <summary>
/// The parts of the process environment that the CLI honors. It is passed in explicitly so that
/// <see cref="ShelfApp"/> never reads the process environment or the static console.
/// </summary>
public sealed class CliEnvironment
{
    private CliEnvironment(bool noColor, bool stdoutIsTerminal, LogLevel minimumLogLevel)
    {
        NoColor = noColor;
        StdoutIsTerminal = stdoutIsTerminal;
        MinimumLogLevel = minimumLogLevel;
    }

    /// <summary>No <c>NO_COLOR</c> and standard output is not a terminal.</summary>
    public static CliEnvironment Empty { get; } = new(noColor: false, stdoutIsTerminal: false, LogLevel.Warning);

    /// <summary>True when <c>NO_COLOR</c> is present and non-empty (no-color.org).</summary>
    public bool NoColor { get; }

    public bool StdoutIsTerminal { get; }

    /// <summary>
    /// Minimum level of log entries written to standard error. Internal control for specs and tests;
    /// there is no user-facing option yet (M1 adds SHELF_LOG_LEVEL).
    /// </summary>
    public LogLevel MinimumLogLevel { get; }

    /// <summary>Colored output is allowed only on a terminal and without <c>NO_COLOR</c>.</summary>
    public bool UseColor => StdoutIsTerminal && !NoColor;

    public static CliEnvironment FromVariables(Func<string, string?> getVariable, bool stdoutIsTerminal)
    {
        ArgumentNullException.ThrowIfNull(getVariable);
        return new CliEnvironment(!string.IsNullOrEmpty(getVariable("NO_COLOR")), stdoutIsTerminal, LogLevel.Warning);
    }

    public CliEnvironment WithMinimumLogLevel(LogLevel level)
    {
        ArgumentOutOfRangeException.ThrowIfNegative((int)level);
        if (level > LogLevel.None)
        {
            throw new ArgumentOutOfRangeException(nameof(level), level, "Not a defined log level.");
        }

        return new CliEnvironment(NoColor, StdoutIsTerminal, level);
    }
}
