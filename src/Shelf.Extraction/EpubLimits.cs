using System.IO.Compression;

namespace Shelf.Extraction;

/// <summary>
/// Refuses an EPUB whose archive declares more data than the limits allow, before any parser reads it
/// (ADR 0005, R-E13: a 256 KB file made the string reader of VersOne.Epub use 2.8 GiB).
/// </summary>
internal static class EpubLimits
{
    public static void Check(string path, ExtractionOptions options)
    {
        // Not a ZIP: InvalidDataException, classified as NotAContainer by the caller.
        using var archive = ZipFile.OpenRead(path);
        long total = 0;
        foreach (var entry in archive.Entries)
        {
            if (entry.Length > options.MaxEntryBytes)
            {
                throw new ExtractionException(
                    $"{path}: too-large: entry '{entry.FullName}' is {entry.Length} bytes unpacked, the limit is {options.MaxEntryBytes}",
                    ExtractionErrorKind.TooLarge,
                    path);
            }

            total += entry.Length;
            if (total > options.MaxTotalBytes)
            {
                throw new ExtractionException(
                    $"{path}: too-large: the archive unpacks to more than {options.MaxTotalBytes} bytes",
                    ExtractionErrorKind.TooLarge,
                    path);
            }
        }
    }
}
