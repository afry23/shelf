using Microsoft.Extensions.Logging;

namespace Proto;

/// <summary>Minimal logger provider that writes to stderr only (as in experiments/mcp).</summary>
internal sealed class StderrLoggerProvider : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName) => new StderrLogger(categoryName);

    public void Dispose()
    {
    }

    private sealed class StderrLogger(string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
                return;
            Console.Error.WriteLine($"{logLevel} {category}: {formatter(state, exception)}");
            if (exception is not null)
                Console.Error.WriteLine(exception);
        }
    }
}
