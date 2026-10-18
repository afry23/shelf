using System.Globalization;

namespace Shelf.Cli;

internal static class TextFormat
{
    /// <summary>A size in bytes as "812 B", "12.5 KB", "3.4 MB" or "1.2 GB" (units of 1024).</summary>
    public static string Size(long bytes) => bytes switch
    {
        < 1024 => $"{bytes.ToString(CultureInfo.InvariantCulture)} B",
        < 1024 * 1024 => $"{(bytes / 1024.0).ToString("0.#", CultureInfo.InvariantCulture)} KB",
        < 1024L * 1024 * 1024 => $"{(bytes / (1024.0 * 1024)).ToString("0.#", CultureInfo.InvariantCulture)} MB",
        _ => $"{(bytes / (1024.0 * 1024 * 1024)).ToString("0.#", CultureInfo.InvariantCulture)} GB",
    };

    public static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);

    public static string Plural(int count, string singular, string plural) =>
        $"{Number(count)} {(count == 1 ? singular : plural)}";
}
