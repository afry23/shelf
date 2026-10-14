using Microsoft.Extensions.Logging;

namespace Shelf.Cli.Logging;

/// <summary>Formats one log entry as <c>[level] category: message</c>. No timestamp, no color.</summary>
internal static class LogLineFormatter
{
    /// <summary>Returns the line, or null when there is nothing to write (empty message, no exception).</summary>
    public static string? Format(LogLevel level, string category, string message, Exception? exception)
    {
        if (string.IsNullOrEmpty(message) && exception is null)
        {
            return null;
        }

        var name = level switch
        {
            LogLevel.Trace => "trace",
            LogLevel.Debug => "debug",
            LogLevel.Information => "information",
            LogLevel.Warning => "warning",
            LogLevel.Error => "error",
            LogLevel.Critical => "critical",
            _ => "none",
        };

        var line = $"[{name}] {category}: {message}";
        return exception is null ? line : line + Environment.NewLine + exception;
    }
}
