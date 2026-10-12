using System.CommandLine;

namespace Shelf.Cli;

/// <summary>Options that apply to every command (PRD 7.1).</summary>
internal static class GlobalOptions
{
    /// <summary>Creates the recursive <c>--json</c> option. One instance per command tree.</summary>
    public static Option<bool> CreateJson() => new("--json")
    {
        Description = "Write machine-readable JSON to standard output",
        Recursive = true,
    };
}
