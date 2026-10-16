#if EPUB
using System.Text;
using VersOne.Epub;
using VersOne.Epub.Schema;

namespace Proto;

public sealed class SpineEntry
{
    public string Idref { get; set; } = "";

    public string Href { get; set; } = "";

    public bool Linear { get; set; }

    public long Chars { get; set; }

    public string Cfi { get; set; } = "";
}

public sealed class EpubResult
{
    public string Version { get; set; } = "";

    public string? Title { get; set; }

    public List<string> Authors { get; } = [];

    public List<string> AuthorDetails { get; } = [];

    public string? Language { get; set; }

    public List<string> Identifiers { get; } = [];

    public string? OpfIsbn { get; set; }

    public string? SeriesName { get; set; }

    public string? SeriesIndex { get; set; }

    public bool HasCover { get; set; }

    public string CoverRule { get; set; } = "none";

    public string CoverMagic { get; set; } = "";

    public string CoverDeclared { get; set; } = "";

    public long CoverBytes { get; set; }

    public string CoverSha { get; set; } = "";

    public (int W, int H)? CoverSize { get; set; }

    public byte[]? CoverData { get; set; }

    public List<SpineEntry> Spine { get; } = [];

    public int NavEntries { get; set; }

    public int NavMapped { get; set; }

    public string? Text { get; set; }

    public long TextChars { get; set; }

    public long Words { get; set; }

    public List<string> TextIsbns { get; } = [];

    public string? Isbn => OpfIsbn ?? (TextIsbns.Count > 0 ? TextIsbns[0] : null);

    public int XhtmlAOk { get; set; }

    public bool XhtmlAHasUmlautA { get; set; }

    public List<string> XhtmlAErrors { get; } = [];

    public double OpenMs { get; set; }

    public double MetaMs { get; set; }

    public double TextMs { get; set; }

    public double IsbnMs { get; set; }
}

public static class Covers
{
    public static string Magic(ReadOnlySpan<byte> b)
    {
        if (b.Length >= 3 && b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF)
        {
            return "image/jpeg";
        }

        if (b.Length >= 8 && b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47 && b[4] == 0x0D && b[5] == 0x0A && b[6] == 0x1A && b[7] == 0x0A)
        {
            return "image/png";
        }

        if (b.Length >= 6 && b[0] == 'G' && b[1] == 'I' && b[2] == 'F' && b[3] == '8')
        {
            return "image/gif";
        }

        if (b.Length >= 12 && b[0] == 'R' && b[1] == 'I' && b[2] == 'F' && b[3] == 'F' && b[8] == 'W' && b[9] == 'E' && b[10] == 'B' && b[11] == 'P')
        {
            return "image/webp";
        }

        return "unknown";
    }

    public static string Extension(string magic) => magic switch
    {
        "image/jpeg" => "jpg",
        "image/png" => "png",
        "image/gif" => "gif",
        "image/webp" => "webp",
        _ => "bin",
    };

    /// <summary>Width and height from the PNG IHDR chunk.</summary>
    public static (int W, int H)? PngSize(ReadOnlySpan<byte> b)
    {
        if (Magic(b) != "image/png" || b.Length < 24)
        {
            return null;
        }

        return (System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(b[16..]), System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(b[20..]));
    }

    public static int SelfCheck(TextWriter w)
    {
        var rows = new Rows(w);
        rows.Gate("selfcheck:cover-magic", Magic([0xFF, 0xD8, 0xFF, 0xE0]) == "image/jpeg" && Magic([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]) == "image/png"
            && Magic("GIF89a"u8) == "image/gif" && Magic([1, 2, 3, 4]) == "unknown");
        var png = new byte[24];
        new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }.CopyTo(png, 0);
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(png.AsSpan(16), 64);
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(png.AsSpan(20), 96);
        rows.Gate("selfcheck:png-ihdr", PngSize(png) == (64, 96));
        rows.Gate("selfcheck:cfi", Epub.Cfi(0, "ch1") == "epubcfi(/6/2[ch1]!)" && Epub.Cfi(4, "x").StartsWith("epubcfi(/6/10", StringComparison.Ordinal));
        return rows.Failed;
    }
}

public static class Epub
{
    public static string Cfi(int index, string idref) => $"epubcfi(/6/{2 * (index + 1)}[{idref}]!)";

    public static EpubResult Read(string path, bool text, bool cover, bool variantA = false, bool keepText = true) =>
        ReadAsync(path, text, cover, variantA, keepText).GetAwaiter().GetResult();

