using System.Text;

namespace Shelf.Core.Organization;

/// <summary>Makes text safe as one path component on every supported system (F-ORG-3).</summary>
public static class PathSanitizer
{
    public const int MaxBytes = 120;

    /// <summary>
    /// Unicode NFC, no separators or other reserved characters (a colon becomes " -", slashes and bars become "-",
    /// the rest is dropped), no leading or trailing dots and spaces, at most <paramref name="maxBytes"/> UTF-8 bytes
    /// (cut between characters, never inside a surrogate pair). An empty result is "_".
    /// </summary>
    public static string Segment(string name, int maxBytes = MaxBytes)
    {
        name = name.Normalize(NormalizationForm.FormC);
        var builder = new StringBuilder(name.Length);
        for (var i = 0; i < name.Length; i++)
        {
            var c = name[i];
            switch (c)
            {
                case ':':
                    // "Flatland: A Romance" reads as "Flatland - A Romance"; a colon inside a word becomes a hyphen.
                    builder.Append(i + 1 < name.Length && name[i + 1] == ' ' ? " -" : "-");
                    break;
                case '/' or '\\' or '|':
                    builder.Append('-');
                    break;
                case '*' or '?' or '"' or '<' or '>':
                    break;
                default:
                    if (!char.IsControl(c))
                    {
                        builder.Append(c);
                    }

                    break;
            }
        }

        var cleaned = string.Join(' ', builder.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries)).Trim('.', ' ');
        while (Encoding.UTF8.GetByteCount(cleaned) > maxBytes)
        {
            cleaned = cleaned[..^(cleaned.Length > 1 && char.IsLowSurrogate(cleaned[^1]) ? 2 : 1)];
        }

        cleaned = cleaned.TrimEnd('.', ' ');
        return cleaned.Length == 0 ? "_" : cleaned;
    }
}
