# ADR 0016: PDF.js and foliate-js for the reader, licenses and maintenance

| | |
|---|---|
| **Status** | Accepted: `pdfjs-dist` 6.4.299 adopted; `foliate-js` 1.0.1 adopted **with a maintenance flag** and conditions (exact pin, a content security policy that blocks scripts, tests) |
| **Date** | 2026-10-18 |
| **Scope** | PRD F-RDR-1 (PDF.js), F-RDR-2 (foliate-js), 10.2 (rows "PDF reader", "EPUB reader"), ADR 0001 method for the two libraries that the UI ships to the browser |
| **Roadmap** | M4 slice `m4-reader` |
| **Inputs** | [ADR 0001](0001-dependency-licenses-and-maintenance.md) (policy and criteria), [ADR 0009](0009-frontend-toolchain-licenses-and-maintenance.md), the READMEs of both libraries, the npm registry and the GitHub API (read on 2026-10-18) |

## Context

Unlike the toolchain of ADR 0009, these two libraries **are shipped**: their code is part of the bundle that `shelf serve` sends to the browser (the UI is embedded in the binary, F-WEB-3). The license policy of ADR 0001 therefore applies fully (GPL-3.0-or-later for shelf, GPL-3.0-compatible dependencies; maintenance flag for no release in 12 months, no commit in 6 months, or a single maintainer without an organization). The PRD named both libraries in table 10.2 before this check; this ADR is the check. It is an engineering assessment, not legal advice.

## Summary

| # | Candidate | Version (date) | License | GPL-3.0 compatible | Maintenance | Role | Verdict |
|---|---|---|---|---|---|---|---|
| R1 | `pdfjs-dist` (PDF.js) | 6.4.299 (2026-10-03) | Apache-2.0 | yes (ADR 0001 S1) | ok: Mozilla (`mozilla/pdf.js`), releases every few weeks (6.3.289 on 2026-08-29, 6.4.299 on 2026-10-03) | PDF rendering, text layer, find, outline | adopt |
| R2 | `foliate-js` | 1.0.1 (npm, 2025-04-21); `main` has a commit from 2026-05-01 | MIT | yes | **flag**: one maintainer (`johnfactotum`: 268 of about 290 commits; others 12, 6, 2) and no organization; the last npm release is 18 months old; 1 commit in the last six months; its README says the library is "*not* stable. Expect it to break and the API to change at any time" | EPUB rendering (pagination, CFI, search, contents) | adopt with the flag and the conditions below |
| R3 | `zip.js` (vendored inside foliate-js as `vendor/zip.js`) | bundled, minified | BSD-3-Clause upstream (`gildas-lormeau/zip.js`) | yes (FSF "Modified BSD") | not assessed | reads the ZIP of an EPUB with random access | adopt as part of R2 |
| R4 | `fflate` (vendored as `vendor/fflate.js`) | bundled, minified | MIT upstream (`101arrowz/fflate`) | yes | not assessed | zlib for fonts of KF8 files; not used for EPUB | adopt as part of R2 |

The licenses of R3 and R4 were taken from the upstream projects, not from the minified files (they carry no header at the top); that is a gap, recorded here.

## Candidate notes

### R1. `pdfjs-dist`

- Apache-2.0 (registry metadata, `package.json`). Engines: Node.js `>=22.13 || >=24` (a build-time matter only; Node.js 26 is pinned, ADR 0009). The package is 35 MB unpacked because it also carries the legacy build, the sample viewer and type definitions; the bundle takes the modern build only.
- What ships: `build/pdf.mjs` (the library, about 1 MB), `build/pdf.worker.min.mjs` (the worker), `web/pdf_viewer.mjs` and `.css` (the viewer components: `PDFViewer`, `PDFLinkService`, `PDFFindController`, `EventBus`), and the static data that PDF.js fetches at run time: `standard_fonts`, `cmaps`, `iccs` and `wasm`. The viewer's own application (`web/viewer.html`) is not used.
- Optional dependency `@napi-rs/canvas` is for Node.js rendering; it is not part of the browser bundle.
- Risk: PDF.js parses PDFs in the browser, in a worker; a hostile PDF is the usual browser risk and the policy (below) keeps it from reaching anything but the page's own origin.

### R2. `foliate-js`

