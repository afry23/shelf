namespace Shelf.TestData.Content;

internal enum TitleVariant
{
    Canonical,
    EditionSuffix,
    LeadingArticle,
}

internal static class TitleVariants
{
    private static readonly string[] SuffixesEnglish = [", 2nd Edition", ", Third Edition", " (German Edition)", " (Revised Edition)"];
    private static readonly string[] SuffixesGerman = [", 2. Auflage", " (Deutsche Ausgabe)", ", Neuausgabe", " (3. überarbeitete Auflage)"];

    public static int SuffixCount(string language) => Suffixes(language).Length;

    /// <param name="choice">Index into the suffix list for <see cref="TitleVariant.EditionSuffix"/>; ignored otherwise.</param>
    public static string Apply(string canonical, string language, TitleVariant variant, int choice = 0) => variant switch
    {
        TitleVariant.EditionSuffix => canonical + Suffixes(language)[choice % Suffixes(language).Length],
        TitleVariant.LeadingArticle => (language == Languages.German ? "Die " : "The ") + canonical,
        _ => canonical,
    };

    public static bool HasNonAsciiLetter(string title) => title.Any(c => c > 127 && char.IsLetter(c));

    private static string[] Suffixes(string language) => language == Languages.German ? SuffixesGerman : SuffixesEnglish;
}
