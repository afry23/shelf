namespace Shelf.Clip;

/// <summary>
/// The YAML front matter of a Markdown file, as web clippers write it (F-ART-3). Only the subset they use is read:
/// <c>key: value</c> lines, quoted or plain, inline lists <c>[a, b]</c> and block lists (<c>- a</c>). Anything deeper is ignored.
/// </summary>
public static class FrontMatter
{
    /// <summary>Splits the file into its fields and the text below the front matter. Without front matter the fields are empty.</summary>
    public static (IReadOnlyDictionary<string, IReadOnlyList<string>> Fields, string Body) Split(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var content = text.TrimStart('﻿').Replace("\r\n", "\n", StringComparison.Ordinal);
        var empty = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
        if (!content.StartsWith("---\n", StringComparison.Ordinal))
        {
            return (empty, content);
        }

        var lines = content.Split('\n');
        var end = Array.FindIndex(lines, 1, l => l.TrimEnd() is "---" or "...");
        if (end < 0)
        {
            return (empty, content);
        }

        var fields = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
        List<string>? list = null;
        for (var i = 1; i < end; i++)
        {
            var line = lines[i];
            var trimmed = line.TrimStart();
            if (trimmed.StartsWith("- ", StringComparison.Ordinal) || trimmed == "-")
            {
                if (list is not null)
                {
                    var item = Unquote(trimmed.Length > 1 ? trimmed[2..] : string.Empty);
                    if (item.Length > 0)
                    {
                        list.Add(item);
                    }
                }

                continue;
            }

            var colon = line.IndexOf(':', StringComparison.Ordinal);
            if (char.IsWhiteSpace(line, 0) || colon <= 0 || line.StartsWith('#'))
            {
                continue; // nested or foreign structure
            }

            var key = line[..colon].Trim();
            var value = line[(colon + 1)..].Trim();
            list = [];
            fields[key] = list;
            if (value.StartsWith('[') && value.EndsWith(']'))
            {
                list.AddRange(SplitInline(value[1..^1]));
            }
            else if (value.Length > 0 && value is not ("|" or ">" or "|-" or ">-"))
            {
                list.Add(Unquote(value));
            }
        }

        var body = string.Join('\n', lines[(end + 1)..]).TrimStart('\n');
        return (fields, body);
    }

    private static IEnumerable<string> SplitInline(string inner)
    {
        var item = new System.Text.StringBuilder();
        char? quote = null;
        foreach (var c in inner)
        {
            if (quote is null && c is '"' or '\'')
            {
                quote = c;
            }
            else if (c == quote)
            {
                quote = null;
            }
            else if (c == ',' && quote is null)
            {
                if (Unquote(item.ToString()) is { Length: > 0 } done)
                {
                    yield return done;
                }

                item.Clear();
                continue;
            }

            item.Append(c);
        }

        if (Unquote(item.ToString()) is { Length: > 0 } last)
        {
            yield return last;
        }
    }

    /// <summary>Removes the quotes of a YAML scalar and a clipper's <c>[[wikilink]]</c> brackets.</summary>
    public static string Unquote(string value)
    {
        var text = value.Trim();
        if (text.Length >= 2 && ((text[0] == '"' && text[^1] == '"') || (text[0] == '\'' && text[^1] == '\'')))
        {
            text = text[1..^1];
            text = text.Replace("\\\"", "\"", StringComparison.Ordinal).Replace("''", "'", StringComparison.Ordinal);
        }

        text = text.Trim();
        if (text.StartsWith("[[", StringComparison.Ordinal) && text.EndsWith("]]", StringComparison.Ordinal))
        {
            text = text[2..^2];
            var bar = text.IndexOf('|', StringComparison.Ordinal);
            text = (bar >= 0 ? text[(bar + 1)..] : text).Trim();
        }

        return text;
    }
}
