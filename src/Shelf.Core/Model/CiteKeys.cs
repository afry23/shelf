namespace Shelf.Core.Model;

/// <summary>
/// The key under which a work is cited. It must survive as a BibTeX key, so it has no spaces, commas, braces or
/// other characters that BibTeX and its relatives treat as syntax.
/// </summary>
public static class CiteKeys
{
    public const string Rule = "may only contain letters, digits and - _ : . + /";

    public static bool IsValid(string key) =>
        key.Length > 0 && key.All(c => char.IsLetterOrDigit(c) || c is '-' or '_' or ':' or '.' or '+' or '/');
}
