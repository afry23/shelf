using System.Reflection;

namespace Shelf.Cli;

/// <summary>
/// Temporary argument handling. It will be replaced by the chosen CLI framework;
/// the exit codes and stream rules (PRD 7.1) stay.
/// </summary>
public static class ShelfApp
{
    private const int ExitSuccess = 0;
    private const int ExitUsageError = 2;

    private const string Usage = "Usage: shelf <command> [options]";

    public static int Run(string[] args, TextWriter stdout, TextWriter stderr)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(stdout);
        ArgumentNullException.ThrowIfNull(stderr);

        if (args.Length == 0)
        {
            stderr.WriteLine(Usage);
            return ExitUsageError;
        }

        var first = args[0];
        switch (first)
        {
            case "--version":
                stdout.WriteLine($"shelf {GetVersion()}");
                return ExitSuccess;
            case "--help":
                stdout.WriteLine(Usage);
                return ExitSuccess;
        }

        stderr.WriteLine(first.StartsWith('-')
            ? $"Unknown option '{first}'"
            : $"Unknown command '{first}'");
        stderr.WriteLine(Usage);
        return ExitUsageError;
    }

    private static string GetVersion()
    {
        var info = typeof(ShelfApp).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (string.IsNullOrEmpty(info))
        {
            return "0.0.0";
        }

        var plus = info.IndexOf('+', StringComparison.Ordinal);
        return plus >= 0 ? info[..plus] : info;
    }
}
