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
    [Description("Search titles, authors, tags and the text of the books with the shelf query language (a saved search can be used as @name): words, \"phrases\", prefix*, -excluded, and author: title: tag: publisher: lang: year: format: status: type: series: filters. Returns works, best first, with places in the text (location) that read_passage accepts.")]
    public SearchBooksResult SearchBooks(
        [Description("The query, e.g. 'event sourcing tag:architektur year:>=2015'")] string query,
        [Description("Largest number of works, 1 to 100 (default 20)")] int limit = LibrarySearch.DefaultLimit,
        [Description("Look only at titles and subtitles, not at authors, tags, descriptions or the text; the places in the text are then empty (default false)")] bool titleOnly = false) =>
        Run(() => service.SearchBooks(query, limit, titleOnly));

    [Description("A few highlights that the person made in their books, for one day: the quote, the note, the work and the place. The same day gives the same highlights, so the tool is good for a daily reminder of what was read.")]
    public ResurfaceResult ResurfaceHighlights(
        [Description("How many highlights, 1 to 100 (default 3)")] int? count = null,
        [Description("The day as yyyy-MM-dd (default: today, UTC)")] string? date = null) =>
        Run(() => service.ResurfaceHighlights(count, date));

    [Description("The searches that the person saved, each with its name and its query. A name written as @name in the query of search_books (and of other searches) stands for the saved query, e.g. '@ddd year:>=2020'.")]
    public SavedSearchList ListSavedSearches() => Run(() => service.ListSavedSearches());

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

    [Description("The passages of the books that fit a question, best first, in one call: the text of each place, the work, the location, and for a work with a cite key the citation to paste, e.g. [@alander2021event, p. 2]. Use it to answer a question about the books and to cite the answer. Searches by words and by meaning together; for the meaning it needs the embeddings (shelf reindex --embeddings), and the note says if they are missing or incomplete. Nothing is changed.")]
    public FindPassagesResult FindPassages(
        [Description("The question or what the passages should be about, as a sentence or a few words, in any language the model knows")] string question,
        [Description("Largest number of passages, 1 to 20 (default 5)")] int limit = LibraryService.DefaultPassages,
        [Description("Look only in this work: its ID, or the start of it (default: all works)")] string? id = null,
        [Description("Largest number of characters per passage (default 1500; the hard maximum is set with mcp.max_chars)")] int? maxChars = null) =>
        Run(() => service.FindPassages(question, limit, id, maxChars));

    [Description("The works most like a given work, best first (without the work itself), by the meaning of title, description and tags. Needs the embeddings (shelf reindex --embeddings); the note says if they are missing or incomplete.")]
    public SimilarBooksResult SimilarBooks(
        [Description("The ID of a work, or the start of it")] string id,
        [Description("Largest number of works, 1 to 100 (default 10)")] int limit = 10) =>
        Run(() => service.SimilarBooks(id, limit));

    [Description("The tags that the works most like a given work carry and the work does not, best first, each with the number of those works that carry it and their share of the similarity (0 to 1). Pass the ones that fit to add_tags. Needs the embeddings (shelf reindex --embeddings); the note says if they are missing or incomplete. Nothing is changed.")]
    public TagSuggestionsResult SuggestTags(
        [Description("The ID of a work, or the start of it")] string id,
        [Description("Largest number of tags, 1 to 100 (default 10)")] int limit = 10) =>
        Run(() => service.SuggestTags(id, limit));

    [Description("The reading list: the works (books, papers, articles) a person wants to read next, the one put on first at the top. A work leaves the list by itself when it is marked as read.")]
    public ReadingListResult GetReadingList() => Run(() => service.ReadingList());

    [Description("The works a person linked with a given work by hand (same topic, a sequel, a rebuttal), each with the note the person wrote on this side. Different from similar_books, which is computed. Nothing is changed.")]
    public LinksResult GetLinks([Description("The ID of a work, or the start of it")] string id) => Run(() => service.GetLinks(id));

    [Description("Link two works by hand and say why. The link is written on both works; the note is about the other work, otherNote is the note of the other side. Linking again replaces the notes that are given and changes nothing else (changed is false if nothing was different). Can be undone with unlink_works.")]
    public LinkOutcome LinkWorks(
        [Description("The ID of a work, or the start of it")] string id,
        [Description("The ID of the work to link with, or the start of it")] string other,
        [Description("Why the first work is linked with the other (one line, at most 200 characters); an empty text removes the note")] string? note = null,
        [Description("Why the other work is linked with the first (one line, at most 200 characters)")] string? otherNote = null) =>
        Run(() => service.LinkWorks(id, other, note, otherNote));

    [Description("Remove the link between two works from both of them. Fails if the works are not linked.")]
    public LinkOutcome UnlinkWorks(
        [Description("The ID of a work, or the start of it")] string id,
        [Description("The ID of the linked work, or the start of it")] string other) =>
        Run(() => service.UnlinkWorks(id, other));

    [Description("Put a work on the reading list. A work that is on it already stays as it is (changed is false). A work that is read already is refused. Can be undone with remove_from_reading_list.")]
    public ReadingListChange AddToReadingList([Description("The ID of a work, or the start of it")] string id) =>
        Run(() => service.AddToReadingList(id));

    [Description("Take a work off the reading list. A work that is not on it stays as it is (changed is false).")]
    public ReadingListChange RemoveFromReadingList([Description("The ID of a work, or the start of it")] string id) =>
        Run(() => service.RemoveFromReadingList(id));

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

    [Description("The library in numbers (works, files, size, authors, tags, formats, languages, how many works are unread, being read or read) and what was read: the works set to read per month, the works being read and the works a reader moved in last, each with the day. Works set to read before shelf kept the date are counted in readWithoutDate. Nothing is changed.")]
    public StatsResult GetStats() => Run(() => service.Stats());

    [Description("The references of a paper as they were loaded (each with the work of the library it points to, if its DOI is there) and the works of the library that cite the paper. Nobody is asked: if no list was loaded, 'references' is empty; the person loads one with 'shelf refs fetch'.")]
    public ReferencesResult GetReferences([Description("The ID of a work, or the start of it")] string id) =>
        Run(() => service.References(id));

    [Description("A passage of a book as a Markdown quote with a Pandoc citation of the work and the place, such as '> the passage' followed by '[@alander2021event, p. 12]', and the BibTeX entry of the work. Use the text of the passage and the location from read_passage or search_books. The work needs a cite key. Nothing is changed.")]
    public PassageCitation CitePassage(
        [Description("The ID of a work, or the start of it")] string id,
        [Description("The passage, as it stands in the book")] string quote,
        [Description("The place in the file, as given by the other tools ('page=12' for a PDF, an 'epubcfi(...)' for an EPUB); without it the citation names the work only")] string? locator = null,
        [Description("The name of the file, if the work has more than one and a locator is given")] string? file = null) =>
        Run(() => service.QuotePassageOfWork(id, quote, locator, file));

    [Description("Set a bookmark at a place in a file of a work, with an optional short label. Use a location from get_book, search_books or read_passage ('page=12' for a PDF, an 'epubcfi(...)' for an EPUB). A bookmark that is there already is returned as it is.")]
    public AnnotationView AddBookmark(
        [Description("The ID of a work, or the start of it")] string id,
        [Description("The place in the file, as given by the other tools")] string locator,
        [Description("A short label for the bookmark")] string? label = null,
        [Description("The name of the file, if the work has more than one")] string? file = null) =>
        Run(() => service.AddBookmark(id, locator, label, file));

    [Description("The citations of works as BibTeX, CSL-JSON or RIS, for all works, for the works a search finds, or for one work. Works without a cite key are left out and named in 'skipped'. Read-only: nothing is written.")]
    public CitationsExport ExportCitations(
        [Description("Only the works this search finds, in the language of search_books, e.g. 'tag:thesis year:>=2020 type:paper venue:CVPR' (default: all works)")] string? query = null,
        [Description("bibtex, csl-json or ris (default bibtex)")] string format = "bibtex",
        [Description("The ID of one work, or the start of it; instead of a query")] string? id = null) =>
        Run(() => service.ExportCitations(query, format, id));

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
