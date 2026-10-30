using System.Globalization;

namespace Shelf.Core.Search;

/// <summary>The search syntax cannot be understood (an invalid value of a filter, ...). A usage error, not a failure.</summary>
public sealed class QueryException(string message) : Exception(message);

/// <summary>The fields a search can be filtered by (F-SRC-3).</summary>
public enum FieldKind
{
    Author,
    Title,
    Tag,
    Publisher,
    Language,
    Year,
    Format,
    Status,
    Type,
    Doi,
    Arxiv,
    Venue,
    Series,
    Site,
    ReadingList,
}

/// <summary>A word, a quoted phrase, or a word with a prefix star. Always compared as whole words after folding case and accents.</summary>
public sealed record SearchTerm(string Text, bool Quoted, bool Prefix)
{
    /// <summary>False for text without any letter or digit (<c>...</c>): there is nothing to look for.</summary>
    public bool HasContent => Text.Any(char.IsLetterOrDigit);

    public string Describe() => Quoted ? $"\"{Text}\"" : Text + (Prefix ? "*" : string.Empty);
}

/// <summary>One condition on the works, optionally negated (<c>-tag:x</c>).</summary>
public abstract record SearchFilter(FieldKind Field, bool Negated)
{
    public abstract string Describe();

    protected string Name => Field switch
    {
        FieldKind.Language => "lang",
        var other => other.ToString().ToLowerInvariant(),
    };
}

/// <summary><c>author:</c>, <c>title:</c>, <c>tag:</c> and <c>publisher:</c>: words of that field in the index.</summary>
public sealed record TextFilter(FieldKind Field, SearchTerm Term, bool Negated) : SearchFilter(Field, Negated)
{
    public override string Describe() => (Negated ? "-" : string.Empty) + Name + ":" + Term.Describe();
}

/// <summary><c>lang:</c>, <c>format:</c> and <c>status:</c>: the exact value, lower case.</summary>
public sealed record ValueFilter(FieldKind Field, string Value, bool Negated) : SearchFilter(Field, Negated)
{
    public override string Describe() => (Negated ? "-" : string.Empty) + Name + ":" + Value;
}

/// <summary><c>year:</c>: an inclusive range; one side may be open. <c>&gt;2015</c> is stored as a minimum of 2016.</summary>
public sealed record YearFilter(int? Min, int? Max, bool Negated) : SearchFilter(FieldKind.Year, Negated)
{
    public override string Describe()
    {
        var range = (Min, Max) switch
        {
            ({ } a, { } b) when a == b => a.ToString(CultureInfo.InvariantCulture),
            ({ } a, { } b) => $"{a}..{b}",
            ({ } a, null) => $">={a}",
            (null, { } b) => $"<={b}",
            _ => string.Empty,
        };
        return (Negated ? "-" : string.Empty) + "year:" + range;
    }
}

/// <summary>
/// A parsed search: words to find, words that must not occur, and filters (F-SRC-3). The same language is used
/// by the CLI, the web UI and MCP.
/// </summary>
public sealed record SearchQuery(IReadOnlyList<SearchTerm> Include, IReadOnlyList<SearchTerm> Exclude, IReadOnlyList<SearchFilter> Filters)
{
    public bool IsEmpty => Include.Count == 0 && Exclude.Count == 0 && Filters.Count == 0;

    /// <summary>A one-line description, as in <c>include: kubernetes; exclude: azure; filters: tag:cloud, year:&gt;=2017</c>.</summary>
    public string Describe()
    {
        var parts = new List<string>();
        if (Include.Count > 0)
        {
            parts.Add("include: " + string.Join(", ", Include.Select(t => t.Describe())));
        }

        if (Exclude.Count > 0)
        {
            parts.Add("exclude: " + string.Join(", ", Exclude.Select(t => t.Describe())));
        }

        if (Filters.Count > 0)
        {
            parts.Add("filters: " + string.Join(", ", Filters.Select(f => f.Describe())));
        }

        return parts.Count == 0 ? "(empty)" : string.Join("; ", parts);
    }
}