- MIT. Pure JavaScript, native ES modules, no hard dependencies, 452 KB unpacked. It is also the library of the e-book reader Foliate (GNOME), which gives it a user base; the repository has about 1,100 stars.
- **The maintenance flag is real and is accepted knowingly.** The PRD chose it (table 10.2), the alternatives (epub.js, Readium's navigator) were not evaluated here, and replacing the engine later is bounded: the EPUB reader is one component (`EpubReader.svelte`) behind a route. The README recommends a git submodule "so that you can easily update it"; the project instead pins the npm package `1.0.1` exactly (integrity hash in the lock file), which has no changes since April 2025 but is reproducible, and is a deliberate choice: `main` has later fixes (for example `isExternal` for FB2 links) but also later, unreleased and untested API changes.
- **Security (from its README, and why the policy is part of this decision).** EPUB books can contain scripts. foliate-js renders sections as `blob:` URLs of the same origin and cannot sandbox them; the README says that a content security policy that blocks all scripts but `'self'` is imperative and that the library must not be used without one. shelf sends such a policy on every answer and a scenario proves that a script inside an EPUB does not run.
- **Secure context.** Fonts that an EPUB obfuscates (the IDPF algorithm) need SHA-1 through Web Crypto, which is only available over HTTPS or on `localhost`/`127.0.0.1`. Over plain HTTP on a LAN address such a font is not loaded; the text is still readable in a fallback font.

## Decision

- **Adopt R1 and R2 (with R3, R4).** Versions pinned exactly in `src/Shelf.Web.Ui/package.json` (`pdfjs-dist` 6.4.299, `foliate-js` 1.0.1) and in the lock file.
- **Conditions for R2:** (1) the content security policy of the slice plan on every response; (2) the scenario "a script in the book does not run"; (3) the formats that shelf does not import (MOBI, FB2, comic books) stay unused; their modules are in the bundle only as lazily loaded chunks of `view.js`; (4) the version is not updated without running the reader scenarios.
- **Fallback if foliate-js becomes unusable** (a browser change breaks it, a security problem without a fix): vendor a copy of the last working state into the repository (MIT allows it) and fix it there, or replace the engine behind `EpubReader.svelte`. The scenarios of the EPUB reader are the contract for either.

## Consequences

- **Bundle:** PDF.js and foliate-js are loaded only when a book is opened (dynamic imports); the library view does not pay for them. The binary grows by the size of the reader chunks and the PDF.js assets (measured in the slice results).
- **`THIRD-PARTY-NOTICES` (M5)** needs the Apache-2.0 notice of PDF.js (and its NOTICE text if there is one), the MIT notice of foliate-js, and the notices of the vendored zip.js (BSD-3-Clause) and fflate (MIT), together with the Svelte runtime (ADR 0009).
- **The tree grew.** The lock file has 87 entries (ADR 0009 counted 85 for the spike's tree; `svelte-check` and the two libraries are the difference); licenses by metadata: MIT 67, Apache-2.0 5, MPL-2.0 12 (the platform packages of lightningcss, ADR 0009 T7), ISC 1, BSD-3-Clause 1.
- **Re-check triggers:** a new major of `pdfjs-dist` (its API for the viewer components changed between versions 4 and 6), any release or a long silence of foliate-js (a year without commits), a security report for either, a Chromium change that breaks `blob:` frames or module workers, a requirement for EPUB features that foliate-js lacks (fixed-layout annotations, media overlays).
- **Not assessed:** the maintenance of the transitive packages, the license files of the vendored bundles, how PDF.js handles malformed fonts and images beyond what its test suite says.

## Addendum: the data files of PDF.js (2026-10-18, found while embedding them)

PDF.js fetches fonts, character maps, color profiles and WebAssembly decoders at run time (`standardFontDataUrl`, `cMapUrl`, `iccUrl`, `wasmUrl`). shelf embeds them with the UI (`dist/pdfjs/`, about 3.9 MB, together with their license files) so that the reader needs no network. They are data that ships **inside the binary**, so their licenses count (ADR 0001 method; read from the `LICENSE_*` files of `pdfjs-dist` 6.4.299):

| Files | License (from the file) | Note |
|---|---|---|
| `standard_fonts/Foxit*.pfb` | BSD-3-Clause ("Copyright 2014 PDFium Authors") | compatible |
| `standard_fonts/LiberationSans-*.ttf` | **GNU General Public License v.2 with exceptions** (Red Hat, Liberation Font Software; the old license agreement text) | a GPL-2.0-only font under a font exception, shipped as separate data files next to a GPL-3.0-or-later program (an aggregate, not a combined work); **to be confirmed for the notices** |
| `wasm/openjpeg.wasm` and its fallback | BSD-2-Clause (OpenJPEG, UCL/Macq) and the BSD-licensed Mozilla glue | compatible |
| `wasm/jbig2.wasm` and its fallback | BSD-3-Clause (PDFium authors) and Apache-2.0 (Mozilla) | compatible |
| `wasm/` qcms | MIT (Mozilla, Marti Maria) | compatible |
| `iccs/*.icc` | CC0-1.0 | compatible |
| `cmaps/*.bcmap` | `cmaps/LICENSE` (Adobe's BSD-style license; not opened in this check) | assumed compatible, **not read** |

- **Decision:** ship them. The Foxit and Liberation fonts are what PDF.js uses for the 14 standard PDF fonts when a PDF does not embed them (common in older PDFs); without them such a PDF renders with a browser fallback and the text layer is misaligned. The notices file (M5) must name all of the above and carry the license texts; the Liberation line is the one to settle first.
- **If the Liberation license cannot be accepted:** drop the four `LiberationSans-*.ttf` files from the copy in `vite.config.ts`; PDF.js then falls back to the Foxit set for these fonts, with a measurable loss for some documents. The scenarios on the sample PDF do not depend on them.
- **Size:** the PDF.js chunk is 603 KB (gzip 182 KB), its worker 1.26 MB, its CSS 230 KB; with the foliate-js chunks and the assets the `dist` folder is 6.3 MB and the Native AOT binary grew from 36 to 42 MiB.
