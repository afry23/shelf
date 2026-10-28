using System.ComponentModel;
using ModelContextProtocol;
using Shelf.Index;
using Shelf.Library;

namespace Shelf.Mcp;

/// <summary>
/// The tools and resources of the MCP server (PRD 7.2): descriptions for the assistant and the translation of a
/// <see cref="LibraryException"/> into a tool error (the SDK sends the message and the server goes on). The rules are in
/// <see cref="LibraryService"/>.
/// </summary>
internal sealed class LibraryTools(LibraryService service)
{
    [Description("Search titles, authors, tags and the text of the books with the shelf query language: words, \"phrases\", prefix*, -excluded, and author: title: tag: lang: year: format: status: filters. Returns works, best first, with places in the text (location) that read_passage accepts.")]
    public SearchBooksResult SearchBooks(
        [Description("The query, e.g. 'event sourcing tag:architektur year:>=2015'")] string query,
        [Description("Largest number of works, 1 to 100 (default 20)")] int limit = LibrarySearch.DefaultLimit,
        [Description("Look only at titles and subtitles, not at authors, tags, descriptions or the text; the places in the text are then empty (default false)")] bool titleOnly = false) =>
        Run(() => service.SearchBooks(query, limit, titleOnly));

    [Description("Metadata, files, tags and table of contents of a work. Locations in the table of contents can be given to read_passage.")]
    public BookDetails GetBook([Description("The ID of a work, or the start of it (at least one character that names exactly one work)")] string id) =>
        Run(() => service.GetBook(id));

    [Description("Plain text of one place in a work, as found by search_books or listed in the table of contents. Long places are read in parts with offset.")]
    public PassageText ReadPassage(
        [Description("The ID of a work, or the start of it")] string id,
        [Description("The location: 'page=12', an EPUB location from search_books or get_book, or just a page number")] string location,
        [Description("Largest number of characters to return (default 4000; the hard maximum is set with mcp.max_chars, 20000 by default)")] int? maxChars = null,
        [Description("Characters to skip at the start of the place, to read on after a part that was cut (default 0)")] int offset = 0,
        [Description("The file name, if the work has several files with the same location (default: the first with text there)")] string? file = null) =>
        Run(() => service.ReadPassage(id, location, maxChars, offset, file));

    [Description("Search by meaning, in any language the model knows: 'Bücher über Messaging' finds an English book on message brokers. level 'work' finds works, 'chunk' finds passages with a location that read_passage takes. Needs the embeddings (shelf reindex --embeddings); the note says if they are missing or incomplete.")]
    public SemanticSearchResult SemanticSearch(
        [Description("What the books or passages should be about, as a sentence or a few words")] string query,
        [Description("'work' (default) finds works, 'chunk' finds passages inside works")] string level = "work",
        [Description("Also use the full-text search and fuse both rankings; then the query may only have words and phrases, no filters (default false)")] bool hybrid = false,
        [Description("Largest number of hits, 1 to 100 (default 10)")] int limit = 10,
        [Description("Compare the query with titles and subtitles only, not with descriptions, tags or the text; only with level 'work' (default false)")] bool titleOnly = false) =>
        Run(() => service.SemanticSearch(query, level, hybrid, limit, titleOnly));

    [Description("The works most like a given work, best first (without the work itself), by the meaning of title, description and tags. Needs the embeddings (shelf reindex --embeddings); the note says if they are missing or incomplete.")]
    public SimilarBooksResult SimilarBooks(
        [Description("The ID of a work, or the start of it")] string id,
        [Description("Largest number of works, 1 to 100 (default 10)")] int limit = 10) =>
        Run(() => service.SimilarBooks(id, limit));

    [Description("Add tags to a work. A tag is free text; '/' makes a hierarchy such as 'architektur/ddd'. Tags the work has are left as they are. The change can be undone with 'shelf undo'.")]
    public TagChange AddTags(
        [Description("The ID of a work, or the start of it")] string id,
        [Description("The tags to add")] string[] tags) =>
        Run(() => service.AddTags(id, tags));

    [Description("Remove tags from a work. Only the tags named are removed; tags below them stay. The change can be undone with 'shelf undo'.")]
    public TagChange RemoveTags(
        [Description("The ID of a work, or the start of it")] string id,
        [Description("The tags to remove")] string[] tags) =>
        Run(() => service.RemoveTags(id, tags));

    [Description("The highlights and notes that a person made in a work, each with the file, the location (pass it on to read_passage), the marked text, the color and the note in Markdown.")]
    public AnnotationList GetAnnotations([Description("The ID of a work, or the start of it")] string id) =>
        Run(() => service.GetAnnotations(id));

    [Description("Add a note in Markdown to a file of a work. It has no place in the text and shows with the highlights of the work. A highlight cannot be added here: a person marks the text in the reader.")]
    public AnnotationView AddNote(
        [Description("The ID of a work, or the start of it")] string id,
        [Description("The note, in Markdown")] string text,
        [Description("The name of the file, if the work has more than one")] string? file = null) =>
        Run(() => service.AddNote(id, text, file));

    [Description("The tags of the library as a tree, with the number of works under each.")]
    public TagTree ListTags() =>
        Run(() => service.ListTags());

    [Description("Duplicate files in the library: identical files (same content) and different files of one work in the same format. Read-only; nothing is deleted.")]
    public DuplicatesResult FindDuplicates() =>
        Run(() => service.FindDuplicates());

    [Description("Works that probably are one book although they are separate works: nearly the same text (a small 'hamming' distance of the text fingerprints), or very similar vectors ('cosine'). For a person to check; read-only, nothing is deleted or marked.")]
    public ProbablePairsResult FindProbableDuplicates() =>
        Run(() => service.FindProbableDuplicates());

    [Description("A dry run of 'shelf organize': which files would move to which folders and names. Nothing is moved.")]
    public OrganizationProposal ProposeOrganization(
        [Description("A path pattern such as '{author_sort}/{title} ({year})/{title}.{ext}' (default: the pattern of the library)")] string? pattern = null) =>
        Run(() => service.ProposeOrganization(pattern));

    // ---- resources ----

    /// <summary>Metadata of a work (without the table of contents: see <see cref="TocJson"/>).</summary>
    public string BookJson(string id) =>
        Resource(() => System.Text.Json.JsonSerializer.Serialize(service.GetBookMetadata(id), McpJsonContext.Default.BookDetails));

    public string TocJson(string id) =>
        Resource(() => System.Text.Json.JsonSerializer.Serialize(service.GetToc(id), McpJsonContext.Default.TocItemArray));

    private static T Run<T>(Func<T> call)
    {
        try
        {
            return call();
        }
        catch (LibraryException e)
        {
            throw new McpException(e.Message);
        }
    }

    // Resources use McpProtocolException with InvalidParams, because only that carries an error code.
    private static string Resource(Func<string> call)
    {
        try
        {
            return call();
        }
        catch (LibraryException e)
        {
            throw new McpProtocolException(e.Message, McpErrorCode.InvalidParams);
        }
    }
}
