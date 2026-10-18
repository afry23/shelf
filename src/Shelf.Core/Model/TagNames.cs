namespace Shelf.Core.Model;

/// <summary>Tags are free text with '/' as the separator of a hierarchy, e.g. <c>architektur/ddd</c> (F-TAG-1).</summary>
public static class TagNames
{
    public const char Separator = '/';

    /// <summary>Blanks around every part are removed; no part may be empty and no character a control character.</summary>
    /// <param name="error">What is wrong, as the end of a sentence: "tag 'x': &lt;error&gt;".</param>
    public static bool TryNormalize(string? text, out string tag, out string error)
    {
        tag = string.Empty;
        if (string.IsNullOrWhiteSpace(text))
        {
            error = "must not be empty";
            return false;
        }

        if (text.Any(char.IsControl))
        {
            error = "must not contain control characters";
            return false;
        }

        var parts = text.Split(Separator).Select(p => p.Trim()).ToArray();
        if (parts.Any(p => p.Length == 0))
        {
            error = "has an empty part";
            return false;
        }

        tag = string.Join(Separator, parts);
        error = string.Empty;
        return true;
    }

    /// <summary>True for the tag itself and every tag below it: "cloud" covers "cloud" and "cloud/azure", not "cloudy".</summary>
    public static bool IsSameOrBelow(string tag, string ancestor) =>
        tag == ancestor || tag.StartsWith(ancestor + Separator, StringComparison.Ordinal);

    /// <summary>The tag with its first <paramref name="from"/> replaced by <paramref name="to"/>; the tag must be <see cref="IsSameOrBelow"/> of it.</summary>
    public static string Move(string tag, string from, string to) => to + tag[from.Length..];
}
