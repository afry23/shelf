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
        # Probable duplicates across works (shelf dupes --probable): text fingerprint distance up to
        # probable_hamming, or similarity of the vectors from probable_cosine on.
        # probable_hamming = 3
        # probable_cosine = 0.95

        # How shelf organize names folders and files. Placeholders: {author_sort}, {author}, {title},
        # {year}, {series}, {series_index}, {language}, {first_tag}, {cite_key}, {ext}. The last part must end with ".{ext}".
        # [organize]
        # pattern = "{author_sort}/{title} ({year})/{title}.{ext}"
        # Papers have a pattern of their own; {cite_key} is the title if a paper has no key.
        # paper_pattern = "papers/{year}/{cite_key}/{cite_key}.{ext}"

        # shelf fetch looks for a free PDF of a paper at Unpaywall if arXiv has none. Unpaywall asks for an
        # e-mail address with every request; without one it is not asked. Only shelf fetch and "Add paper" go online.
        # [fetch]
        # unpaywall_email = "you@example.org"

        # The reader of the web interface: the format that "Read" opens when a work has an EPUB and a PDF.
        # [reader]
        # preferred_format = "epub"

        # The web interface: how many works a page of the library view holds (1 to 100).
        # [web]
        # page_size = 20
        """ + "\n";
}
