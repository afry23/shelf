# Test fixtures

Small, documented e-book files for parser and import tests (PRD 10.1). Every file is listed in
[`manifest.json`](manifest.json) with provenance, license basis, size and SHA-256. The `Fixtures.feature`
scenarios in `tests/Shelf.Specs` check that manifest and directory agree, that hashes and the size budget hold,
and that derived files are reproducible. Plan: [`docs/slices/m0-fixtures.md`](../../docs/slices/m0-fixtures.md).

Budget: total at most 5 MiB (5,242,880 bytes), per file at most 1.5 MiB (1,572,864 bytes). Hosting is plain git
(binary files, see `/.gitattributes`); there is no git-lfs and no download script.

## Files

| File | Origin | Work | Traits | Size (bytes) |
|---|---|---|---|---|
| `upstream/gutenberg/pg201-flatland.epub3.epub` | Project Gutenberg #201 | flatland | epub3, cover, language-en | 301,494 |
| `upstream/gutenberg/pg201-flatland.epub2.epub` | Project Gutenberg #201 | flatland | epub2, cover, language-en | 301,854 |
| `upstream/gutenberg/pg22367-die-verwandlung.epub3.epub` | Project Gutenberg #22367 | kafka-die-verwandlung | epub3, cover, language-de | 99,771 |
| `upstream/standardebooks/edwin-a-abbott_flatland.epub` | Standard Ebooks | flatland | epub3, cover, language-en | 1,006,174 |
| `upstream/pdf/arxiv-2403.02174v1.pdf` | arXiv 2403.02174v1 | arxiv-2403.02174 | pdf-text-layer, pdf-info, pdf-xmp | 153,667 |
| `derived/epub3-isbn-series.epub` | generator | d1 | epub3, isbn, series, no-cover | see manifest |
| `derived/Event Streams in Practice.pdf` | generator | d1 | pdf-text-layer, pdf-info, pdf-xmp, isbn | see manifest |
| `derived/epub2-isbn-cover.epub` | generator | d2 | epub2, isbn, series, cover, language-de, non-ascii-title | see manifest |
| `derived/Jane Doe - Quiet Systems (2004).epub` | generator | d3 | epub3, empty-metadata, no-cover | see manifest |
| `derived/Jane Doe - Quiet Systems (2004).pdf` | generator | d3 | pdf-text-layer, pdf-no-metadata | see manifest |
| `derived/malformed-opf.epub` | generator | d4 | malformed-opf | see manifest |
| `derived/not-a-zip.epub` | generator | d5 | not-a-zip | see manifest |
| `derived/scanned-page.pdf` | generator | d6 | pdf-image-only, pdf-info | see manifest |

Total about 1.9 MB. The same work in several files (`flatland`: EPUB 2, EPUB 3 and a second EPUB 3 from another
source; `d1`: EPUB and PDF; `d3`: EPUB and PDF) is intentional: it is what grouping and duplicate tests need.

## Rules

**Upstream files are committed byte-for-byte as downloaded.** Their `modifications` list in the manifest is empty.
Never edit one; to change a fixture, replace it by a new download and update the manifest (path, size, SHA-256,
retrieval date).

**Public-domain basis.** An upstream file is only added when both hold, recorded per file in the manifest
(`publicDomain.us`, `publicDomain.lifePlus70`):

1. published in or before 1930 (public domain in the USA on 2026-01-01), and
2. every author, translator and illustrator died in or before 1955 (life + 70 years on 2026-01-01, e.g. EU).

The one exception is the arXiv paper, which is not public domain by age but dedicated by its author to the public
domain with CC0 1.0 (see below). Other jurisdictions (for example life + 100) are not checked. This is not legal
advice.

**Derived files** are produced by `dotnet run --project tests/Shelf.TestData -- fixtures --out tests/fixtures/derived`
from synthetic text (no third-party content) and are licensed GPL-3.0-or-later like the repository. They carry
made-up ISBNs with valid check digits (`9780000000019`, `9780000000026`) that may collide with real books; never use
them for online lookups.

**Adding or refreshing a fixture.** Download the single file by hand (one request, no scripts), verify the license
claims at the source and quote them here with URL and retrieval date, check the public-domain basis, copy the file
unmodified, add a manifest entry, and keep the budget. Do not crawl any of the sources below.

## Provenance and license quotes

All retrieved on 2026-10-15 with one request per page or file.

### Project Gutenberg (files `upstream/gutenberg/*`)

- Ebook page https://www.gutenberg.org/ebooks/201: "Author: Abbott, Edwin Abbott, 1838-1926", "Title: Flatland: A
  Romance of Many Dimensions", "Copyright: Public domain in the USA." Files: `201.epub3.images` (EPUB3) and
  `201.epub.images` ("EPUB (older e-readers)", EPUB 2.0), both with cover.
