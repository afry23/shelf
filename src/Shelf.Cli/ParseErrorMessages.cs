namespace Shelf.Cli;

/// <summary>Translates framework parse results into shelf's own error texts.</summary>
internal static class ParseErrorMessages
{
    /// <summary>
    /// Message for the first unmatched token, or null when there is none. At the top level a word is
    /// an unknown command; below a command it is an extra argument.
    /// </summary>
    public static string? Describe(IReadOnlyList<string> unmatchedTokens, bool atRoot = true)
    {
        if (unmatchedTokens.Count == 0)
        {
            return null;
        }

        var token = unmatchedTokens[0];
        if (token.StartsWith('-'))
        {
            return $"Unknown option '{token}'";
        }

        return atRoot ? $"Unknown command '{token}'" : $"Unexpected argument '{token}'";
    }
}
