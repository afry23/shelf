using System.Reflection;

namespace Shelf.TestData.Content;

/// <summary>Hand-authored word lists, embedded as resources (no third-party frequency lists or texts).</summary>
internal static class Vocabulary
{
    private static readonly Lazy<IReadOnlyList<string>> English = new(() => Load("en.txt"));
    private static readonly Lazy<IReadOnlyList<string>> German = new(() => Load("de.txt"));
    private static readonly Lazy<IReadOnlyList<string>> TopicsEnglish = new(() => Load("topics-en.txt"));
    private static readonly Lazy<IReadOnlyList<string>> TopicsGerman = new(() => Load("topics-de.txt"));
    private static readonly Lazy<IReadOnlyList<string>> Given = new(() => Load("given-names.txt"));
    private static readonly Lazy<IReadOnlyList<string>> Family = new(() => Load("surnames.txt"));

    /// <summary>Words in rank order: the first entries are the most frequent function words.</summary>
    public static IReadOnlyList<string> Words(string language) => language == Languages.German ? German.Value : English.Value;

    public static IReadOnlyList<string> Topics(string language) => language == Languages.German ? TopicsGerman.Value : TopicsEnglish.Value;

    public static IReadOnlyList<string> GivenNames => Given.Value;

    public static IReadOnlyList<string> Surnames => Family.Value;

    private static string[] Load(string name)
    {
        var assembly = typeof(Vocabulary).Assembly;
        using var stream = assembly.GetManifestResourceStream("Vocabulary." + name)
            ?? throw new InvalidOperationException($"Embedded vocabulary '{name}' is missing.");
        using var reader = new StreamReader(stream, System.Text.Encoding.UTF8);
        var lines = new List<string>();
        while (reader.ReadLine() is { } line)
        {
            var trimmed = line.Trim();
            if (trimmed.Length > 0)
            {
                lines.Add(trimmed);
            }
        }

        return [.. lines];
    }
}

internal static class Languages
{
    public const string English = "en";
    public const string German = "de";
}