- Ebook page https://www.gutenberg.org/ebooks/22367: "Author: Kafka, Franz, 1883-1924", "Title: Die Verwandlung",
  "Language: German", "Copyright: Public domain in the USA." File: `22367.epub3.images`. Publication year 1915:
  Wikipedia (https://en.wikipedia.org/wiki/The_Metamorphosis, REST summary): "a novella by Franz Kafka published in
  1915".
- License https://www.gutenberg.org/policy/license.html: "These books are not restricted by copyright in the United
  States and anyone located in the United States - including Project Gutenberg and you - may read and distribute
  them. If you don't live in the United States you'll have to check the laws of the country you live in before
  downloading and distributing our ebooks." On the trademark: "you may only distribute verbatim copies of the
  ebooks. No changes are allowed to the ebook contents. (Though reformatting the ebook to a different file format is
  considered okay)." "If you charge money for the copies you distribute, you have to pay royalties to Project
  Gutenberg." "You may still distribute the ebooks if you strip the Project Gutenberg license and all references to
  Project Gutenberg." Full terms: Section 1.B, "'Project Gutenberg' is a registered trademark."
- Permission FAQ https://www.gutenberg.org/policy/permission.html: "No permission is needed for non-commercial use.
  So, for example, you can freely redistribute any eBook, anywhere, any time, with or without the 'Project
  Gutenberg' trademark included." "Note that if you are not in the US, you must confirm yourself whether an item is
  free to redistribute where you are."
- Robot policy https://www.gutenberg.org/policy/robot_access.html: "The Project Gutenberg website is intended for
  human users only. Any perceived use of automated tools to access the Project Gutenberg website will result in a
  temporary or permanent block of your IP address." Nothing in this repository downloads from Project Gutenberg;
  the three files were fetched once each.

Consequences for shelf: the files are redistributed free of charge, unmodified, with the Project Gutenberg license
text they contain (verified: each file contains the "Project Gutenberg License" text). They are never edited while
keeping the Project Gutenberg name. Mentioning "Project Gutenberg" here as the factual source is a reference, not use
of the trademark on a product. The generated PG cover image of #201 is part of the unmodified file.

### Standard Ebooks (`upstream/standardebooks/edwin-a-abbott_flatland.epub`)

- Ebook page https://standardebooks.org/ebooks/edwin-a-abbott/flatland ("This ebook is thought to be free of
  copyright restrictions in the United States. It may still be under copyright in other countries."). File: the
  "Compatible epub", fetched with the `?source=download` parameter that the site's own download link uses (without
  it the server returns an HTML "Your Download Has Started" page). The page carries a hidden link "Following this
  link will ban your IP for 24 hours"; it was not followed.
- Inside the file (`epub/content.opf` `dc:rights` and `epub/text/uncopyright.xhtml`): "The source text and artwork
  in this ebook are believed to be in the United States public domain ... The creators of, and contributors to,
  this ebook dedicate their contributions to the worldwide public domain via the terms in the CC0 1.0 Universal
  Public Domain Dedication." and "Regardless, the contributors to this ebook release their contributions under the
  terms in the CC0 1.0 Universal Public Domain Dedication ... This dedication doesn't change the copyright status
  of the source text or artwork."
- Colophon: "Flatland was published in 1884 by Edwin A. Abbott." "The cover page is adapted from Rotterdam, a
  painting completed in 1914 by Edward Wadsworth." Wadsworth died 21 June 1949 (English Wikipedia, infobox
  `death_date`).
- https://standardebooks.org/about footer: "Content produced by or for Standard Ebooks LLC is dedicated to the
  public domain via the CC0 1.0 Universal Public Domain Dedication."
- Not verified: whether the Standard Ebooks name or logo (`epub/images/logo.png` is part of the file) carries a
  trademark restriction; no such statement was found on the pages above. The file is committed unmodified, which
  is what the CC0 dedication of the production work allows.

### arXiv (`upstream/pdf/arxiv-2403.02174v1.pdf`)

- Abstract page https://arxiv.org/abs/2403.02174: "arXiv:2403.02174 (math) [Submitted on 4 Mar 2024] Title: On
  Hilbert's 16th Problem, Authors: Lars Andersen", `[v1] Mon, 4 Mar 2024 16:20:49 UTC (9 KB)`. The page's license
  link (`abs-license`) points to http://creativecommons.org/publicdomain/zero/1.0/ (CC0 1.0 Universal). The
  OAI-PMH record (https://export.arxiv.org/oai2, set math, 2024-03-05) lists the same `license` URL; the paper was
  found there.
- File: https://arxiv.org/pdf/2403.02174v1, 153,667 bytes, PDF 1.4, 7 pages, producer "GPL Ghostscript 10.01.2",
  creator "LaTeX with hyperref", with an XMP metadata stream; the Info title and author are empty, so the manifest
  title and author come from the abstract page.
- The arXiv watermark stamp on the PDF is kept. The CC0 dedication is the author's; the manifest records "not
  applicable" for the age-based public-domain fields.

### Not used

- Wikisource (CC BY-SA, ws-export output changes over time) and any scan that is not CC0 or public domain by age.
- A real image-only scan PDF: public scans are mostly larger than 10 MB and carry OCR layers; `scanned-page.pdf` is
  derived instead.
