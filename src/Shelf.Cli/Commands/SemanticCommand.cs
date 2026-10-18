using System.CommandLine;
using System.Globalization;
using System.Text.Json;
using Shelf.Index;
using Shelf.Semantic;

namespace Shelf.Cli.Commands;

/// <summary><c>shelf semantic &lt;text&gt; [--chunks]</c>: search by meaning, for works or for passages (F-SEM-1 to F-SEM-3).</summary>
internal static class SemanticCommand
{
    private const string EmbedHint = "shelf reindex --embeddings";

    public static Command Create(Option<bool> json, TextWriter stdout, TextWriter stderr, CliEnvironment environment)
    {
        var words = new Argument<string[]>("text")
        {
            Description = "What the books should be about, in any language the model knows",
            Arity = ArgumentArity.OneOrMore,
        };
        var chunks = new Option<bool>("--chunks") { Description = "Find passages with their location instead of works" };
        var limit = new Option<int>("--limit")
        {
            Description = "Largest number of hits to show",
            DefaultValueFactory = _ => LibrarySearch.DefaultLimit,
        };
        limit.Validators.Add(result =>
        {
            if (result.GetValueOrDefault<int>() is < 1 or > 1000)
            {
                result.AddError("--limit must be between 1 and 1000");
            }
        });
        var library = LibraryContext.CreateOption();
        var command = new Command("semantic", "Search by meaning: works, or with --chunks the passages inside them");
        command.Arguments.Add(words);
        command.Options.Add(chunks);
        command.Options.Add(limit);
        command.Options.Add(library);
        command.SetAction(parseResult =>
        {
            var text = string.Join(' ', parseResult.GetRequiredValue(words)).Trim();
            if (text.Length == 0)
            {
                stderr.WriteLine("error: the query has nothing to search for");
                return ExitCodes.Usage;
            }

            var context = LibraryContext.Open(parseResult.GetValue(library), environment, stderr);
            var passages = parseResult.GetValue(chunks);
            using var connection = IndexDatabase.OpenRead(IndexDatabase.PathFor(context.Root));
            var store = VectorStore.OpenExisting(connection);

            List<SemanticHit> hits = [];
            if (store is not null)
            {
                hits = Search(context, environment, connection, store, text, passages, parseResult.GetValue(limit));
            }

            var coverage = SemanticSearch.Coverage(connection, store);
            if (parseResult.GetValue(json))
            {
                stdout.WriteLine(JsonOutput.Write(writer => WriteJson(writer, text, passages, hits, coverage)));
            }
            else if (hits.Count == 0)
            {
                stdout.WriteLine("No results.");
            }
            else
            {
                var palette = Palette.For(environment, json: false);
                for (var i = 0; i < hits.Count; i++)
                {
                    WriteText(stdout, palette, i + 1, hits[i]);
                }
            }

            var empty = store is null || (store.Count(VectorKind.Work) == 0 && store.Count(VectorKind.Chunk) == 0);
            WriteNote(stderr, empty, coverage);
            return ExitCodes.Success;
        });
        return command;
    }

    private static List<SemanticHit> Search(LibraryContext context, CliEnvironment environment, Microsoft.Data.Sqlite.SqliteConnection connection, VectorStore store, string text, bool passages, int limit)
    {
        var name = context.Settings.Semantic.Model;
        if (store.Model != name)
        {
            throw new VectorStoreException($"the vectors in this index were made with {store.Model} ({store.Dimensions} dimensions), not {name}; make them again with: shelf reindex, then {EmbedHint}");
        }

        var model = environment.ModelCatalog.Find(name)
            ?? throw new ModelInstallException($"unknown model '{name}'; known models: {string.Join(", ", environment.ModelCatalog.Models.Select(m => m.Name))}");
        var folder = ModelInstaller.FolderFor(context.Root, model.Name);
        if (!ModelInstaller.IsInstalled(model, folder))
        {
            throw new ModelInstallException($"{name} is not installed; run: shelf model install");
        }

        var embedder = environment.EmbedderFactory(folder);
        try
        {
            var query = embedder.EmbedQuery(text);
            return passages
                ? SemanticSearch.SearchPassages(connection, store, query, limit)
                : SemanticSearch.SearchWorks(connection, store, query, limit);
        }
        finally
        {
            (embedder as IDisposable)?.Dispose();
        }
    }

