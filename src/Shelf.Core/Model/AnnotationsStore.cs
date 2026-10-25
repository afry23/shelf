using System.Text;

namespace Shelf.Core.Model;

/// <summary>
/// Reads and writes <c>annotations.json</c> files. A write is atomic like that of <see cref="SidecarStore"/>. A work
/// without annotations has no file: writing an empty list removes it.
/// </summary>
public static class AnnotationsStore
{
    /// <summary>The annotations of the file at this path; a missing file has none.</summary>
    /// <exception cref="SidecarException">The file is unreadable or invalid.</exception>
    public static AnnotationsSidecar Read(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        if (!File.Exists(path))
        {
            return new AnnotationsSidecar();
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

        return AnnotationsFormat.Parse(text, path);
    }

    /// <exception cref="SidecarException"/>
    public static void Write(string path, AnnotationsSidecar sidecar)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        ArgumentNullException.ThrowIfNull(sidecar);

        AnnotationsFormat.Validate(sidecar, path);
        try
        {
            if (sidecar.Annotations.Count == 0 && sidecar.UnknownFields.Count == 0)
            {
                File.Delete(path);
                return;
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            throw new SidecarException($"{path}: cannot remove file: {e.Message}", path, e);
        }

        var bytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(AnnotationsFormat.Serialize(sidecar));
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
