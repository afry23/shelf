using System.Text;

namespace Shelf.TestData.Content;

internal readonly record struct AuthorName(string Given, string Family)
{
    public string Display => Given + " " + Family;
}

internal enum AuthorVariant
{
    Canonical,
    SurnameFirst,
    Initials,
    NoDiacritics,
}

internal static class NameVariants
{
    private static readonly Dictionary<char, string> Folding = new()
    {
        ['ä'] = "a",
        ['ö'] = "o",
        ['ü'] = "u",
        ['Ä'] = "A",
        ['Ö'] = "O",
        ['Ü'] = "U",
        ['ß'] = "ss",
        ['å'] = "a",
        ['Å'] = "A",
        ['ø'] = "o",
        ['Ø'] = "O",
        ['æ'] = "ae",
        ['Æ'] = "Ae",
        ['é'] = "e",
        ['è'] = "e",
        ['ê'] = "e",
        ['ë'] = "e",
        ['É'] = "E",
        ['È'] = "E",
        ['Ê'] = "E",
        ['Ë'] = "E",
        ['á'] = "a",
        ['à'] = "a",
        ['â'] = "a",
        ['ã'] = "a",
        ['Á'] = "A",
        ['À'] = "A",
        ['Â'] = "A",
        ['Ã'] = "A",
        ['í'] = "i",
        ['ì'] = "i",
        ['î'] = "i",
        ['ï'] = "i",
        ['Í'] = "I",
        ['Ì'] = "I",
        ['Î'] = "I",
        ['Ï'] = "I",
        ['ó'] = "o",
        ['ò'] = "o",
        ['ô'] = "o",
        ['õ'] = "o",
        ['Ó'] = "O",
        ['Ò'] = "O",
        ['Ô'] = "O",
        ['Õ'] = "O",
        ['ú'] = "u",
        ['ù'] = "u",
        ['û'] = "u",
        ['Ú'] = "U",
        ['Ù'] = "U",
        ['Û'] = "U",
        ['ñ'] = "n",
        ['Ñ'] = "N",
        ['ç'] = "c",
        ['Ç'] = "C",
        ['ý'] = "y",
        ['ÿ'] = "y",
        ['Ý'] = "Y",
        ['ð'] = "d",
        ['Ð'] = "D",
        ['þ'] = "th",
        ['Þ'] = "Th",
    };

    /// <summary>"José Núñez" becomes "Jose Nunez". Characters outside the table are kept.</summary>
    public static string Fold(string text)
    {
        var builder = new StringBuilder(text.Length);
        foreach (var c in text)
        {
            builder.Append(Folding.TryGetValue(c, out var replacement) ? replacement : c.ToString());
        }

        return builder.ToString();
    }

    public static bool HasDiacritics(string text) => text.Any(Folding.ContainsKey);

    public static string Render(AuthorName author, AuthorVariant variant) => variant switch
    {
        AuthorVariant.SurnameFirst => author.Family + ", " + author.Given,
        AuthorVariant.Initials => string.Concat(author.Given.AsSpan(0, 1), ". ", author.Family),
        AuthorVariant.NoDiacritics => Fold(author.Display),
        _ => author.Display,
    };

    /// <summary>The "file-as" form that EPUB metadata carries.</summary>
    public static string FileAs(AuthorName author) => author.Family + ", " + author.Given;
}
