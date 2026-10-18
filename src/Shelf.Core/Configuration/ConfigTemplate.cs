namespace Shelf.Core.Configuration;

/// <summary>The config file that <c>shelf init</c> writes: every setting as a commented default.</summary>
public static class ConfigTemplate
{
    public const string Library =
        """
        # Settings of this library. They override ~/.config/shelf/config.toml.
        # Remove the leading "# " of a line to change a setting.

        # Grouping of files into works (confidence thresholds, 0 to 1).
        # [grouping]
        # auto_merge = 0.9
        # review = 0.5
        # max_hamming = 5
        # auto_hamming = 3

        # How shelf organize names folders and files. Placeholders: {author_sort}, {author}, {title},
        # {year}, {series}, {series_index}, {language}, {first_tag}, {ext}. The last part must end with ".{ext}".
        # [organize]
        # pattern = "{author_sort}/{title} ({year})/{title}.{ext}"
        """ + "\n";
}
