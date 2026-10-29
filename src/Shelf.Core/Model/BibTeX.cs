using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Shelf.Core.Model;

/// <summary>One entry of a BibTeX file. The values are kept as written; <see cref="Text"/> and <see cref="Authors"/> read them.</summary>
/// <param name="Type">The entry type in lower case (<c>article</c>, <c>inproceedings</c>, ...).</param>
public sealed record BibEntry(string Type, string Key, IReadOnlyDictionary<string, string> Fields)
{
    /// <summary>The value of a field with LaTeX removed (accents decoded, braces dropped, white space collapsed), or null.</summary>
    public string? Text(string field)
    {
        if (!Fields.TryGetValue(field, out var raw))
        {
            return null;
        }

        var text = BibTeX.Clean(raw);
        return text.Length == 0 ? null : text;
    }

    /// <summary>The authors as "First Last"; "others" (et al.) is dropped.</summary>
    public IReadOnlyList<string> Authors => Fields.TryGetValue("author", out var raw) ? BibTeX.SplitNames(raw) : [];
}

/// <summary>
/// Reads the BibTeX that Zotero, Mendeley, Google Scholar and LaTeX users write (F-PAP-6): entries in braces or parentheses,
/// values in braces, in quotes, as numbers or as <c>@string</c> macros joined by <c>#</c>. <c>@comment</c> and <c>@preamble</c>
/// are skipped. An entry that is not closed is dropped; the text between entries is ignored.
/// </summary>
public static partial class BibTeX
{
    public static IReadOnlyList<BibEntry> Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var entries = new List<BibEntry>();
        var macros = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var i = 0;
        while ((i = text.IndexOf('@', i)) >= 0)
        {
            i++;
            var start = i;
            while (i < text.Length && char.IsLetter(text[i]))
            {
                i++;
            }

            var type = text[start..i].ToLowerInvariant();
            i = SkipWhite(text, i);
            if (type.Length == 0 || i >= text.Length || text[i] is not ('{' or '('))
            {
                continue;
            }

            var close = text[i] == '{' ? '}' : ')';
            var open = text[i];
            var end = BlockEnd(text, i, open, close);
            if (end < 0)
            {
                return entries; // not closed: nothing after it can be trusted
            }

            var body = text[(i + 1)..end];
            if (type == "string")
            {
                var (name, value) = Assignment(body, macros);
                if (name is not null)
                {
                    macros[name] = Clean(value!);
                }
            }
            else if (type is not ("comment" or "preamble") && ReadEntry(type, body, macros) is { } entry)
            {
                entries.Add(entry);
            }

            i = end + 1;
        }

