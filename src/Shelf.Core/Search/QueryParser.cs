using System.Text.RegularExpressions;
using Shelf.Core.Model;

namespace Shelf.Core.Search;

/// <summary>
/// Reads the search language (F-SRC-3): free words, <c>"phrases"</c>, <c>prefix*</c>, <c>-negation</c>, and the
/// filters <c>author:</c> <c>title:</c> <c>tag:</c> <c>publisher:</c> <c>lang:</c> <c>year:</c> <c>format:</c> <c>status:</c>. Everything else is
/// plain text: characters that mean something to the index (<c>( ) : ^ NEAR OR</c>) are never passed on as syntax.
/// </summary>
public static partial class QueryParser
{
    [GeneratedRegex(@"^(>=|<=|>|<)?([0-9]+)$", RegexOptions.CultureInvariant)]
    private static partial Regex YearComparison();

    [GeneratedRegex(@"^([0-9]+)\.\.([0-9]+)$", RegexOptions.CultureInvariant)]
    private static partial Regex YearRange();

    /// <exception cref="QueryException">A filter has an invalid or missing value.</exception>
    public static SearchQuery Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var include = new List<SearchTerm>();
        var exclude = new List<SearchTerm>();
        var filters = new List<SearchFilter>();

        var i = 0;
        while (true)
        {
            while (i < text.Length && char.IsWhiteSpace(text[i]))
            {
                i++;
            }

            if (i >= text.Length)
            {
                break;
            }

            // A minus directly in front of something negates it; a lone "-" is nothing.
            var negated = false;
            if (text[i] == '-' && i + 1 < text.Length && !char.IsWhiteSpace(text[i + 1]))
            {
                negated = true;
                i++;
            }

            if (text[i] == '"')
            {
                var phrase = ReadQuoted(text, ref i);
                Add(new SearchTerm(phrase.Trim(), Quoted: true, Prefix: false), negated, include, exclude);
                continue;
            }

            var raw = ReadWord(text, ref i);
            if (TryFilter(raw, negated) is { } filter)
            {
                filters.Add(filter);
                continue;
            }

            var prefix = raw.Length > 1 && raw.EndsWith('*');
            Add(new SearchTerm(prefix ? raw[..^1] : raw, Quoted: false, prefix), negated, include, exclude);
        }

