# Slice m4-reader: The PDF and EPUB Reader, Deep Links and the Content Security Policy

| | |
|---|---|
| **Slice ID** | `m4-reader` |
| **Branch** | `feat/m4-library-core` (stacked: same branch as slices 1 to 7, separate commit) |
| **Milestone** | M4 – Web |
| **Roadmap** | M4 slice 8 |
| **PRD references** | F-RDR-1 (PDF reader on PDF.js: page navigation, zoom, text search, outline), F-RDR-2 (EPUB reader on foliate-js: reflow, font size, theme, contents, text search), F-RDR-3 (deep links `#page=N` and `#cfi=...`, hits jump there), acceptance criterion 5, 9.4 |
| **Inputs** | slices `m4-api-files-dupes` (files with range requests), `m4-ui-views` (the search that links into the reader), [ADR 0016](../adr/0016-reader-libraries.md) |

---

## 1. Scope

**In scope**

1. The reader at `/read/{fileId}`: a PDF reader (PDF.js) and an EPUB reader (foliate-js), chosen by the format of the file; loaded only when a book is opened (a separate chunk).
2. **PDF**: continuous pages, page number field with previous and next, zoom (in, out, fit width), search in the text with the number of matches and "next" and "previous", the outline.
3. **EPUB**: paginated reading with previous and next, larger and smaller text, light, dark and sepia, search with a list of hits, the table of contents.
4. **Deep links**: `/read/{fileId}#page=N` (PDF) and `/read/{fileId}#cfi=epubcfi(...)` (EPUB) open at that place; the address follows the reader (replaced, not pushed), so a copied address opens where one is.
5. **Acceptance criterion 5**: search, click a hit, the reader shows the place; for a PDF page and for an EPUB section.
6. **The Content Security Policy** (the reader renders other people's content): `script-src 'self'` and the rest, and a test that a script inside an EPUB does not run.
7. The assets that PDF.js loads at run time (fonts, character maps, color profiles, WebAssembly decoders) embedded with the UI, and the content types for them.
8. ADR 0016: license and maintenance of PDF.js and foliate-js.

**Out of scope**: reading progress (F-RDR-4, M5), the preferred format (F-RDR-5, M5), bookmarks (F-RDR-6, Could), annotations, MOBI/FB2/comics (foliate-js can read them; shelf only imports EPUB and PDF), text-to-speech, keyboard shortcuts (F-WEB-5, M5), printing.

## 2. Feature Files

All `@ui` (Chromium), tagged with the requirement.

- `Features/Web/UiReaderPdf.feature` (`@F-RDR-1 @F-RDR-3 @M4`): opens at page 1 or at the page of the address, next and previous change the page and the address, the page field, zoom, search (matches, next match, nothing found), the outline of a real paper (click goes to its page), a file that is not in the library, the way back to the book, no errors in the browser console.
- `Features/Web/UiReaderEpub.feature` (`@F-RDR-2 @F-RDR-3 @M4`): opens at the start, the contents (click goes to a chapter), next and previous, larger and smaller text, the three themes, search with hits that lead to the place, a deep link with a location, a script in the book does not run, no errors in the browser console.
- `Features/Web/UiAcceptance.feature` (`@AC-5 @F-RDR-3 @M4`): from the search to the page of a PDF, from the search to the section of an EPUB, from the table of contents of the book page into the reader, a reader address copied and opened in a new browser.
- `Features/Web/WebUi.feature`: the policy header on every answer.

## 3. Design

- **One route, two readers.** `Reader.svelte` asks for the work (`GET /works/{id}`), finds the file by its ID, and loads `PdfReader` or `EpubReader` with a dynamic import. The libraries are only downloaded when a book is opened.
- **PDF: the library's viewer, not a hand-made one.** `PDFViewer`, `PDFLinkService`, `PDFFindController` and `EventBus` of `pdfjs-dist/web` give continuous scrolling, a text layer, links, zoom and find with highlights. The toolbar is ours (the names are in the tests): `Previous page`, `Next page`, the field `Page`, `Zoom in`, `Zoom out`, `Fit width`, `Search in this book` with `Next match` and `Previous match`, `Contents`. The file is opened by its address with the cookie (`withCredentials`) and range requests, so a large PDF shows its first page before it is loaded (ADR 0007, slice 4).
- **EPUB: `<foliate-view>`.** The file (a few MB) is fetched as a blob and opened; `relocate` events give the location and the progress, `goTo(cfi)` the deep link, `search()` the hits. The paginator's styles are set from our tokens: font size by the percentage of the text, the theme by the colors of the page inside its frames. The contents come from `view.book.toc`.
- **The address is the state.** The hash is read when the reader opens and written (`replaceState`) on every move: `#page=N` for a PDF, `#cfi=epubcfi(...)` for an EPUB. The reader also follows the hash when the address changes while it is open.
- **Static assets of PDF.js.** `standard_fonts`, `cmaps`, `iccs` and `wasm` are copied by the Vite config into `dist/pdfjs/` and passed to `getDocument` (`standardFontDataUrl`, `cMapUrl`, `iccUrl`, `wasmUrl`). The server serves unknown extensions as `application/octet-stream` (`.bcmap`, `.pfb`), so the embedded folder needs no list of types; the worker is a module (`.mjs`, `text/javascript`).
- **Content Security Policy** on every answer:

  ```
  default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data: blob:;
  font-src 'self' data: blob:; media-src 'self' blob:; connect-src 'self'; worker-src 'self' blob:;
  frame-src 'self' blob:; object-src 'none'; base-uri 'none'; form-action 'self'; frame-ancestors 'self'
  ```

  This is not decoration: foliate-js says in its README that it renders the scripts of a book from `blob:` URLs of the same origin and that a policy blocking all scripts except `'self'` is **imperative**. A script inside a blob frame inherits this policy, and `blob:` is not a script source. `'unsafe-inline'` is for styles only (foliate-js and PDF.js write style elements); it does not allow a script. `frame-ancestors 'self'` replaces nothing of `X-Frame-Options` (both stay).
- **Secure context.** foliate-js deobfuscates fonts of some EPUBs with `crypto.subtle`, which only exists in a secure context (HTTPS or `localhost`/`127.0.0.1`). On a LAN address over plain HTTP such a font does not load and the book falls back to another font. Recorded as a known limit; the book still reads.
- **The book is not trusted.** An EPUB can carry scripts, remote images and links. Scripts are blocked by the policy (and tested), remote images by `img-src`, and links that leave the book are opened by foliate-js through `isExternal` (not followed inside the frame).
- **Dark mode.** PDF pages stay white (inverting a page breaks images); the frame around them follows the system. EPUB has its own theme choice (Light, Dark, Sepia) that starts from the system.

- **The pages of a book are frames, and foliate-js hides them.** `<foliate-view>` attaches a *closed* shadow root, so no selector reaches the frames of the sections. The browser steps read the text of every frame of the page (`Page.Frames`) instead; that is also what a person reads.
- **Inputs wait for the book.** The search field and the page field are disabled until the document is open (found by a scenario that typed before the reader was ready: the first key presses were lost). A real person can type as fast as a test does.
- **A click on "Next page" that comes too early is repeated.** The paginator of foliate-js ignores a turn while it still settles the last one (about 100 ms after a section changed). The button repeats the turn up to four times until the position moves; without it a quick "next, previous" did nothing.
- **Fit width follows the room.** A `ResizeObserver` re-fits the PDF when its panel opens or the window changes; without it the contents panel cut the page (found in a screenshot).
- **`.mjs?url`** gives the worker's address (hashed name, module type); the Vite plugin copies the PDF.js data into `dist/pdfjs/` for the build and serves it from `node_modules` in `npm run dev`.

## 4. Native AOT

**Result (`osx-arm64`):** 0 warnings, 42 MiB (36 before; the UI grew from 0.3 MB to 6.3 MB). From a copy of the binary in an empty folder with `PATH=/usr/bin:/bin` (no `node`): the page carries the policy header; `/pdfjs/wasm/openjpeg.wasm` is `application/wasm`, a `.pfb` font and a `.bcmap` are `application/octet-stream`, the worker (`pdf.worker.min-*.mjs`, 1.26 MB) is `text/javascript`; a reader address with `Accept: text/html` gives the page; a range request on the PDF answers `206` (`bytes 0-1023/153667`), which is how PDF.js reads it. `linux-x64` is checked by the `aot` job.

## 5. Tests (result)

37 browser scenarios (PDF reader 15, EPUB reader 14, acceptance criterion 5: 8) and 16 for the policy and the files of PDF.js. A helper step checks that the browser reported no errors (that is how a blocked resource, a worker that does not start or a policy that is too strict shows up), and one scenario checks the opposite: the script inside an EPUB is blocked and the browser says why ("Content Security Policy"). The whole suite is green (2413 tests).

## 6. Open Questions

- **Size.** PDF.js adds about 1 MB of script and 1 MB of worker plus the assets (about 5 MB); the binary grows by about that much. Measured and recorded in the results; trimming `cmaps` is possible if the size matters.
- **foliate-js is "not stable"** (its README) and has one maintainer; the version is pinned exactly and ADR 0016 names the re-check triggers and the fallback (vendoring a copy).