        return entries;
    }

    /// <summary>Names separated by "and" outside braces, as "First Last".</summary>
    public static IReadOnlyList<string> SplitNames(string raw)
    {
        var names = new List<string>();
        var depth = 0;
        var start = 0;
        for (var i = 0; i < raw.Length; i++)
        {
            switch (raw[i])
            {
                case '{':
                    depth++;
                    break;
                case '}':
                    depth = Math.Max(0, depth - 1);
                    break;
                default:
                    if (depth == 0 && char.IsWhiteSpace(raw[i]) && AndAt(raw, i) is { } length)
                    {
                        names.Add(raw[start..i]);
                        i += length - 1;
                        start = i + 1;
                    }

                    break;
            }
        }

        names.Add(raw[start..]);
        return [.. names.Select(Clean).Where(n => n.Length > 0 && !n.Equals("others", StringComparison.OrdinalIgnoreCase)).Select(FirstLast)];
    }

    /// <summary>LaTeX to plain text: accents and special letters decoded, escapes undone, protective braces dropped.</summary>
    public static string Clean(string raw)
    {
        var text = AccentCommand().Replace(raw, m =>
        {
            var letter = m.Groups[2].Success ? m.Groups[2].Value : m.Groups[3].Value;
            var mark = m.Groups[1].Value switch
            {
                "\"" => '\u0308', "'" => '\u0301', "`" => '\u0300', "^" => '\u0302', "~" => '\u0303', "=" => '\u0304', "." => '\u0307', _ => '\0',
            };
            return (letter + mark).Normalize(NormalizationForm.FormC);
        });
        text = LetterAccentCommand().Replace(text, m =>
        {
            var letter = m.Groups[2].Success ? m.Groups[2].Value : m.Groups[3].Value;
            var mark = m.Groups[1].Value switch { "c" => '\u0327', "v" => '\u030C', _ => '\u0306' };
            return (letter + mark).Normalize(NormalizationForm.FormC);
        });
        text = SpecialLetter().Replace(text, m => m.Groups[1].Value switch
        {
            "ss" => "ß", "o" => "ø", "O" => "Ø", "aa" => "å", "AA" => "Å", "ae" => "æ", "AE" => "Æ", "l" => "ł", "L" => "Ł", "i" => "i", _ => m.Value,
        });
        text = EscapedSign().Replace(text, "$1").Replace('~', ' ');
        text = text.Replace("{", string.Empty, StringComparison.Ordinal).Replace("}", string.Empty, StringComparison.Ordinal);
        return string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    private static BibEntry? ReadEntry(string type, string body, Dictionary<string, string> macros)
    {
        var comma = body.IndexOf(',', StringComparison.Ordinal);
        if (comma <= 0)
        {
            return null;
        }

        var key = body[..comma].Trim();
        if (key.Length == 0 || key.Any(char.IsWhiteSpace))
        {
            return null;
        }

        var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var i = comma + 1;
        while (i < body.Length)
        {
            i = SkipWhite(body, i);
            if (i >= body.Length)
            {
                break;
            }

            if (body[i] == ',')
            {
                i++;
                continue;
            }

            var nameStart = i;
            while (i < body.Length && !char.IsWhiteSpace(body[i]) && body[i] != '=' && body[i] != ',')
            {
                i++;
            }

            var name = body[nameStart..i].ToLowerInvariant();
            i = SkipWhite(body, i);
            if (name.Length == 0 || i >= body.Length || body[i] != '=')
            {
                break;
            }

            i = SkipWhite(body, i + 1);
            var value = new StringBuilder();
            while (i < body.Length)
            {
                value.Append(ReadPart(body, ref i, macros));
                i = SkipWhite(body, i);
                if (i < body.Length && body[i] == '#')
                {
                    i = SkipWhite(body, i + 1);
                    continue;
                }

                break;
            }

            fields[name] = value.ToString();
        }

        return new BibEntry(type, key, fields);
    }

    private static (string? Name, string? Value) Assignment(string body, Dictionary<string, string> macros)
    {
        var equals = body.IndexOf('=', StringComparison.Ordinal);
        if (equals <= 0)
        {
            return (null, null);
        }

        var i = SkipWhite(body, equals + 1);
        var value = new StringBuilder();
        while (i < body.Length)
        {
            value.Append(ReadPart(body, ref i, macros));
            i = SkipWhite(body, i);
            if (i < body.Length && body[i] == '#')
            {
                i = SkipWhite(body, i + 1);
                continue;
            }

            break;
        }

        return (body[..equals].Trim(), value.ToString());
    }

    // One piece of a value: {braced}, "quoted", a number or a macro name.
    private static string ReadPart(string text, ref int i, Dictionary<string, string> macros)
    {
        if (i >= text.Length)
        {
            return string.Empty;
        }

        if (text[i] == '{')
        {
            var end = BlockEnd(text, i, '{', '}');
            if (end < 0)
            {
                end = text.Length;
            }

            var inner = text[(i + 1)..end];
            i = Math.Min(text.Length, end + 1);
            return inner;
        }

        if (text[i] == '"')
        {
            var depth = 0;
            var j = i + 1;
            for (; j < text.Length; j++)
            {
                if (text[j] == '{')
                {
                    depth++;
                }
                else if (text[j] == '}')
                {
                    depth--;
                }
                else if (text[j] == '"' && depth <= 0 && text[j - 1] != '\\')
                {
                    break;
                }
            }

            var inner = text[(i + 1)..Math.Min(j, text.Length)];
            i = Math.Min(text.Length, j + 1);
            return inner;
        }

        var start = i;
        while (i < text.Length && !char.IsWhiteSpace(text[i]) && text[i] is not (',' or '#' or '}'))
        {
            i++;
        }

        var token = text[start..i];
        return macros.TryGetValue(token, out var macro) ? macro : token;
    }

    // The index of the bracket that closes the one at <paramref name="open"/>, or -1.
    private static int BlockEnd(string text, int open, char opening, char closing)
    {
        var depth = 0;
        for (var i = open; i < text.Length; i++)
        {
            if (text[i] == '\\')
            {
                i++;
            }
            else if (text[i] == opening)
            {
                depth++;
            }
            else if (text[i] == closing && --depth == 0)
            {
                return i;
            }
        }

        return -1;
    }

    private static int SkipWhite(string text, int i)
    {
        while (i < text.Length && char.IsWhiteSpace(text[i]))
        {
            i++;
        }

        return i;
    }

    // The length of "<white space>and<white space>" at i, or null.
    private static int? AndAt(string text, int i)
    {
        var j = i;
        while (j < text.Length && char.IsWhiteSpace(text[j]))
        {
            j++;
        }

        if (j + 3 < text.Length && string.Compare(text, j, "and", 0, 3, StringComparison.OrdinalIgnoreCase) == 0 && char.IsWhiteSpace(text[j + 3]))
        {
            return j + 3 - i + 1;
        }

        return null;
    }

    private static string FirstLast(string name)
    {
        var parts = name.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        return parts.Length switch
        {
            2 => $"{parts[1]} {parts[0]}",
            >= 3 => $"{parts[2]} {parts[0]} {parts[1]}",
            _ => name,
        };
    }

    [GeneratedRegex(@"\\([""'`^~=.])\s*(?:\{\s*\\?([A-Za-z])\s*\}|\\?([A-Za-z]))")]
    private static partial Regex AccentCommand();

    [GeneratedRegex(@"\\([cvu])\s*(?:\{\s*([A-Za-z])\s*\}|\s([A-Za-z]))")]
    private static partial Regex LetterAccentCommand();

    [GeneratedRegex(@"\\(ss|aa|AA|ae|AE|o|O|l|L|i)(?![A-Za-z])(?:\{\}|\s)?")]
    private static partial Regex SpecialLetter();

    [GeneratedRegex(@"\\([&%$#_])")]
    private static partial Regex EscapedSign();
}
