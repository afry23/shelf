using System.Security.Cryptography;
using Shelf.Core.Grouping;
using Shelf.Extraction;

namespace Shelf.Index;

/// <summary>What reading one file produced. Created by worker threads, applied to the index by one thread.</summary>
/// <param name="Sha256">Lower-case hex; null if the file could not be read at all.</param>
/// <param name="Book">The extraction result; null if the file could not be extracted or was not extracted at all.</param>
/// <param name="Error">Why extraction failed; null on success.</param>
/// <param name="Extracted">False if extraction was skipped because the hash equals the known one.</param>
internal sealed record FileAnalysis(
    string Path,
    long Size,
    long Mtime,
    string? Sha256,
    ExtractedBook? Book,
    ExtractionException? Error,
    ulong? Fingerprint,
    IReadOnlyList<TextChunk> Chunks,
    bool Extracted)
{
    public string TextStatus => Error is not null ? "failed" : Chunks.Count > 0 ? "done" : "none";
}

internal static class FileAnalyzer
{
    /// <summary>At most this many files are read at the same time (PdfPig does not scale beyond 4, ADR 0005).</summary>
    public const int MaxWorkers = 4;

    private const int BatchSize = MaxWorkers * 4;

    /// <summary>
    /// Hashes the file, then reads it, unless the hash equals <paramref name="knownSha256"/> (the file was only
    /// touched). Never throws for a bad file: problems are in <see cref="FileAnalysis.Error"/>.
    /// </summary>
    public static FileAnalysis Analyze(string path, string? knownSha256 = null)
    {
        FileInfo info;
        string sha;
        try
        {
            info = new FileInfo(path);
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 16);
            // Stat after opening so that size and time belong to the bytes that are hashed.
            info.Refresh();
            sha = Convert.ToHexStringLower(SHA256.HashData(stream));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            var error = new ExtractionException($"{path}: io: {e.Message}", ExtractionErrorKind.Io, path, e);
            return new FileAnalysis(path, 0, 0, null, null, error, null, [], Extracted: true);
        }

        var size = info.Length;
        var mtime = IndexWriter.Mtime(info);
        if (knownSha256 is not null && knownSha256 == sha)
        {
            return new FileAnalysis(path, size, mtime, sha, null, null, null, [], Extracted: false);
        }

        try
        {
            var book = BookExtractor.Extract(path);
            return new FileAnalysis(
                path,
                size,
                mtime,
                sha,
                book,
                null,
                SimHash.Compute(book.Segments.Select(s => s.Text)),
                TextChunker.Chunk(book.Segments),
                Extracted: true);
        }
        catch (ExtractionException e)
        {
            return new FileAnalysis(path, size, mtime, sha, null, e, null, [], Extracted: true);
        }
    }

    /// <summary>
    /// Analyzes the files with up to <see cref="MaxWorkers"/> threads and hands the results to
    /// <paramref name="apply"/> one at a time and in the order of <paramref name="items"/>, so that the outcome
    /// does not depend on thread timing. Memory is bounded by one batch.
    /// </summary>
    public static void ForEach(IReadOnlyList<(string Path, string? KnownSha256)> items, Action<int, FileAnalysis> apply)
    {
        for (var start = 0; start < items.Count; start += BatchSize)
        {
            var count = Math.Min(BatchSize, items.Count - start);
            var results = new FileAnalysis[count];
            Parallel.For(
                0,
                count,
                new ParallelOptions { MaxDegreeOfParallelism = MaxWorkers },
                i => results[i] = Analyze(items[start + i].Path, items[start + i].KnownSha256));
            for (var i = 0; i < count; i++)
            {
                apply(start + i, results[i]);
            }
        }
    }
}
