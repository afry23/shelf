using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using Shelf.Core.Model;
using VersOne.Epub;
using VersOne.Epub.Schema;

namespace Shelf.Extraction;

/// <summary>EPUB metadata from the OPF package and text from the spine (VersOne.Epub with default, strict options; ADR 0005).</summary>
internal static partial class EpubExtractor
{
    // ISBN scan range (ADR 0005, Q6): the first 3 and the last 2 spine items.
    private const int IsbnHeadItems = 3;
    private const int IsbnTailItems = 2;

    public static ExtractedBook Read(string path, bool withText, ExtractionOptions options)
    {
        EpubLimits.Check(path, options);
        return ReadAsync(path, withText).GetAwaiter().GetResult();
    }

    private static async Task<ExtractedBook> ReadAsync(string path, bool withText)
    {
        using var book = await EpubReader.OpenBookAsync(path).ConfigureAwait(false);
        var package = book.Schema.Package;
        var meta = package.Metadata;

        // Creators with a role other than "aut" are editors, translators, illustrators ...; use them only if there is no author.
        var creators = meta.Creators.Where(c => TextHelpers.Clean(c.Creator) is not null).ToList();
        var authors = creators.Where(c => string.IsNullOrEmpty(c.Role) || c.Role == "aut").ToList();
        if (authors.Count == 0)
        {
            authors = creators;
        }

        string? isbn = null;
        foreach (var identifier in meta.Identifiers)
        {
            isbn ??= Isbn.FromIdentifier(identifier.Identifier);
        }

        var (series, seriesIndex) = ReadSeries(meta);
        var segments = new List<TextSegment>();
        if (withText)
        {
            var order = await book.GetReadingOrderAsync().ConfigureAwait(false);
            var spine = package.Spine.Items;
            var scanned = new List<string>();
            for (var i = 0; i < order.Count && i < spine.Count; i++)
            {
                var html = await order[i].ReadContentAsync().ConfigureAwait(false);
                var text = XhtmlText.ToPlainText(html);
                if (string.IsNullOrWhiteSpace(text))
                {
                    continue;
                }

                segments.Add(new TextSegment(Locators.EpubChapter(i, spine[i].IdRef), text, KindOf(html)));
                if (i < IsbnHeadItems || i >= order.Count - IsbnTailItems)
                {
                    scanned.Add(text);
                }
            }

            isbn ??= scanned.Select(Isbn.Find).FirstOrDefault(found => found is not null);
        }

        var metadata = new BookMetadata
        {
            Title = TextHelpers.Clean(book.Title),
            TitleOrigin = TextHelpers.Clean(book.Title) is null ? MetadataOrigin.None : MetadataOrigin.FileContent,
            Authors = [.. authors.Select(c => TextHelpers.Clean(c.Creator)!)],
            AuthorsOrigin = authors.Count == 0 ? MetadataOrigin.None : MetadataOrigin.FileContent,
            Language = meta.Languages.Select(l => TextHelpers.Clean(l.Language)).FirstOrDefault(l => l is not null),
            Year = meta.Dates.Select(d => TextHelpers.YearOf(d.Date)).FirstOrDefault(y => y is not null),
            Publisher = meta.Publishers.Select(p => TextHelpers.Clean(p.Publisher)).FirstOrDefault(p => p is not null),
            Description = meta.Descriptions.Select(d => TextHelpers.Clean(d.Description)).FirstOrDefault(d => d is not null),
            Isbn13 = isbn,
            Series = series,
            SeriesIndex = seriesIndex,
        };
        return new ExtractedBook(metadata, null, segments);
    }

    /// <summary>What a chapter is by its <c>epub:type</c> on body or section, or else by its first heading.</summary>
    private static ContentKind KindOf(string html)
    {
        var type = EpubTypeAttribute().Match(html);
        var kind = type.Success ? ContentClassifier.FromEpubType(type.Groups[1].Value) : ContentKind.Body;
        if (kind != ContentKind.Body)
        {
            return kind;
        }

        var heading = FirstHeading().Match(html);
        return heading.Success ? ContentClassifier.FromTitle(WebUtility.HtmlDecode(Tags().Replace(heading.Groups[1].Value, string.Empty))) : ContentKind.Body;
    }

    [GeneratedRegex(@"<(?:body|section)\b[^>]*?\bepub:type\s*=\s*[""']([^""']*)[""']", RegexOptions.IgnoreCase)]
    private static partial Regex EpubTypeAttribute();

    [GeneratedRegex(@"<h[1-3]\b[^>]*>(.*?)</h[1-3]>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex FirstHeading();

    [GeneratedRegex(@"<[^>]*>")]
    private static partial Regex Tags();

    /// <summary>EPUB 3 collections (<c>belongs-to-collection</c> with <c>group-position</c>) or calibre's <c>calibre:series</c>.</summary>
    private static (string? Series, double? Index) ReadSeries(EpubMetadata meta)
    {
        var collection = meta.MetaItems.Find(m => m.Property == "belongs-to-collection");
        if (collection is not null)
        {
            var position = collection.Id is { } id
                ? meta.MetaItems.Find(m => m.Refines == "#" + id && m.Property == "group-position")?.Content
                : null;
            return (TextHelpers.Clean(collection.Content), ParseIndex(position));
        }

        var calibre = meta.MetaItems.Find(m => m.Name == "calibre:series");
        return calibre is null
            ? (null, null)
            : (TextHelpers.Clean(calibre.Content), ParseIndex(meta.MetaItems.Find(m => m.Name == "calibre:series_index")?.Content));
    }

    private static double? ParseIndex(string? text) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && value >= 0 ? value : null;
}
