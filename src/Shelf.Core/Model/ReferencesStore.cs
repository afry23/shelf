using System.Text;

namespace Shelf.Core.Model;

/// <summary>
/// Reads and writes <c>references.json</c> files. A write is atomic like that of <see cref="SidecarStore"/>. A work
/// without references has no file: writing an empty list removes it.
/// </summary>
public static class ReferencesStore
{
    /// <summary>The references of the file at this path; a missing file has none.</summary>
    /// <exception cref="SidecarException">The file is unreadable or invalid.</exception>
    public static ReferencesSidecar Read(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        if (!File.Exists(path))
        {
            return new ReferencesSidecar();
        }

        string text;
        try
        {
            text = File.ReadAllText(path, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or DecoderFallbackException)
        {
            throw new SidecarException($"{path}: cannot read file: {e.Message}", path, e);
        }

        return ReferencesFormat.Parse(text, path);
    }

    /// <exception cref="SidecarException"/>
    public static void Write(string path, ReferencesSidecar sidecar)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        ArgumentNullException.ThrowIfNull(sidecar);

        ReferencesFormat.Validate(sidecar, path);
        try
        {
            if (sidecar.References.Count == 0 && sidecar.UnknownFields.Count == 0)
            {
                File.Delete(path);
                return;
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            throw new SidecarException($"{path}: cannot remove file: {e.Message}", path, e);
        }

        var bytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(ReferencesFormat.Serialize(sidecar));
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
            try
            {
                File.Delete(temporary);
            }
            catch (Exception cleanup) when (cleanup is IOException or UnauthorizedAccessException)
            {
                // Best effort: the original error is the one worth reporting.
            }

            throw new SidecarException($"{path}: cannot write file: {e.Message}", path, e);
        }
    }
}
