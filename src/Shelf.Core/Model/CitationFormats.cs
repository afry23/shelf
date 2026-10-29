using System.Globalization;
using System.Text;
using System.Text.Json;
using Shelf.Core.Grouping;

namespace Shelf.Core.Model;

/// <summary>The formats a citation can be exported in (F-PAP-5).</summary>
public enum CitationFormat
{
    BibTeX,
    CslJson,
    Ris,
}

/// <summary>Renders works as BibTeX, CSL-JSON or RIS (F-PAP-5). Every work needs a cite key; the caller gives one first.</summary>
public static class CitationFormats
{
    private static readonly string[] ConferenceWords = ["conference", "proceedings", "symposium", "workshop", "congress"];

    // Conference series that are known by an acronym that is not all upper case.
    private static readonly HashSet<string> ConferenceNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "NeurIPS", "NIPS", "ICML", "ICLR", "CVPR", "ICCV", "ECCV", "ACL", "EMNLP", "NAACL", "AAAI", "IJCAI", "KDD",
        "SIGIR", "SIGMOD", "VLDB", "WWW", "CHI", "SOSP", "OSDI", "NSDI", "USENIX", "ISCA", "MICRO", "PLDI", "POPL",
    };

    public static bool TryParse(string name, out CitationFormat format)
    {
        switch (name.ToLowerInvariant())
        {
            case "bibtex" or "bib":
                format = CitationFormat.BibTeX;
                return true;
            case "csl-json" or "csljson" or "csl":
                format = CitationFormat.CslJson;
                return true;
            case "ris":
                format = CitationFormat.Ris;
                return true;
            default:
                format = default;
                return false;
        }
    }

    /// <summary>The works in the format, in the given order; text ends with a newline.</summary>
    /// <exception cref="InvalidOperationException">A work has no cite key.</exception>
    public static string Render(CitationFormat format, IEnumerable<WorkSidecar> works)
    {
        ArgumentNullException.ThrowIfNull(works);
        var list = works.ToList();
        foreach (var work in list.Where(w => string.IsNullOrWhiteSpace(w.CiteKey)))
        {
            throw new InvalidOperationException($"'{work.Title}' has no cite key");
        }

        return format switch
        {
            CitationFormat.BibTeX => string.Concat(list.Select(BibTeX)),
            CitationFormat.CslJson => CslJson(list),
            _ => string.Concat(list.Select(Ris)),
        };
    }

    // ---- entry types ----

    private enum EntryType
    {
        Book,
        Article,
        InProceedings,
        Misc,
    }

    private static EntryType TypeOf(WorkSidecar work)
    {
        if (work.Kind == WorkKind.Book)
        {
            return EntryType.Book;
        }

        if (IsConference(work.Venue))
        {
            return EntryType.InProceedings;
        }

        // A preprint without a journal is cited as such; a paper that has nothing at all is an article.
        return work.Venue is null && work.ArxivId is not null ? EntryType.Misc : EntryType.Article;
    }

    private static bool IsConference(string? venue)
    {
        if (string.IsNullOrWhiteSpace(venue))
        {
            return false;
        }

        if (ConferenceWords.Any(w => venue.Contains(w, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        var tokens = venue.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return tokens.Any(t => ConferenceNames.Contains(t.Trim(',', '.', '\'', '(', ')')))
            || (tokens.Length == 1 && venue.Length >= 3 && venue.All(c => char.IsUpper(c) || char.IsDigit(c)));
    }

    // ---- BibTeX ----

    private static string BibTeX(WorkSidecar work)
    {
        var type = TypeOf(work);
        var text = new StringBuilder();
        text.Append('@').Append(type switch
        {
            EntryType.Book => "book",
            EntryType.InProceedings => "inproceedings",
            EntryType.Misc => "misc",
            _ => "article",
        }).Append('{').Append(work.CiteKey).Append(",\n");

        Field(text, "title", FullTitle(work));
        Field(text, "author", string.Join(" and ", work.Authors.Select(a => AuthorNames.Parse(a).SortKey)));
        Field(text, "year", work.Year?.ToString(CultureInfo.InvariantCulture));
        switch (type)
        {
            case EntryType.Article:
                Field(text, "journal", work.Venue);
                break;
            case EntryType.InProceedings:
                Field(text, "booktitle", work.Venue);
                break;
            case EntryType.Book:
                Field(text, "publisher", work.Publisher);
                Field(text, "edition", work.Edition);
                Field(text, "isbn", work.Isbn13 ?? work.Isbn10);
                break;
        }

        Field(text, "volume", work.Volume);
        Field(text, "number", work.Issue);
        Field(text, "pages", work.Pages?.Replace("-", "--", StringComparison.Ordinal).Replace("----", "--", StringComparison.Ordinal));
        Field(text, "doi", work.Doi);
        if (work.ArxivId is not null)
        {
            Field(text, "eprint", work.ArxivId);
            Field(text, "archiveprefix", "arXiv");
        }

        Field(text, "abstract", work.Abstract);
        Field(text, "keywords", work.Keywords.Count > 0 ? string.Join(", ", work.Keywords) : null);
        Field(text, "language", work.Language);
        text.Append("}\n\n");
        return text.ToString();
    }

    private static void Field(StringBuilder text, string name, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            text.Append("  ").Append(name).Append(" = {").Append(Escape(value)).Append("},\n");
        }
    }

    private static string Escape(string value)
    {
        var text = new StringBuilder(value.Length);
        foreach (var c in value.ReplaceLineEndings(" "))
        {
            if (c is '&' or '%' or '$' or '#' or '_')
            {
                text.Append('\\');
            }

            text.Append(c);
        }

        return text.ToString();
    }

    private static string FullTitle(WorkSidecar work) =>
        string.IsNullOrWhiteSpace(work.Subtitle) ? work.Title : work.Title + ": " + work.Subtitle;

    // ---- CSL-JSON ----

    private static string CslJson(List<WorkSidecar> works)
    {
        using var buffer = new MemoryStream();
        using (var w = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = true, NewLine = "\n", Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
        {
            w.WriteStartArray();
            foreach (var work in works)
            {
                w.WriteStartObject();
                w.WriteString("id", work.CiteKey);
                w.WriteString("type", TypeOf(work) switch
                {
                    EntryType.Book => "book",
                    EntryType.InProceedings => "paper-conference",
                    EntryType.Misc => "article",
                    _ => "article-journal",
                });
                w.WriteString("title", FullTitle(work));
                w.WriteStartArray("author");
                foreach (var author in work.Authors)
                {
                    var sort = AuthorNames.Parse(author).SortKey;
                    var comma = sort.IndexOf(", ", StringComparison.Ordinal);
                    w.WriteStartObject();
                    if (comma < 0)
                    {
                        w.WriteString("literal", sort);
                    }
                    else
                    {
                        w.WriteString("family", sort[..comma]);
                        w.WriteString("given", sort[(comma + 2)..]);
                    }

                    w.WriteEndObject();
                }

                w.WriteEndArray();
                if (work.Year is { } year)
                {
                    w.WriteStartObject("issued");
                    w.WriteStartArray("date-parts");
                    w.WriteStartArray();
                    w.WriteNumberValue(year);
                    w.WriteEndArray();
                    w.WriteEndArray();
                    w.WriteEndObject();
                }

                Optional(w, "container-title", work.Venue);
                Optional(w, "publisher", work.Publisher);
                Optional(w, "volume", work.Volume);
                Optional(w, "issue", work.Issue);
                Optional(w, "page", work.Pages);
                Optional(w, "DOI", work.Doi);
                Optional(w, "ISBN", work.Isbn13 ?? work.Isbn10);
                Optional(w, "abstract", work.Abstract);
                Optional(w, "language", work.Language);
                w.WriteEndObject();
            }

            w.WriteEndArray();
        }

        return Encoding.UTF8.GetString(buffer.GetBuffer(), 0, (int)buffer.Length) + "\n";
    }

    private static void Optional(Utf8JsonWriter writer, string name, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            writer.WriteString(name, value);
        }
    }

    // ---- RIS ----

    private static string Ris(WorkSidecar work)
    {
        var text = new StringBuilder();
        Tag(text, "TY", TypeOf(work) switch
        {
            EntryType.Book => "BOOK",
            EntryType.InProceedings => "CPAPER",
            EntryType.Misc => "GEN",
            _ => "JOUR",
        });
        Tag(text, "ID", work.CiteKey);
        Tag(text, "TI", FullTitle(work));
        foreach (var author in work.Authors)
        {
            Tag(text, "AU", AuthorNames.Parse(author).SortKey);
        }

        Tag(text, "PY", work.Year?.ToString(CultureInfo.InvariantCulture));
        Tag(text, "T2", work.Venue);
        Tag(text, "PB", work.Publisher);
        Tag(text, "VL", work.Volume);
        Tag(text, "IS", work.Issue);
        if (work.Pages is { } pages)
        {
            var dash = pages.IndexOfAny(['-', '–']);
            Tag(text, "SP", dash < 0 ? pages : pages[..dash]);
            Tag(text, "EP", dash < 0 ? null : pages[(dash + 1)..].TrimStart('-', '–'));
        }

        Tag(text, "DO", work.Doi);
        Tag(text, "SN", work.Isbn13 ?? work.Isbn10);
        Tag(text, "AB", work.Abstract);
        foreach (var keyword in work.Keywords)
        {
            Tag(text, "KW", keyword);
        }

        Tag(text, "LA", work.Language);
        text.Append("ER  - \n\n");
        return text.ToString();
    }

    private static void Tag(StringBuilder text, string tag, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            text.Append(tag).Append("  - ").Append(value.ReplaceLineEndings(" ")).Append('\n');
        }
    }
}
