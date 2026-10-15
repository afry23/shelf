namespace Shelf.TestData.Model;

/// <summary>Closed list of per-file traits recorded in <c>ground-truth.json</c> (slice plan section 3.4).</summary>
public static class Traits
{
    public const string Epub2 = "epub2";
    public const string Epub3 = "epub3";
    public const string Cover = "cover";
    public const string Isbn = "isbn";
    public const string Series = "series";
    public const string AuthorSurnameFirst = "author-surname-first";
    public const string AuthorInitials = "author-initials";
    public const string AuthorNoDiacritics = "author-no-diacritics";
    public const string TitleEditionSuffix = "title-edition-suffix";
    public const string TitleLeadingArticle = "title-leading-article";
    public const string TitleNonAscii = "title-non-ascii";
    public const string PdfInfo = "pdf-info";
    public const string PdfXmp = "pdf-xmp";
    public const string PdfNoMetadata = "pdf-no-metadata";
    public const string PdfIsbnInText = "pdf-isbn-in-text";
    public const string FileNameAuthorTitle = "file-name-author-title";
    public const string LanguageDe = "language-de";
    public const string LanguageEn = "language-en";

    public static IReadOnlyList<string> All { get; } =
    [
        Epub2, Epub3, Cover, Isbn, Series, AuthorSurnameFirst, AuthorInitials, AuthorNoDiacritics,
        TitleEditionSuffix, TitleLeadingArticle, TitleNonAscii, PdfInfo, PdfXmp, PdfNoMetadata, PdfIsbnInText,
        FileNameAuthorTitle, LanguageDe, LanguageEn,
    ];
}

/// <summary>Closed list of traits of committed fixtures (manifest.json), independent of the generator traits.</summary>
public static class FixtureTraits
{
    public static IReadOnlyList<string> All { get; } =
    [
        "epub2", "epub3", "cover", "no-cover", "language-de", "language-en", "non-ascii-title", "isbn", "series",
        "empty-metadata", "malformed-opf", "not-a-zip", "pdf-text-layer", "pdf-image-only", "pdf-info", "pdf-xmp",
        "pdf-no-metadata",
    ];
}

public static class RelationKinds
{
    public const string Edition = "edition";
    public const string SameTitleDifferentAuthor = "same-title-different-author";
}

public static class DuplicateKinds
{
    public const string Exact = "exact";
    public const string SameFormat = "same-format";
}
