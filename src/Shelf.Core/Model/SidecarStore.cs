namespace Shelf.Core.Model;

/// <summary>Reads and writes <c>book.json</c> files. A write is atomic: readers see the old or the new file, never half of one.</summary>
public static class SidecarStore
{
    public const string FileName = "book.json";

    /// <exception cref="SidecarException">The file is missing, unreadable or invalid.</exception>
    public static WorkSidecar Read(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);

        string text;
        try
        {
            text = File.ReadAllText(path, new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Text.DecoderFallbackException)
        {
            throw new SidecarException($"{path}: cannot read file: {e.Message}", path, e);
        }

        return SidecarFormat.Parse(text, path);
    }

    /// <summary>
    /// Validates the sidecar and replaces the file. The ID of a work never changes (F-LIB-5): if the file
    /// exists with another ID, or exists but cannot be read, nothing is written.
    /// </summary>
    /// <exception cref="SidecarException"/>
    public static void Write(string path, WorkSidecar sidecar)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        ArgumentNullException.ThrowIfNull(sidecar);

        SidecarFormat.Validate(sidecar, path);
        if (File.Exists(path))
        {
            var existing = Read(path);
            if (existing.Id != sidecar.Id)
            {
                throw new SidecarException($"{path}: id must not change (file has {existing.Id}, new content has {sidecar.Id})", path);
            }
        }

        var bytes = new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(SidecarFormat.Serialize(sidecar));
        var temporary = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.Write(bytes);
                stream.Flush(flushToDisk: true);
            }

            File.Move(temporary, path, overwrite: true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            TryDelete(temporary);
            throw new SidecarException($"{path}: cannot write file: {e.Message}", path, e);
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Best effort: the original error is the one worth reporting.
        }
    }
}
