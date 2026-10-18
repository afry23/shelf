namespace Shelf.Library;

/// <summary>
/// The ID of a file over HTTP: the ID of its work, a tilde and its name in the sidecar (<c>01J9Z3K0...~kp.epub</c>). It is
/// the key that the sidecar uses, it survives <c>reindex</c> (the numbers of the index do not), and a tilde never occurs in
/// an ID of a work, so the first one splits.
/// </summary>
public static class FileIds
{
    public const char Separator = '~';

    public static string Make(string work, string name) => work + Separator + name;

    /// <summary>Splits an ID; false when there is no work part or no name.</summary>
    public static bool TryParse(string? id, out string work, out string name)
    {
        work = name = string.Empty;
        var at = id?.IndexOf(Separator, StringComparison.Ordinal) ?? -1;
        if (id is null || at < 1 || at == id.Length - 1)
        {
            return false;
        }

        work = id[..at];
        name = id[(at + 1)..];
        return true;
    }
}
