# ADR 0020: Libraries for extracting web articles

| | |
|---|---|
| **Status** | Accepted for `osx-arm64`: Native AOT publish with the three libraries has 0 `IL*`/`AOT*` warnings and runs; `linux-x64` not verified |
| **Date** | 2026-10-29 |
| **Scope** | PRD 6.10 (F-ART-2 to F-ART-5), 9.2 (single Native AOT binary), 10.2 |
| **Roadmap** | M13 |
| **Inputs** | [ADR 0001](0001-dependency-licenses-and-maintenance.md), [ADR 0008](0008-combined-aot-binary.md), slice plan `docs/slices/m13-articles.md` |

## Context

`shelf clip` needs three things: parse HTML, pick the main content of a page (drop navigation, ads, comments), and write that content as Markdown. The result must run in the single Native AOT binary (ADR 0008) and the licenses must be GPL-3.0-compatible (ADR 0001).

## Options

- **A. SmartReader + ReverseMarkdown on AngleSharp.** SmartReader is a port of Mozilla Readability; both libraries use AngleSharp as their DOM, so there is one HTML parser in the binary.
- **B. HtmlAgilityPack + own extraction.** Lenient parser, but no readability scoring and no Markdown writer: both would be our code.
- **C. Own extractor on AngleSharp only.** Full control, but the scoring heuristics of Readability are years of tuning on real pages.

## Facts (retrieved 2026-10-29)

| Library | Version (date) | License | GPL-3.0 | Maintenance |
|---|---|---|---|---|
| AngleSharp | 1.8.6 | MIT | yes | last push 2026-10-28, organization project |
| SmartReader | 0.11.1 (2026-07-25) | Apache-2.0 | yes | last push 2026-07-25, 3 open issues; flag: single maintainer, pre-1.0 |
| ReverseMarkdown | 6.3.0 (2026-10-21) | MIT | yes | last push 2026-10-21, 0 open issues; flag: single maintainer |

SmartReader pins AngleSharp 1.5.2 and ReverseMarkdown AngleSharp 1.5.2; the central package version 1.8.6 wins without a conflict.

## Spike

A Native AOT console project (`PublishAot`, `TrimmerSingleWarn=false`, `osx-arm64`, .NET SDK 10.0.403) that reads an HTML file, runs `Reader.GetArticle()` and `Converter.Convert()`.

| Check | Result |
|---|---|
| AOT publish | 0 `IL*`/`AOT*` warnings; two `CS0618` (obsolete `Config.UnknownTags`, `Config.RemoveComments`; use `Tags.Unknown`, `Formatting.RemoveComments`) |
| Binary size | 8.3 MB |
| Small test page | title, author, site name and date read; headings, lists, block quote, code block and image become Markdown |
| Real page (martinfowler.com article, 86 kB) | `IsReadable=true`, title and author right, 6 images, headings and links kept |

Findings to carry into the product:

1. **`Article.IsReadable` is false for a short page** (the test page). It is not used as the gate; an empty `Content` (or no text) means "no article found" (F-ART scenario).
2. **The reader needs the real page URL as base**, otherwise relative image and link targets resolve against the wrong host. For `--html` without `--url` relative targets stay relative and images must come from the saved page itself.
3. **`PublicationDate` is converted to local time.** The published date is read as a date from the metadata, not from that property.
4. **A comment block can survive on a very short page** (the test page kept "First! Great post."). On real pages the comment section was dropped.
5. **A figure caption can replace the alt text** of the image in the Markdown. Accepted; the caption is the better description.
6. **No lazy-load or `srcset` handling was verified.** The image collector (F-ART-5) works on the AngleSharp DOM before the reader runs, so it does not depend on the reader for these.

## Decision

Option A. SmartReader 0.11.1, ReverseMarkdown 6.3.0 and AngleSharp 1.8.6 go into `Directory.Packages.props`. The facts above are their entry in the dependency check; ADR 0001 keeps the state of 2026-10-12. If a library turns out to break on the other AOT target (`linux-x64`), the fallback is option C for the failing part.

## Consequences

- A new project `Shelf.Clip` holds the three package references and the article code; `Shelf.Library` references it.
- Two single-maintainer libraries are in the product; the extraction sits behind one class (`ArticleExtractor`), so a replacement touches one file.
- `linux-x64` stays "not verified", as for ADR 0008.
