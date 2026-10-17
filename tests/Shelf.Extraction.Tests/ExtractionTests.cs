using System.IO.Compression;
using System.Xml;
using Xunit;

namespace Shelf.Extraction.Tests;

public sealed class ExtractionTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("shelf-extraction-").FullName;

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [Fact]
    public void Classify_maps_the_exceptions_of_the_parsers_to_kinds()
    {
        Assert.Equal(ExtractionErrorKind.NotAContainer, BookExtractor.Classify(new InvalidDataException()));
        Assert.Equal(ExtractionErrorKind.Io, BookExtractor.Classify(new FileNotFoundException()));
        Assert.Equal(ExtractionErrorKind.Io, BookExtractor.Classify(new UnauthorizedAccessException()));
        Assert.Equal(ExtractionErrorKind.Unsupported, BookExtractor.Classify(new NotSupportedException()));
        Assert.Equal(ExtractionErrorKind.BadEpub, BookExtractor.Classify(new XmlException()));
        Assert.Equal(ExtractionErrorKind.Other, BookExtractor.Classify(new IndexOutOfRangeException()));
    }

    [Theory]
    [InlineData(ExtractionErrorKind.NotAContainer, "not-a-container")]
    [InlineData(ExtractionErrorKind.BadEpub, "bad-epub")]
    [InlineData(ExtractionErrorKind.BadPdf, "bad-pdf")]
    [InlineData(ExtractionErrorKind.Encrypted, "encrypted")]
    [InlineData(ExtractionErrorKind.Unsupported, "unsupported")]
    [InlineData(ExtractionErrorKind.Io, "io")]
    [InlineData(ExtractionErrorKind.TooLarge, "too-large")]
    [InlineData(ExtractionErrorKind.Other, "other")]
    public void Every_kind_has_its_name(ExtractionErrorKind kind, string name) => Assert.Equal(name, KindNames.Of(kind));

    [Fact]
    public void A_missing_file_is_an_io_error_of_that_file()
    {
        var path = Path.Combine(_directory, "missing.pdf");

        var error = Assert.Throws<ExtractionException>(() => BookExtractor.Extract(path));

        Assert.Equal(ExtractionErrorKind.Io, error.Kind);
        Assert.Equal(path, error.Path);
    }

    [Theory]
    [InlineData("book.EPUB", true)]
    [InlineData("book.Pdf", true)]
    [InlineData("book.mobi", false)]
    [InlineData("book", false)]
    public void IsSupported_looks_at_the_extension_without_case(string name, bool expected) =>
        Assert.Equal(expected, BookExtractor.IsSupported(name));

    [Fact]
    public void The_total_size_limit_applies_to_many_small_entries()
    {
        var path = Path.Combine(_directory, "many.epub");
        using (var file = File.Create(path))
        using (var archive = new ZipArchive(file, ZipArchiveMode.Create))
        {
            for (var i = 0; i < 4; i++)
            {
                using var stream = archive.CreateEntry($"e{i}.xhtml").Open();
                stream.Write(new byte[1000]);
            }
        }

        var options = ExtractionOptions.Default with { MaxTotalBytes = 3500 };

        var error = Assert.Throws<ExtractionException>(() => BookExtractor.Extract(path, options));

        Assert.Equal(ExtractionErrorKind.TooLarge, error.Kind);
    }

    [Fact]
    public void Metadata_only_reads_no_text()
    {
        var path = Path.Combine(RepositoryRoot(), "tests", "fixtures", "derived", "epub3-isbn-series.epub");

        var metadata = BookExtractor.ReadMetadata(path);

        Assert.Equal("Event Streams in Practice", metadata.Title);
        Assert.Equal("9780000000019", metadata.Isbn13);
    }

    [Fact]
    public void Without_the_file_name_fallback_nothing_is_invented()
    {
        var path = Path.Combine(RepositoryRoot(), "tests", "fixtures", "derived", "Jane Doe - Quiet Systems (2004).pdf");

        var metadata = BookExtractor.ReadMetadata(path, ExtractionOptions.Default with { FileNameFallback = false });

        Assert.Null(metadata.Title);
        Assert.Empty(metadata.Authors);
        Assert.Null(metadata.Year);
    }

    [Fact]
    public void YearOf_accepts_publication_years_only()
    {
        Assert.Equal(2017, TextHelpers.YearOf("2017-05-01"));
        Assert.Equal(1999, TextHelpers.YearOf("1999"));
        Assert.Null(TextHelpers.YearOf("0042-01-01"));
        Assert.Null(TextHelpers.YearOf("2999"));
        Assert.Null(TextHelpers.YearOf("May 2017"));
        Assert.Null(TextHelpers.YearOf(null));
    }

    [Fact]
    public void Clean_collapses_white_space_and_turns_blank_into_null()
    {
        Assert.Equal("a b c", TextHelpers.Clean("  a \n b\tc "));
        Assert.Null(TextHelpers.Clean("  \n "));
        Assert.Null(TextHelpers.Clean(null));
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "shelf.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("shelf.slnx not found above the test binary.");
    }
}
