using Shelf.Core.Model;
using Shelf.Index;
using Shelf.Organize;

namespace Shelf.Library;

public sealed partial class LibraryService
{
    /// <summary>
    /// Turns the PDF of a work into a reflowable EPUB with the converter of <c>convert.command</c> (F-CNV-1, F-CNV-4), as
    /// <c>shelf convert</c> does. The EPUB is a file of the same work; it can be undone with <c>shelf undo</c>.
    /// </summary>
    /// <param name="fileId">The ID of the PDF; null takes the only PDF of the work.</param>
    /// <param name="replace">Convert again if the work has an EPUB; the old one goes to the trash.</param>
    /// <exception cref="LibraryException">
    /// Invalid: no converter is configured, no PDF, no text layer. NotFound: no such work or file. Conflict: the work has an
    /// EPUB and <paramref name="replace"/> is not set. Failed: the converter failed, ran too long or wrote no usable EPUB.
    /// </exception>
    public ConvertOutcome Convert(string workId, string? fileId, bool replace, CancellationToken cancellation)
    {
        if (!WorkLookup.LooksLikeId(workId))
        {
            throw new LibraryException($"'{workId}' is not the ID of a work");
        }

        var work = Find(workId);
        string? name = null;
        if (fileId is not null)
        {
            if (!FileIds.TryParse(fileId, out _, out _))
            {
                throw new LibraryException($"'{fileId}' is not the ID of a file (the ID of a work, a tilde and the file name)");
            }

            var file = ResolveFile(fileId);
            if (file.Work != work.Id)
            {
                throw new LibraryException($"'{fileId}' is not a file of the work {work.Id}", LibraryErrorKind.Conflict);
            }

            name = file.Name;
        }

        lock (_writeGate)
        {
            try
            {
                var result = LibraryConvert.Convert(root, work, new ConvertOptions(name, replace), settings.Convert, converter ?? new ProcessConverter(), root, cancellation);
                return new ConvertOutcome(result.Work.ToString(), result.Source, result.File, result.Replaced);
            }
            catch (ConvertException e)
            {
                var kind = e.Kind == ConvertFailureKind.Failed
                    ? LibraryErrorKind.Failed
                    : e.Message.Contains("already has an EPUB", StringComparison.Ordinal) || e.Message.Contains("is identical to", StringComparison.Ordinal) ? LibraryErrorKind.Conflict : LibraryErrorKind.Invalid;
                throw new LibraryException(e.Message, kind);
            }
            catch (Exception e) when (e is LibraryEditException or SidecarException or JournalException or IndexException)
            {
                throw new LibraryException(e.Message, KindOf(e));
            }
        }
    }
}
