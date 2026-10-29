# Slice m13-articles: Web Articles as Markdown

| | |
|---|---|
| **Slice ID** | `m13-articles` |
| **Branch** | `feat/articles` |
| **Milestone** | M13 – Web Articles |
| **PRD references** | F-ART-1 to F-ART-9, F-LIB-3, 9.4 |
| **Inputs** | `WorkSidecar`, `BookExtractor`, `LibraryImporter`, `HttpGetter`, `FetchCommand` |

---

## 1. Scope

**In scope**

1. `WorkKind.Article` and the sidecar fields source URL, site, published, captured (F-ART-1).
2. Spike: choose the HTML parser, readability extraction and HTML-to-Markdown libraries; check Native AOT (ADR 0008) and licenses (ADR 0001). Done: SmartReader, ReverseMarkdown and AngleSharp, [ADR 0020](../adr/0020-article-extraction-libraries.md).
3. `ArticleExtractor` (main content, metadata from Open Graph / JSON-LD / `<meta>`) and `ImageCollector` (`srcset`, `data-src`, size filter, SHA-256 dedup) (F-ART-4, F-ART-5).
4. `shelf clip <url>` and `shelf clip --html <file>` (HTML and MHTML), REST `POST /works/clip`; duplicates by canonical URL (F-ART-2, F-ART-3, F-ART-6).
5. Markdown as an indexed format with a heading locator (`heading=<slug>`), `type:article`, `site:` (F-ART-7). A Markdown file outside a work is no book: `import` and `scan` skip it.
6. The work folder as a unit in organize, undo and delete (F-ART-9): an article is placed at `Site/Title (Year)` (or `Author/…`), keeps its file names, and its `images/` go along. Scan, reindex and import never take an image for a work (they read EPUB, PDF and the files a sidecar names).
7. `MarkdownReader.svelte` serving images from the library only (F-ART-8).

**Not in scope**

- Cookies, logins or any paywall bypass in shelf. Paywalled pages come in through `--html`.
- A browser extension or bookmarklet (backlog).
- Annotations on articles, and merging or splitting the files of an article.
- A MCP tool for clipping (the web reader and the CLI are the ways in for now).
- An own organize pattern for articles (`organize.article_pattern`); the place is fixed.

## 2. Feature Files

`Articles/ArticleClipHtml.feature`, `ArticleClipUrl.feature`, `ArticleSearch.feature`, `ArticleFolder.feature`; `Web/WebArticleClip.feature` (REST and the images), `Web/UiArticleClip.feature` ("Add article"), `Web/UiArticleReader.feature` (the reader, F-ART-8). Unit tests: `Shelf.Clip.Tests` (clipper, images, MHTML, addresses), `MarkdownExtractorTests`.

## 3. Verification

Complete BDD suite green, `npm run check`.
