using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Shelf.Core.Model;

/// <summary>Picks the highlights that come back on a day (F-ANN-10). The pick depends on the date only, so a day gives the same highlights.</summary>
public static class Resurface
{
    public const int DefaultCount = 3;
    public const int MaxCount = 100;

    /// <summary>The items in the order of the day, at most <paramref name="count"/> of them.</summary>
    public static IReadOnlyList<T> Pick<T>(IEnumerable<T> items, Func<T, string> key, DateOnly date, int count)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(key);
        var day = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        return [.. items
            .Select(item => (Item: item, Rank: Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(day + "/" + key(item)))), Key: key(item)))
            .OrderBy(x => x.Rank, StringComparer.Ordinal)
            .ThenBy(x => x.Key, StringComparer.Ordinal)
            .Take(count)
            .Select(x => x.Item)];
    }

    /// <summary>The date as <c>yyyy-MM-dd</c>.</summary>
    public static bool TryParseDate(string? text, out DateOnly date) =>
        DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
}
