using Shelf.Index;

namespace Shelf.Cli.Commands;

/// <summary>What the commands that read files do about covers: a file that cannot give a cover is a warning, never a failure.</summary>
internal static class CoverReport
{
    public static void Warn(CoverFillResult result, TextWriter stderr)
    {
        foreach (var problem in result.Problems)
        {
            stderr.WriteLine($"warning: {problem.Message}");
        }
    }
}
