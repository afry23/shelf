using Microsoft.Extensions.Logging;

namespace Shelf.Cli;

/// <summary>
/// The parts of the process environment that the CLI honors. It is passed in explicitly so that
/// <see cref="ShelfApp"/> never reads the process environment or the static console.
/// </summary>
public sealed class CliEnvironment
{
    private const string LogLevelVariable = "SHELF_LOG_LEVEL";

    private CliEnvironment(bool noColor, bool stdoutIsTerminal, LogLevel minimumLogLevel, string? ignoredLogLevel = null)
    {
        NoColor = noColor;
        StdoutIsTerminal = stdoutIsTerminal;
        MinimumLogLevel = minimumLogLevel;
        IgnoredLogLevel = ignoredLogLevel;
    }

    /// <summary>No <c>NO_COLOR</c> and standard output is not a terminal.</summary>
    public static CliEnvironment Empty { get; } = new(noColor: false, stdoutIsTerminal: false, LogLevel.Warning);

    /// <summary>True when <c>NO_COLOR</c> is present and non-empty (no-color.org).</summary>
    public bool NoColor { get; }

    public bool StdoutIsTerminal { get; }

    /// <summary>
    /// Minimum level of log entries written to standard error. Set with the SHELF_LOG_LEVEL variable
    /// (a <see cref="LogLevel"/> name, case-insensitive) or programmatically with <see cref="WithMinimumLogLevel"/>.
    /// </summary>
    public LogLevel MinimumLogLevel { get; }

    /// <summary>The SHELF_LOG_LEVEL value that is not a log level name, or null. The CLI reports it and keeps the default.</summary>
    public string? IgnoredLogLevel { get; }

    /// <summary>Colored output is allowed only on a terminal and without <c>NO_COLOR</c>.</summary>
    public bool UseColor => StdoutIsTerminal && !NoColor;

    public static CliEnvironment FromVariables(Func<string, string?> getVariable, bool stdoutIsTerminal)
    {
        ArgumentNullException.ThrowIfNull(getVariable);
        var noColor = !string.IsNullOrEmpty(getVariable("NO_COLOR"));
        var level = LogLevel.Warning;
        string? ignored = null;
        var value = getVariable(LogLevelVariable);
        if (!string.IsNullOrEmpty(value))
        {
            // Names only: Enum.TryParse would also accept numbers like "3".
            if (Array.Exists(Enum.GetNames<LogLevel>(), n => n.Equals(value, StringComparison.OrdinalIgnoreCase)))
            {
                level = Enum.Parse<LogLevel>(value, ignoreCase: true);
            }
            else
            {
                ignored = value;
            }
        }

        return new CliEnvironment(noColor, stdoutIsTerminal, level, ignored);
    }

    public CliEnvironment WithMinimumLogLevel(LogLevel level)
    {
        ArgumentOutOfRangeException.ThrowIfNegative((int)level);
        if (level > LogLevel.None)
        {
            throw new ArgumentOutOfRangeException(nameof(level), level, "Not a defined log level.");
        }

        return new CliEnvironment(NoColor, StdoutIsTerminal, level, IgnoredLogLevel);
    }
}