    private static void WriteNote(TextWriter stderr, bool noEmbeddings, SemanticCoverage coverage)
    {
        if (noEmbeddings && coverage.PendingFiles + coverage.WorksWithoutVector > 0)
        {
            stderr.WriteLine($"note: no embeddings yet; run: {EmbedHint}");
        }
        else if (coverage.PendingFiles > 0)
        {
            stderr.WriteLine(coverage.PendingFiles == 1
                ? $"note: 1 file still waits for its embeddings; run: {EmbedHint}"
                : $"note: {coverage.PendingFiles.ToString(CultureInfo.InvariantCulture)} files still wait for their embeddings; run: {EmbedHint}");
        }
        else if (coverage.WorksWithoutVector > 0)
        {
            stderr.WriteLine($"note: {TextFormat.Plural(coverage.WorksWithoutVector, "work has", "works have")} no embedding yet; run: {EmbedHint}");
        }
    }

    private static void WriteText(TextWriter stdout, Palette palette, int number, SemanticHit hit)
    {
        var work = hit.Work;
        var line = $"{number.ToString(CultureInfo.InvariantCulture)}. {palette.Bold(work.Title)} - {string.Join(", ", work.Authors)}";
        if (work.Year is { } year)
        {
            line += $" ({year.ToString(CultureInfo.InvariantCulture)})";
        }

        stdout.WriteLine($"{line} [{string.Join(", ", work.Formats)}]  {palette.Dim(work.Id.ToString())}  {hit.Score.ToString("F2", CultureInfo.InvariantCulture)}");
        if (hit.Passage is { } passage)
        {
            stdout.WriteLine($"   {palette.Dim($"{passage.Where}  {Path.GetFileName(passage.File)}:")} {passage.Snippet}");
        }
    }

    private static void WriteJson(Utf8JsonWriter writer, string query, bool passages, List<SemanticHit> hits, SemanticCoverage coverage)
    {
        writer.WriteStartObject();
        JsonOutput.Property(writer, "query", query);
        JsonOutput.Property(writer, "level", passages ? "chunk" : "work");
        JsonOutput.Property(writer, "total", hits.Count);
        JsonOutput.Property(writer, "pendingFiles", coverage.PendingFiles);
        JsonOutput.Property(writer, "worksWithoutEmbedding", coverage.WorksWithoutVector);
        writer.WriteStartArray("results");
        foreach (var hit in hits)
        {
            var work = hit.Work;
            writer.WriteStartObject();
            JsonOutput.Property(writer, "id", work.Id.ToString());
            JsonOutput.Property(writer, "title", work.Title);
            writer.WriteStartArray("authors");
            foreach (var author in work.Authors)
            {
                writer.WriteStringValue(author);
            }

            writer.WriteEndArray();
            if (work.Year is { } year)
            {
                JsonOutput.Property(writer, "year", year);
            }

            JsonOutput.Property(writer, "language", work.Language);
            writer.WriteStartArray("formats");
            foreach (var format in work.Formats)
            {
                writer.WriteStringValue(format);
            }

            writer.WriteEndArray();
            JsonOutput.Property(writer, "score", Math.Round(hit.Score, 4));
            if (hit.Passage is { } passage)
            {
                JsonOutput.Property(writer, "file", passage.File);
                JsonOutput.Property(writer, "format", passage.Format);
                JsonOutput.Property(writer, "locator", passage.Locator);
                JsonOutput.Property(writer, "where", passage.Where);
                JsonOutput.Property(writer, "snippet", passage.Snippet);
            }

            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    }
}
