using System.Globalization;
using System.Text;

namespace Shelf.Core.Grouping;

/// <summary>Case, accents and ligatures folded away so that texts can be compared (titles, names, running text).</summary>
public static class TextFolding
{
    /// <summary>Lower case, accents removed (NFKD, combining marks dropped), letters without a decomposition spelled out.</summary>
    public static string Fold(string text)
    {
        var builder = new StringBuilder(text.Length);
        foreach (var c in text)
        {
            if (c < 128)
            {
                builder.Append(char.ToLowerInvariant(c));
            }
            else
            {
                AppendFolded(builder, c);
            }
        }

        return builder.ToString();
    }

    public static void AppendFolded(StringBuilder builder, char c)
    {
        switch (char.ToLowerInvariant(c))
        {
            case 'ß': builder.Append("ss"); return;
            case 'æ': builder.Append("ae"); return;
            case 'œ': builder.Append("oe"); return;
            case 'ø': builder.Append('o'); return;
            case 'đ': builder.Append('d'); return;
            case 'ð': builder.Append('d'); return;
            case 'ł': builder.Append('l'); return;
            case 'þ': builder.Append("th"); return;
            case 'ı': builder.Append('i'); return;
        }

        foreach (var part in c.ToString().Normalize(NormalizationForm.FormKD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(part) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(char.ToLowerInvariant(part));
            }
        }
    }
}
