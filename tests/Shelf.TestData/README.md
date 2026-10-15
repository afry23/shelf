# Shelf.TestData

Deterministic generator of a synthetic e-book library (and of the derived fixtures in `tests/fixtures/derived`).
Test infrastructure only: no project below `src/` references it (guard test in `Shelf.TestData.Tests`), so nothing
of it reaches the shelf binary or its AOT publish. Plan: [`docs/slices/m0-fixtures.md`](../../docs/slices/m0-fixtures.md).

## Usage

```
dotnet run --project tests/Shelf.TestData -c Release -- generate --out artifacts/synthetic-library --preset reference [--seed 42]
dotnet run --project tests/Shelf.TestData -c Release -- generate --out <dir> --works N --files M [--seed S] [--words-min 3000] [--words-max 8000] [--deflate]
dotnet run --project tests/Shelf.TestData -c Release -- generate --preset reference --plan
dotnet run --project tests/Shelf.TestData -c Release -- fixtures --out tests/fixtures/derived
```

- Exactly one of `--preset` (`tiny` = 20 works / 30 files, `reference` = 10,000 works / 15,000 files) or
  `--works` and `--files`. There is no default size (the reference library is about 0.6 GB).
- `--files` must lie between `works` and the largest count for which the number of single-file works is not
  negative (21 for 10 works).
- `--out` must be missing or empty (exit 1 otherwise; nothing is ever deleted). Use `artifacts/`, which is
  git-ignored.
- Exit codes: 0 success, 1 runtime error, 2 usage error. stdout: one summary line; stderr: `Elapsed: <s> s` and
  `error: ...` messages.

## Output

```
<out>/ground-truth.json
<out>/input/{downloads,downloads/old,ebooks-backup}/<files>.{epub,pdf}
```

Library shape (formulas of plan section 3.4; reference values in parentheses): 3-file works `floor((files-works)/10)`
(500), 2-file works (4,000), 1-file works (5,500); single-file PDFs `floor(2a/5)`; 3-file works hold EPUB + PDF + a
duplicate (exact byte copy for `ceil(c/2)`, otherwise a second EPUB of the same text with other version and
metadata); edition pairs `floor(works/50)` (200); same-title-different-author pairs `floor(works/100)` (100);
series works `floor(works/10)` (1,000). English 75 %, German 25 %. Titles are unique except for the deliberate
edition and same-title pairs; ISBNs are unique.

`ground-truth.json` (schema 1, `generatorFormat` 1; no timestamps, no versions):
`parameters`, `summary` (the counts above and `totalBytes`), `works[]` (`key`, `title`, `authors`, `language`,
`year`, `isbn13`, `series`, `files`), `relations[]` (`edition`, `same-title-different-author`), `files[]` (`path`,
`work`, `format`, `epubVersion`, `pageCount`, `size`, `sha256`, `duplicateOf`, `duplicateKind`, `traits`,
`embedded` title/authors/isbn13/language). Traits: `epub2 epub3 cover isbn series author-surname-first
author-initials author-no-diacritics title-edition-suffix title-leading-article title-non-ascii pdf-info pdf-xmp
pdf-no-metadata pdf-isbn-in-text file-name-author-title language-de language-en`. Arrays are ordinal-sorted,
paths use `/`, JSON has `\n` line endings and no BOM.

## Determinism contract

The same seed, parameters and `generatorFormat` produce byte-identical files and ground truth on every machine.

- Own SplitMix64 and xoshiro256** (golden-tested against the reference C code); no `System.Random`.
- Per-work generators derived from the seed, so generation is order-independent and runs in `Parallel.For`.
- Integer-only decisions (Zipf weights `floor(10^9 / rank)`, probabilities as `NextInt(1000) < p`); no
  floating point, no `Math.Pow`/`Math.Log`.
- `InvariantCulture`, ordinal sorting, no `Environment.NewLine`, no `Guid.NewGuid`, no clock, no assembly version.
- ZIP entries are stored by default, fixed timestamps. `--deflate` output depends on the zlib implementation and
  is not covered by the digest.
- A golden digest of `generate --seed 42 --preset tiny` (SHA-256 over ordinal-sorted `<path>TAB<sha256>LF` lines of
  every generated file) is frozen in `DeterminismTests`; it is checked on all CI platforms, under `de-DE` and with
  parallelism 1. If a .NET update changes ZIP bytes, the test fails: update the constant and bump
  `GroundTruth.CurrentGeneratorFormat` in the same change.

## Tests

`tests/Shelf.TestData.Tests` (xunit.v3): PRNG and checksum goldens, ISBN, plan arithmetic, name/title variants,
file names, text generation, EPUB/PDF/PNG byte structure, readability with VersOne.Epub 3.3.6 and PdfPig 0.1.16
(test-only), digest, committed and derived fixtures, catalog validation, guard. The reference run is
`[Fact(Explicit = true)] GeneratorTests.ReferenceRun` and not part of the default run or CI.
The behavior of the CLI is specified in `tests/Shelf.Specs/Features/TestData/SyntheticLibrary.feature`.

## Measured reference run

`generate --preset reference --seed 42` in Release on an Apple M4 (macOS 26.6.2, .NET 10.0.401, warm file cache):
2.6 s wall clock (reported `Elapsed: 2.6 s`, about 10 s CPU), 604,478,481 bytes (576 MiB; 635 MB on disk) in 15,000
files plus `ground-truth.json`, 248 MB peak resident memory. 10,000 works, 5,500/4,000/500 works with
1/2/3 files, 8,300 EPUB and 6,700 PDF files, 250 exact and 250 same-format duplicates, 200 edition pairs, 100
same-title-different-author pairs, 1,000 series works. The plan expected 0.6-0.8 GB and less than 10 minutes.

## Manual validation

`pdfinfo` (poppler) reported no errors or warnings on 25 sampled generated PDFs and `unzip -t` passed on 40 sampled
EPUBs. `epubcheck` and `qpdf --check` were not available in the development environment (no epubcheck jar,
no qpdf) and were not downloaded; spec validity beyond the lenient VersOne.Epub/PdfPig/poppler readers is
unverified.
