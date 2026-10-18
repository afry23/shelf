using Shelf.Core.Configuration;
using Shelf.Semantic;
using Xunit;

namespace Shelf.Library.Tests;

/// <summary>Argument checks of the service: they fail before the index is opened, so no library is needed.</summary>
public sealed class LibraryServiceTests : IDisposable
{
    private readonly SemanticBackend _semantic = new(ModelCatalog.Default, _ => throw new InvalidOperationException("no model in this test"));
    private readonly LibraryService _service;

    public LibraryServiceTests() => _service = new LibraryService("/nonexistent", ShelfSettings.Defaults, _semantic);

    public void Dispose() => _semantic.Dispose();

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public void SearchBooks_refuses_a_limit_out_of_range(int limit)
    {
        var error = Assert.Throws<LibraryException>(() => _service.SearchBooks("event", limit));
        Assert.Equal("limit must be between 1 and 100.", error.Message);
    }

    [Fact]
    public void SearchBooks_refuses_a_query_without_words()
    {
        var error = Assert.Throws<LibraryException>(() => _service.SearchBooks("   ", 20));
        Assert.Equal("the query has nothing to search for", error.Message);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(20_001)]
    public void ReadPassage_refuses_max_chars_out_of_range(int maxChars)
    {
        var error = Assert.Throws<LibraryException>(() => _service.ReadPassage("01", "page=1", maxChars));
        Assert.Equal("maxChars must be between 1 and 20000.", error.Message);
    }

    [Fact]
    public void ReadPassage_refuses_a_negative_offset()
    {
        var error = Assert.Throws<LibraryException>(() => _service.ReadPassage("01", "page=1", offset: -1));
        Assert.Equal("offset must not be negative.", error.Message);
    }

    [Fact]
    public void SemanticSearch_refuses_an_unknown_level()
    {
        var error = Assert.Throws<LibraryException>(() => _service.SemanticSearch("messaging", "book"));
        Assert.Equal("level must be 'work' or 'chunk'.", error.Message);
    }

    [Fact]
    public void SemanticSearch_refuses_an_empty_query()
    {
        var error = Assert.Throws<LibraryException>(() => _service.SemanticSearch("  "));
        Assert.Equal("the query has nothing to search for", error.Message);
    }

    [Fact]
    public void AddTags_refuses_an_empty_list()
    {
        var error = Assert.Throws<LibraryException>(() => _service.AddTags("01", []));
        Assert.Equal("give at least one tag", error.Message);
    }
}
