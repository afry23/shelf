using Microsoft.Extensions.Logging;

namespace Shelf.Cli;

/// <summary>
/// The parts of the process environment that the CLI honors. It is passed in explicitly so that
/// <see cref="ShelfApp"/> never reads the process environment or the static console.
/// </summary>
public sealed class CliEnvironment
{
    private const string LogLevelVariable = "SHELF_LOG_LEVEL";

    private readonly Func<string, string?> _getVariable;
    private readonly string? _workingDirectory;
    private readonly string? _homeDirectory;
    private readonly TextReader? _standardInput;

    private CliEnvironment(
        bool noColor,
        bool stdoutIsTerminal,
        LogLevel minimumLogLevel,
        string? ignoredLogLevel,
        Func<string, string?> getVariable,
        string? workingDirectory,
        string? homeDirectory,
        IFileOpener? fileOpener = null,
        TextReader? standardInput = null)
    {
        NoColor = noColor;
        StdoutIsTerminal = stdoutIsTerminal;
        MinimumLogLevel = minimumLogLevel;
        IgnoredLogLevel = ignoredLogLevel;
        _getVariable = getVariable;
        _workingDirectory = workingDirectory;
        _homeDirectory = homeDirectory;
        FileOpener = fileOpener ?? SystemFileOpener.Instance;
        _standardInput = standardInput;
    }

    /// <summary>No <c>NO_COLOR</c> and standard output is not a terminal.</summary>
    public static CliEnvironment Empty { get; } = new(noColor: false, stdoutIsTerminal: false, LogLevel.Warning, null, _ => null, null, null);

    /// <summary>True when <c>NO_COLOR</c> is present and non-empty (no-color.org).</summary>
    public bool NoColor { get; }

    public bool StdoutIsTerminal { get; }

    /// <summary>Where interactive commands read the answers from; the process's standard input unless the host set another.</summary>
    public TextReader StandardInput => _standardInput ?? Console.In;

    /// <summary>What <c>shelf open</c> uses to start the application for a file; the system's by default.</summary>
    public IFileOpener FileOpener { get; }

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

        return new CliEnvironment(noColor, stdoutIsTerminal, level, ignored, getVariable, null, null);
    }

    /// <summary>The value of a process environment variable, or null.</summary>
    public string? GetVariable(string name) => _getVariable(name);

    /// <summary>The directory commands resolve relative paths against. Set by the host; a command that needs it fails if it is missing.</summary>
    public string WorkingDirectory =>
        _workingDirectory ?? throw new InvalidOperationException("The working directory is not known.");

    /// <summary>The home directory, used to find the global config file.</summary>
    public string HomeDirectory =>
        _homeDirectory ?? throw new InvalidOperationException("The home directory is not known.");

    public CliEnvironment WithDirectories(string workingDirectory, string homeDirectory)
    {
        ArgumentException.ThrowIfNullOrEmpty(workingDirectory);
        ArgumentException.ThrowIfNullOrEmpty(homeDirectory);
        return new CliEnvironment(NoColor, StdoutIsTerminal, MinimumLogLevel, IgnoredLogLevel, _getVariable, workingDirectory, homeDirectory, FileOpener, _standardInput);
    }

    public CliEnvironment WithMinimumLogLevel(LogLevel level)
    {
        ArgumentOutOfRangeException.ThrowIfNegative((int)level);
        if (level > LogLevel.None)
        {
            throw new ArgumentOutOfRangeException(nameof(level), level, "Not a defined log level.");
        }

        return new CliEnvironment(NoColor, StdoutIsTerminal, level, IgnoredLogLevel, _getVariable, _workingDirectory, _homeDirectory, FileOpener, _standardInput);
    }

    public CliEnvironment WithFileOpener(IFileOpener fileOpener)
    {
        ArgumentNullException.ThrowIfNull(fileOpener);
        return new CliEnvironment(NoColor, StdoutIsTerminal, MinimumLogLevel, IgnoredLogLevel, _getVariable, _workingDirectory, _homeDirectory, fileOpener, _standardInput);
    }

    public CliEnvironment WithStandardInput(TextReader standardInput)
    {
        ArgumentNullException.ThrowIfNull(standardInput);
        return new CliEnvironment(NoColor, StdoutIsTerminal, MinimumLogLevel, IgnoredLogLevel, _getVariable, _workingDirectory, _homeDirectory, FileOpener, standardInput);
    }
}
