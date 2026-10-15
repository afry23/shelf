using Microsoft.Extensions.Logging;

namespace Shelf.Cli.Logging;

internal static partial class ShelfAppLog
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Debug, Message = "Starting shelf {Version}")]
    public static partial void Starting(ILogger logger, string version);

    [LoggerMessage(EventId = 2, Level = LogLevel.Trace, Message = "Parsed command line: {TokenCount} token(s), {ErrorCount} error(s)")]
    public static partial void Parsed(ILogger logger, int tokenCount, int errorCount);

    [LoggerMessage(EventId = 3, Level = LogLevel.Debug, Message = "Exiting with code {ExitCode}")]
    public static partial void Exiting(ILogger logger, int exitCode);
}
