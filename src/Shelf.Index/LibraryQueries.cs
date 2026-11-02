using Shelf.Core.Search;

namespace Shelf.Index;

/// <summary>Reads a query of a library: a word <c>@name</c> stands for a saved search (F-SRC-9).</summary>
public static class LibraryQueries
{
    /// <summary>The query text with the saved searches in place of the names.</summary>
    /// <exception cref="QueryException">A name is not saved, or the saved searches cannot be read.</exception>
    public static string Expand(string libraryRoot, string text) =>
        SavedSearches.Expand(text, () => SavedSearches.Load(libraryRoot));

    /// <exception cref="QueryException">The query is invalid, a name is not saved, or the saved searches cannot be read.</exception>
    public static SearchQuery Parse(string libraryRoot, string text) => QueryParser.Parse(Expand(libraryRoot, text));
}