    public static async Task<EpubResult> ReadAsync(string path, bool text, bool cover, bool variantA, bool keepText)
    {
        var r = new EpubResult();
        var clock = new Clock();
        using var book = await EpubReader.OpenBookAsync(path).ConfigureAwait(false);
        r.OpenMs = clock.Ms;
        clock = new Clock();
        var package = book.Schema.Package;
        r.Version = package.EpubVersion switch
        {
            EpubVersion.EPUB_2 => "2.0",
            EpubVersion.EPUB_3 => "3.0",
            EpubVersion.EPUB_3_1 => "3.1",
            _ => "?",
        };
        var meta = package.Metadata;
        r.Title = string.IsNullOrWhiteSpace(book.Title) ? null : book.Title;
        foreach (var c in meta.Creators)
        {
            r.Authors.Add(c.Creator);
            r.AuthorDetails.Add($"{c.Creator}|fileAs={c.FileAs}|role={c.Role}");
        }

        r.Language = meta.Languages.Count > 0 ? meta.Languages[0].Language : null;
        foreach (var i in meta.Identifiers)
        {
            r.Identifiers.Add($"{i.Scheme}|{i.Identifier}");
            r.OpfIsbn ??= Isbn.FromIdentifier(i.Identifier);
        }

        ReadSeries(meta, r);
        if (cover)
        {
            await ReadCover(book, r).ConfigureAwait(false);
        }

        var order = await book.GetReadingOrderAsync().ConfigureAwait(false);
        var spine = package.Spine.Items;
        for (var i = 0; i < spine.Count; i++)
        {
            var manifest = package.Manifest.Items.Find(m => m.Id == spine[i].IdRef);
            r.Spine.Add(new SpineEntry { Idref = spine[i].IdRef, Href = manifest?.Href ?? "", Linear = spine[i].IsLinear, Cfi = Cfi(i, spine[i].IdRef) });
        }

        var nav = await book.GetNavigationAsync().ConfigureAwait(false);
        if (nav is not null)
        {
            var keys = order.Select(o => o.Key).ToHashSet(StringComparer.Ordinal);
            Walk(nav, keys, r);
        }

        r.MetaMs = clock.Ms;
        if (!text)
        {
            return r;
        }

        clock = new Clock();
        var sb = keepText ? new StringBuilder() : null;
        var scan = new List<(int Index, string Plain)>();
        for (var i = 0; i < order.Count; i++)
        {
            var html = await order[i].ReadContentAsync().ConfigureAwait(false);
            var plain = XhtmlText.Tolerant(html);
            r.Spine[i].Chars = plain.Length;
            r.TextChars += plain.Length;
            r.Words += Text.CountWords(plain);
            sb?.Append(plain).Append('\n');
            if (i < 3 || i >= order.Count - 2)
            {
                scan.Add((i, plain));
            }

            if (variantA)
            {
                try
                {
                    await using var s = await order[i].GetContentStreamAsync().ConfigureAwait(false);
                    var strict = XhtmlText.Strict(s);
                    r.XhtmlAHasUmlautA |= strict.Contains('\u00E4', StringComparison.Ordinal);
                    r.XhtmlAOk++;
                }
                catch (Exception e) when (e is not OutOfMemoryException)
                {
                    r.XhtmlAErrors.Add(e.GetType().Name);
                }
            }
        }

        r.TextMs = clock.Ms;
        r.Text = sb?.ToString();
        clock = new Clock();
        foreach (var (_, plain) in scan)
        {
            foreach (var hit in Isbn.FindAll(plain))
            {
                if (!r.TextIsbns.Contains(hit))
                {
                    r.TextIsbns.Add(hit);
                }
            }
        }

        r.IsbnMs = clock.Ms;
        return r;
    }

    private static void Walk(List<EpubNavigationItemRef> items, HashSet<string> spineKeys, EpubResult r)
    {
        foreach (var item in items)
        {
            if (item.Type == EpubNavigationItemType.LINK)
            {
                r.NavEntries++;
                if (item.HtmlContentFileRef is { } f && spineKeys.Contains(f.Key))
                {
                    r.NavMapped++;
                }
            }

            Walk(item.NestedItems, spineKeys, r);
        }
    }

    private static void ReadSeries(EpubMetadata meta, EpubResult r)
    {
        var collection = meta.MetaItems.Find(m => m.Property == "belongs-to-collection");
        if (collection is not null)
        {
            r.SeriesName = collection.Content;
            if (collection.Id is { } id)
            {
                r.SeriesIndex = meta.MetaItems.Find(m => m.Refines == "#" + id && m.Property == "group-position")?.Content;
            }

            return;
        }

        var calibre = meta.MetaItems.Find(m => m.Name == "calibre:series");
        if (calibre is not null)
        {
            r.SeriesName = calibre.Content;
            r.SeriesIndex = meta.MetaItems.Find(m => m.Name == "calibre:series_index")?.Content;
        }
    }

    private static async Task ReadCover(EpubBookRef book, EpubResult r)
    {
        var package = book.Schema.Package;
        if (book.Content.Cover is not { } cover)
        {
            return;
        }

        var bytes = await cover.ReadContentAsBytesAsync().ConfigureAwait(false);
        r.HasCover = true;
        r.CoverBytes = bytes.Length;
        r.CoverMagic = Covers.Magic(bytes);
        r.CoverDeclared = cover.ContentMimeType;
        r.CoverSha = Text.Sha256Hex(bytes);
        r.CoverSize = Covers.PngSize(bytes);
        r.CoverData = bytes;
        var v3 = package.EpubVersion is EpubVersion.EPUB_3 or EpubVersion.EPUB_3_1;
        r.CoverRule =
            v3 && package.Manifest.Items.Any(m => m.Properties?.Contains(EpubManifestProperty.COVER_IMAGE) == true) ? "epub3-property"
            : package.Metadata.MetaItems.Any(m => string.Equals(m.Name, "cover", StringComparison.OrdinalIgnoreCase)) ? "epub2-meta"
            : package.Guide?.Items.Any(g => string.Equals(g.Type, "cover", StringComparison.OrdinalIgnoreCase)) == true ? "guide"
            : "unknown";
    }
}
#endif
