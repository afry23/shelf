using Microsoft.Extensions.Logging;

namespace Shelf.Cli.Logging;

internal sealed class StderrLogger(string category, TextWriter writer, Lock gate) : ILogger
{
    public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        ArgumentNullException.ThrowIfNull(formatter);
        if (!IsEnabled(logLevel))
        {
            return;
        }

        var text = LogLineFormatter.Format(logLevel, category, formatter(state, exception), exception);
        if (text is null)
        {
            return;
        }

        try
        {
            lock (gate)
            {
                writer.WriteLine(text);
            }
        }
        catch (IOException)
        {
            // A closed stderr must not crash shelf or an MCP session.
        }
        catch (ObjectDisposedException)
        {
            // Same as above.
        }
    }
}
