using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
using Microsoft.AspNetCore.Mvc;
using Shelf.Index;
using Shelf.Library;

namespace Shelf.Web;

/// <summary>The endpoints of <c>/api/v1</c> that read and change the library (PRD 7.3). The rules are those of <see cref="LibraryService"/>.</summary>
internal static class Api
{
    private const int DefaultSearchLimit = 20;

    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet("/works", (
                [FromServices] LibraryHandle library,
                string? q,
                string? sort,
                string? order,
                string? offset,
                string? limit) =>
            Run(WebJsonContext.Default.WorkList, () =>
            {
                var from = Whole(offset, "offset must not be negative.", 0);
                var count = Whole(limit, $"limit must be between 1 and {BrowseOptions.MaxLimit}.", BrowseOptions.DefaultLimit);
                var page = library.Service.Browse(q, sort ?? "title", order ?? "asc", from, count);
                return new WorkList(page.Total, from, count, [.. page.Items.Select(Item)], Facets(page.Facets));
            }))
            .WithName("ListWorks")
            .Produces<WorkList>(StatusCodes.Status200OK);

        api.MapGet("/works/{id}", ([FromServices] LibraryHandle library, string id) =>
                Run(WebJsonContext.Default.BookDetails, () => library.Service.GetBook(RequireId(id))))
            .WithName("GetWork")
            .Produces<BookDetails>(StatusCodes.Status200OK);

        api.MapMethods("/works/{id}", ["PATCH"], ([FromServices] LibraryHandle library, string id, [FromBody] JsonObject patch) =>
                Run(WebJsonContext.Default.BookDetails, () => library.Service.EditMetadata(RequireId(id), patch)))
            .WithName("EditWork")
            .Accepts<JsonObject>("application/merge-patch+json", "application/json")
            .Produces<BookDetails>(StatusCodes.Status200OK);

        api.MapPut("/works/{id}/tags", ([FromServices] LibraryHandle library, string id, [FromBody] TagsRequest body) =>
                Run(WebJsonContext.Default.TagSetResult, () => library.Service.SetTags(RequireId(id), TagList(body))))
            .WithName("SetWorkTags")
            .Produces<TagSetResult>(StatusCodes.Status200OK);

        api.MapGet("/tags", ([FromServices] LibraryHandle library) =>
                Run(WebJsonContext.Default.TagTree, () => library.Service.ListTags()))
            .WithName("ListTags")
            .Produces<TagTree>(StatusCodes.Status200OK);

        api.MapGet("/search", ([FromServices] LibraryHandle library, string? q, string? mode, string? level, string? limit) =>
                Run(WebJsonContext.Default.SearchResponse, () => Search(library.Service, q, mode ?? "fulltext", level ?? "work", limit)))
            .WithName("Search")
            .Produces<SearchResponse>(StatusCodes.Status200OK);
    }

    private static SearchResponse Search(LibraryService service, string? q, string mode, string level, string? limit)
    {
        if (string.IsNullOrEmpty(q))
        {
            throw new LibraryException("q is required");
        }

        if (mode is not ("fulltext" or "semantic" or "hybrid"))
        {
            throw new LibraryException("mode must be fulltext, semantic or hybrid.");
        }

        var count = Whole(limit, $"limit must be between 1 and {LibraryService.MaxLimit}.", DefaultSearchLimit);
        if (mode == "fulltext")
        {
            var found = service.SearchBooks(q, count);
            return new SearchResponse(
                q,
                mode,
                "work",
                found.Total,
                0,
                0,
                null,
                [.. found.Results.Select(r => new SearchItem(r.Id, r.Title, r.Authors, r.Year, r.Language, r.Formats, r.Score, r.Passages))]);
        }

        var semantic = service.SemanticSearch(q, level, hybrid: mode == "hybrid", count);
        return new SearchResponse(
            semantic.Query,
            mode,
            semantic.Level,
            semantic.Total,
            semantic.PendingFiles,
            semantic.WorksWithoutEmbedding,
            semantic.Note,
            [.. semantic.Results.Select(r => new SearchItem(r.Id, r.Title, r.Authors, r.Year, r.Language, r.Formats, r.Score, r.Passage is { } p ? [p] : []))]);
    }

    /// <summary>The ID of a work, or a refusal. The service also reads folder and file paths; a web client must not probe the disk with them.</summary>
    private static string RequireId(string id) =>
        WorkLookup.LooksLikeId(id) ? id : throw new LibraryException($"no work matches '{id}'", LibraryErrorKind.NotFound);

    private static string[] TagList(TagsRequest body)
    {
        if (body.Tags is not JsonArray array || array.Any(t => t is null || t.GetValueKind() != System.Text.Json.JsonValueKind.String))
        {
            throw new LibraryException("tags must be a list of text");
        }

        return [.. array.Select(t => t!.GetValue<string>())];
    }

    /// <summary>A whole number from the query string, or <paramref name="fallback"/> when it is missing; <paramref name="message"/> when it is not a number.</summary>
    private static int Whole(string? text, string message, int fallback)
    {
        if (text is null)
        {
            return fallback;
        }

        return int.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var number) ? number : throw new LibraryException(message);
    }

    private static IResult Run<T>(JsonTypeInfo<T> info, Func<T> call)
    {
        try
        {
            return Results.Json(call(), info);
        }
        catch (LibraryException e)
        {
            return new ProblemResult(e.Kind == LibraryErrorKind.NotFound ? StatusCodes.Status404NotFound : StatusCodes.Status400BadRequest, e.Message);
        }
        catch (IndexException e)
        {
            return new ProblemResult(StatusCodes.Status500InternalServerError, e.Message);
        }
    }

    private static WorkItem Item(WorkListEntry w) =>
        new(w.Id.ToString(), w.Title, w.Subtitle, [.. w.Authors], w.Year, w.Language, [.. w.Formats], [.. w.Tags], w.ReadStatus);

    private static Facets Facets(BrowseFacets f) =>
        new(Values(f.Tags), Values(f.Authors), Values(f.Languages), Values(f.Years), Values(f.Formats), Values(f.Statuses));

    private static FacetValue[] Values(IReadOnlyList<FacetCount> counts) => [.. counts.Select(c => new FacetValue(c.Value, c.Count))];
}
