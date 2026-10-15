using Microsoft.Extensions.Logging;
using Shelf.Cli.Logging;
using Xunit;

namespace Shelf.Cli.Tests.Logging;

public sealed class LoggingTests
{
    [Theory]
    [InlineData(LogLevel.Trace, "trace")]
    [InlineData(LogLevel.Debug, "debug")]
    [InlineData(LogLevel.Information, "information")]
    [InlineData(LogLevel.Warning, "warning")]
    [InlineData(LogLevel.Error, "error")]
    [InlineData(LogLevel.Critical, "critical")]
    public void Formatter_uses_lower_case_level_names(LogLevel level, string name) =>
        Assert.Equal($"[{name}] Cat: msg", LogLineFormatter.Format(level, "Cat", "msg", null));

    [Fact]
    public void Formatter_appends_the_exception_and_keeps_newlines()
    {
        var exception = new InvalidOperationException("boom");
        Assert.Equal(
            "[error] Cat: a\nb" + Environment.NewLine + exception,
            LogLineFormatter.Format(LogLevel.Error, "Cat", "a\nb", exception));
    }

    [Fact]
    public void Formatter_returns_null_for_an_empty_message_without_exception() =>
        Assert.Null(LogLineFormatter.Format(LogLevel.Error, "Cat", string.Empty, null));

    [Fact]
    public void Logger_enables_every_level_except_none_and_has_no_scopes()
    {
        var logger = new StderrLoggerProvider(new StringWriter()).CreateLogger("A");
        Assert.False(logger.IsEnabled(LogLevel.None));
        Assert.True(logger.IsEnabled(LogLevel.Trace));
        Assert.True(logger.IsEnabled(LogLevel.Critical));
        Assert.Null(logger.BeginScope("x"));
    }

    [Fact]
    public void Logger_passes_state_and_exception_to_the_formatter()
    {
        var writer = new StringWriter();
        var logger = new StderrLoggerProvider(writer).CreateLogger("A");
        var exception = new InvalidOperationException("x");
        Exception? seen = null;
        logger.Log(LogLevel.Warning, default, 42, exception, (state, ex) =>
        {
            seen = ex;
            return $"state {state}";
        });
        Assert.Same(exception, seen);
        Assert.StartsWith("[warning] A: state 42" + Environment.NewLine, writer.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Logger_swallows_IOException_and_ObjectDisposedException()
    {
        foreach (Exception failure in new Exception[] { new IOException(), new ObjectDisposedException("w") })
        {
            var logger = new StderrLoggerProvider(new ThrowingWriter(failure)).CreateLogger("A");
            logger.LogError("m");
        }
    }

    [Fact]
    public void Provider_caches_loggers_and_does_not_dispose_the_writer()
    {
        var writer = new StringWriter();
        var provider = new StderrLoggerProvider(writer);
        Assert.Same(provider.CreateLogger("A"), provider.CreateLogger("A"));
        provider.Dispose();
        writer.WriteLine("still open");
        Assert.Contains("still open", writer.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Factory_filters_below_the_minimum_level()
    {
        var writer = new StringWriter();
        using var provider = new StderrLoggerProvider(writer);
        using var factory = new LoggerFactory([provider], new LoggerFilterOptions { MinLevel = LogLevel.Information });
        var logger = factory.CreateLogger("C");
        logger.LogDebug("hidden");
        logger.LogInformation("shown");
        Assert.Equal("[information] C: shown" + Environment.NewLine, writer.ToString());
    }

    [Fact]
    public void Concurrent_entries_never_interleave()
    {
        var writer = new StringWriter();
        using var provider = new StderrLoggerProvider(writer);
        var loggers = new[] { provider.CreateLogger("A"), provider.CreateLogger("B") };
        Parallel.For(0, 8, new ParallelOptions { MaxDegreeOfParallelism = 8 }, thread =>
        {
            var logger = loggers[thread % 2];
            for (var i = 0; i < 500; i++)
            {
                logger.LogDebug("{Number}", i);
            }
        });

        var lines = writer.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(4000, lines.Length);
        Assert.All(lines, line => Assert.Matches(@"^\[debug\] (A|B): \d+$", line));
    }

    [Theory]
    [InlineData("--version")]
    [InlineData("--version --json")]
    public void Standard_output_is_identical_regardless_of_log_level(string arguments)
    {
        var args = arguments.Split(' ');
        string Run(LogLevel level)
        {
            var stdout = new StringWriter();
            ShelfApp.Run(args, stdout, new StringWriter(), CliEnvironment.Empty.WithMinimumLogLevel(level));
            return stdout.ToString();
        }

        Assert.Equal(Run(LogLevel.Warning), Run(LogLevel.Trace));
    }

    private sealed class ThrowingWriter(Exception failure) : StringWriter
    {
        public override void WriteLine(string? value) => throw failure;
    }
}
