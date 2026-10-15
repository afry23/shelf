using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Shelf.Cli.Logging;

/// <summary>
/// Writes log entries, one line each, to a given <see cref="TextWriter"/> (standard error in production).
/// Microsoft.Extensions.Logging.Console is not used: it writes to the static console, so it cannot target the
/// injected stderr writer, writes from a background queue (reordering, flushing), pulls in the options
/// infrastructure and may send entries to stdout. This is the only log sink shelf may register: standard
/// output is reserved for command output and the MCP stdio protocol.
/// </summary>
public sealed class StderrLoggerProvider(TextWriter writer) : ILoggerProvider
{
    private readonly TextWriter _writer = writer ?? throw new ArgumentNullException(nameof(writer));
    private readonly ConcurrentDictionary<string, StderrLogger> _loggers = new(StringComparer.Ordinal);
    private readonly Lock _gate = new();

    public ILogger CreateLogger(string categoryName)
    {
        ArgumentNullException.ThrowIfNull(categoryName);
        return _loggers.GetOrAdd(categoryName, name => new StderrLogger(name, _writer, _gate));
    }

    /// <summary>Does not dispose the writer; its owner does.</summary>
    public void Dispose()
    {
    }
}