        return new SearchQuery(include, exclude, filters);
    }

    private static void Add(SearchTerm term, bool negated, List<SearchTerm> include, List<SearchTerm> exclude)
    {
        if (term.HasContent)
        {
            (negated ? exclude : include).Add(term);
        }
    }

    /// <summary>A quoted text; the closing quote is optional at the end of the input. <paramref name="i"/> ends after it.</summary>
    private static string ReadQuoted(string text, ref int i)
    {
        var start = ++i;
        while (i < text.Length && text[i] != '"')
        {
            i++;
        }

        var result = text[start..i];
        if (i < text.Length)
        {
            i++;
        }

        return result;
    }

    /// <summary>Up to the next white space; a quote right after a colon (<c>author:"A B"</c>) runs to its closing quote.</summary>
    private static string ReadWord(string text, ref int i)
    {
        var start = i;
        while (i < text.Length && !char.IsWhiteSpace(text[i]))
        {
            if (text[i] == '"' && i > start && text[i - 1] == ':')
            {
                i++;
                while (i < text.Length && text[i] != '"')
                {
                    i++;
                }

                if (i < text.Length)
                {
                    i++;
                }

                continue;
            }

            i++;
        }

        return text[start..i];
    }

    private static SearchFilter? TryFilter(string raw, bool negated)
    {
        var colon = raw.IndexOf(':', StringComparison.Ordinal);
        if (colon <= 0)
        {
            return null;
        }

        var field = raw[..colon].ToLowerInvariant();
        var kind = field switch
        {
            "author" => (FieldKind?)FieldKind.Author,
            "title" => FieldKind.Title,
            "tag" => FieldKind.Tag,
            "publisher" => FieldKind.Publisher,
            "lang" => FieldKind.Language,
            "year" => FieldKind.Year,
            "format" => FieldKind.Format,
            "status" => FieldKind.Status,
            "type" => FieldKind.Type,
            "doi" => FieldKind.Doi,
            "arxiv" => FieldKind.Arxiv,
            "venue" => FieldKind.Venue,
            "series" => FieldKind.Series,
            "site" => FieldKind.Site,
            "readinglist" => FieldKind.ReadingList,
            _ => null,
        };
        if (kind is null)
        {
            return null; // "http://x" or "foo:bar" are ordinary text
        }

        var value = raw[(colon + 1)..];
        var quoted = value.StartsWith('"');
        if (quoted)
        {
            value = value[1..];
            if (value.EndsWith('"'))
            {
                value = value[..^1];
            }
        }

        value = value.Trim();
        if (value.Length == 0)
        {
            throw new QueryException($"{field}: needs a value");
        }

        switch (kind)
        {
            case FieldKind.Author or FieldKind.Title or FieldKind.Tag or FieldKind.Publisher:
                var prefix = !quoted && value.Length > 1 && value.EndsWith('*');
                var term = new SearchTerm(prefix ? value[..^1] : value, quoted, prefix);
                return term.HasContent ? new TextFilter(kind.Value, term, negated) : throw new QueryException($"{field}: needs a value");
            case FieldKind.Year:
                return ParseYear(value, negated);
            case FieldKind.Status:
                var status = value.ToLowerInvariant();
                return status is "unread" or "reading" or "read"
                    ? new ValueFilter(FieldKind.Status, status, negated)
                    : throw new QueryException($"status: '{value}' is not one of unread, reading, read");
            case FieldKind.ReadingList:
                var onList = value.ToLowerInvariant();
                return onList is "yes" or "no"
                    ? new ValueFilter(FieldKind.ReadingList, onList, negated)
                    : throw new QueryException($"readinglist: '{value}' is not one of yes, no");
            case FieldKind.Type:
                var type = value.ToLowerInvariant();
                return type is "book" or "paper" or "article"
                    ? new ValueFilter(FieldKind.Type, type, negated)
                    : throw new QueryException($"type: '{value}' is not one of book, paper, article");
            case FieldKind.Doi:
                // "doi:10.1145/x", "doi:https://doi.org/10.1145/x": the DOI itself is what counts.
                return new ValueFilter(FieldKind.Doi, (PaperIds.FindDoi(value) ?? value).ToLowerInvariant(), negated);
            case FieldKind.Arxiv:
                return new ValueFilter(FieldKind.Arxiv, PaperIds.FindArxivId(value) ?? value, negated);
            default:
                return new ValueFilter(kind.Value, value.ToLowerInvariant(), negated);
        }
    }

    private static YearFilter ParseYear(string value, bool negated)
    {
        var comparison = YearComparison().Match(value);
        if (comparison.Success)
        {
            var year = Digits(comparison.Groups[2].Value, value);
            return comparison.Groups[1].Value switch
            {
                ">" => new YearFilter(year + 1, null, negated),
                ">=" => new YearFilter(year, null, negated),
                "<" => new YearFilter(null, year - 1, negated),
                "<=" => new YearFilter(null, year, negated),
                _ => new YearFilter(year, year, negated),
            };
        }

        var range = YearRange().Match(value);
        if (range.Success)
        {
            var from = Digits(range.Groups[1].Value, value);
            var to = Digits(range.Groups[2].Value, value);
            return from <= to ? new YearFilter(from, to, negated) : throw new QueryException($"year: the range {value} is empty");
        }

        throw new QueryException(value is ">" or ">=" or "<" or "<="
            ? $"year: '{value}' is not a year"
            : $"year: '{value}' is not a year, a comparison like >2015 or a range like 2010..2015");
    }

    private static int Digits(string digits, string whole) =>
        digits.Length <= 4 ? int.Parse(digits, System.Globalization.CultureInfo.InvariantCulture) : throw new QueryException($"year: '{whole}' is not a year");
}
