namespace Shelf.Cli;

/// <summary>Translates framework parse results into shelf's own error texts.</summary>
internal static class ParseErrorMessages
{
    /// <summary>Message for the first unmatched token, or null when there is none.</summary>
    public static string? Describe(IReadOnlyList<string> unmatchedTokens)
    {
        if (unmatchedTokens.Count == 0)
        {
            return null;
        }

        var token = unmatchedTokens[0];
        return token.StartsWith('-')
            ? $"Unknown option '{token}'"
            : $"Unknown command '{token}'";
    }
}
