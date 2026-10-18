using System.Diagnostics;
using Shelf.Index;
using Shelf.Semantic;

namespace Shelf.Cli.Commands;

/// <summary><c>shelf reindex --embeddings</c> (F-SEM-7): installs the model if needed, then makes the missing vectors.</summary>
internal static class EmbeddingsRunner
{
    private static readonly TimeSpan ProgressInterval = TimeSpan.FromSeconds(1);

    public static int Run(LibraryContext context, bool json, TextWriter stdout, TextWriter stderr, CliEnvironment environment)
    {
        var name = context.Settings.Semantic.Model;
        var model = environment.ModelCatalog.Find(name)
            ?? throw new ModelInstallException(
                $"unknown model '{name}'; known models: {string.Join(", ", environment.ModelCatalog.Models.Select(m => m.Name))}");

        var folder = ModelInstaller.FolderFor(context.Root, model.Name);
        if (!ModelInstaller.IsInstalled(model, folder))
        {
            stderr.WriteLine($"{model.Name} is not installed; downloading it once");
            ModelInstaller.Download(model, context.Root, environment.ModelDownloader, message => stderr.WriteLine(message));
        }

        var embedder = environment.EmbedderFactory(folder);
        try
        {
            using var connection = IndexDatabase.OpenWrite(IndexDatabase.PathFor(context.Root));
            var store = VectorStore.Open(connection, embedder.Dimensions, model.Name);
            if (!store.IsCurrentModel)
            {
                // F-SEM-8: vectors of two models cannot be compared. Queue every file first, then start the store over, so that
                // a run that stops in between is found out and done again.
                stderr.WriteLine(
                    $"warning: the embeddings were made with {store.StoredModel} ({store.StoredDimensions} dimensions); switching to {model.Name} makes them all again");
                EmbeddingIndexer.RequeueAll(connection);
                store = store.Reset();
            }

            var clock = Stopwatch.StartNew();
            var lastPrinted = TimeSpan.Zero;
            var result = EmbeddingIndexer.Run(
                connection,
                embedder,
                store,
                progress =>
                {
                    if (progress.Done == progress.Total || clock.Elapsed - lastPrinted >= ProgressInterval)
                    {
                        lastPrinted = clock.Elapsed;
                        stderr.WriteLine($"embedding {progress.Phase} {progress.Done}/{progress.Total}");
                    }
                },
                environment.Cancellation);
            IndexDatabase.Checkpoint(connection);

            foreach (var problem in result.Problems)
            {
                stderr.WriteLine($"warning: {problem}");
            }

            if (json)
            {
                stdout.WriteLine(JsonOutput.Object(
                    ("model", model.Name),
                    ("works", result.Works),
                    ("passages", result.Passages),
                    ("files", result.Files),
                    ("remaining", result.Remaining),
                    ("interrupted", result.Interrupted),
                    ("problems", result.Problems.Count)));
            }
            else if (result.Works == 0 && result.Passages == 0 && !result.Interrupted)
            {
                stdout.WriteLine(result.Problems.Count == 0 ? "Nothing to embed." : $"Nothing embedded, {result.Problems.Count} problems.");
            }
            else
            {
                stdout.WriteLine($"Embedded {TextFormat.Plural(result.Works, "work", "works")} and {TextFormat.Plural(result.Passages, "passage", "passages")} from {TextFormat.Plural(result.Files, "file", "files")}.");
            }

            if (result.Interrupted)
            {
                stderr.WriteLine($"interrupted: {TextFormat.Plural(result.Remaining, "file", "files")} still wait for embeddings; run the same command again to continue");
                return ExitCodes.Error;
            }

            return result.Problems.Count == 0 ? ExitCodes.Success : ExitCodes.Error;
        }
        finally
        {
            (embedder as IDisposable)?.Dispose();
        }
    }
}
