# Slice m0-fixtures: Public-Domain Fixtures and Synthetic Library Generator

| | |
|---|---|
| **Slice ID** | `m0-fixtures` |
| **Branch** | `feat/m0-fixtures` |
| **Milestone** | M0 – Setup & Spikes |
| **Roadmap** | M0 work package "Test fixtures: Public-domain EPUBs/PDFs; generator for a synthetic library of 10,000 works / 15,000 files for performance tests" |
| **PRD references** | 10.1 (`tests/fixtures/` "Sample EPUBs/PDFs (public domain)"), 9.1 (reference library: 10,000 works, approx. 15,000 files), G3 (search < 100 ms at 10,000 works). Enabling only, not verified here: F-IMP-2/3/4 (OPF, Info, XMP, file-name fallback, ISBN in text), F-GRP-1/2/5 (ISBN, normalized title + surname, SimHash, editions), F-DUP-1/2, Q7 (scanned PDFs), acceptance criterion 1 |
| **Inputs** | ADR 0001 (license policy GPL-3.0-compatible; rows 3, 4, 5, 19, 20), `docs/slices/m0-logging.md` (`ShelfApp.Run(args, stdout, stderr)` pattern), `tests/Shelf.Specs/Steps/*` (existing step phrases), `.gitignore`, `.github/workflows/build.yml` |

---

## 1. Scope

**In scope**

1. `tests/fixtures/`: a small committed set of **upstream** public-domain/CC0 EPUBs and one PDF (unmodified, as downloaded) plus **derived** fixtures produced by the generator (broken metadata, ISBN, image-only PDF, ...). `manifest.json` with provenance, license basis, jurisdiction notes, SHA-256 and size per file; `README.md` with the licensing rules. Size budget: total ≤ 5 MiB, per file ≤ 1.5 MiB.
2. `tests/Shelf.TestData/`: a deterministic, seeded generator for a synthetic library (default reference shape 10,000 works / 15,000 files) that writes valid minimal EPUB 2/3 and PDF files plus a `ground-truth.json`. Exe project, run with `dotnet run`; also a library that specs and later slices reference in-process. Also hosts `FixtureCatalog` (manifest reader) for later slices.
3. `tests/Shelf.TestData.Tests/`: unit tests (xunit.v3), including readability checks with VersOne.Epub and PdfPig (test-only use).
4. Feature files `SyntheticLibrary.feature` (`@PRD-9.1 @M0`) and `Fixtures.feature` (`@PRD-10.1 @M0`) with step definitions in `Shelf.Specs`.
5. `.gitignore` (`/artifacts/`), new `.gitattributes` (binary fixtures), roadmap row marked done.

**Requirements covered:** PRD 10.1 `fixtures/` line and the 9.1 reference-library definition, milestone M0. No functional requirement (F-*) is implemented or tagged; the F-* IDs above are only enabled.

**Feature vs. unit test (explicit split)**

| Concern | Where | Why |
|---|---|---|
| Generator CLI contract: counts, shape, determinism, ground truth completeness, usage errors, refusal to overwrite | Feature `SyntheticLibrary.feature` | Observable behavior of the deliverable (input: arguments; output: files, exit code) |
| Fixture set contract: manifest completeness, checksums, size budget, provenance fields, trait coverage, reproducible derived files | Feature `Fixtures.feature` | Observable property of the repository deliverable |
| PRNG, CRC32/Adler-32, ISBN check digit, plan arithmetic, name/title variants, file-name sanitizing, ZIP/OPF/XHTML/PDF/PNG byte structure, parser readability, cross-platform golden digest, culture independence, full 10k run | Unit tests `Shelf.TestData.Tests` | Internal details, or too slow for the default suite |

**Out of scope**

- Any shelf product code (`src/` unchanged). No EPUB/PDF parsing in `src/` (M1 extraction slice).
- Running the full 10,000-work generation in CI; benchmarks against it (roadmap: from M1 on, Q10).
- An acceptance-criterion-1 preset (500 files incl. 100 EPUB+PDF works); added by the M1 grouping calibration slice (Q12).
- Calibrating SimHash distances of generated editions; the generator only records ground-truth relations.
- Packaging the generator as a `dotnet tool`; git-lfs; download scripts (Q1, Q5).
- epubcheck/qpdf in CI (Java/native tools; manual check only, step 11).
- MOBI/AZW3/CBZ/TXT/MD files (F-LIB-3, Should).

## 2. Feature Files

Rules for the implementer:

- Write both files first. `dotnet test -c Release`: new scenarios red (undefined steps), existing green.
- Step phrases start with "the generator", "the output", "the ground truth" or "fixture" to avoid ambiguous bindings with `CliSteps` (`the exit code is {int}`, `standard error contains {string}` already exist and refer to a shelf run).
- Numbers in the count tables follow the plan formula in 3.4. If an implementation detail forces a different number, stop and report; do not edit rows silently.

### 2.1 `tests/Shelf.Specs/Features/TestData/SyntheticLibrary.feature` (new)

```gherkin
@PRD-9.1 @M0
Feature: Synthetic library generator
  As a developer of shelf
  I want a deterministic generator for a synthetic e-book library
  So that performance and grouping tests use the same reproducible data on every machine

  Background:
    Given an empty generator workspace

  Scenario Outline: The generator creates the requested numbers of works and files
    When I run the generator with arguments "generate --seed 42 --works <works> --files <files>" into "out"
    Then the generator exit code is 0
    And the output "out" contains <files> e-book files
    And the ground truth of "out" lists <works> works and <files> files
    And the ground truth of "out" lists <single> works with 1 file, <double> works with 2 files and <triple> works with 3 files
    And the output "out" contains <epub> EPUB files and <pdf> PDF files

    Examples:
      | works | files | single | double | triple | epub | pdf |
      | 10    | 10    | 10     | 0      | 0      | 6    | 4   |
      | 10    | 15    | 5      | 5      | 0      | 8    | 7   |
      | 20    | 30    | 11     | 8      | 1      | 17   | 13  |
      | 20    | 40    | 2      | 16     | 2      | 22   | 18  |
      | 100   | 150   | 55     | 40     | 5      | 83   | 67  |

  Scenario Outline: Presets plan the documented library shapes without writing files
    When I run the generator with arguments "generate --preset <preset> --plan"
    Then the generator exit code is 0
    And the generator plan has the value <value> for "<property>"

    Examples:
      | preset    | property             | value |
      | reference | works                | 10000 |
      | reference | files                | 15000 |
      | reference | singleFileWorks      | 5500  |
      | reference | twoFileWorks         | 4000  |
      | reference | threeFileWorks       | 500   |
      | reference | epubFiles            | 8300  |
      | reference | pdfFiles             | 6700  |
      | reference | exactDuplicates      | 250   |
      | reference | sameFormatDuplicates | 250   |
      | reference | editionPairs         | 200   |
      | reference | hardNegativePairs    | 100   |
      | reference | seriesWorks          | 1000  |
      | tiny      | works                | 20    |
      | tiny      | files                | 30    |

  Scenario Outline: The same seed produces byte-identical output
    When I run the generator with arguments "generate --seed <seed> --works 20 --files 30" into "a"
    And I run the generator with arguments "generate --seed <seed> --works 20 --files 30" into "b"
    Then the outputs "a" and "b" are byte-identical

    Examples:
      | seed       |
      | 0          |
      | 42         |
      | 2147483647 |

  Scenario: Different seeds produce different libraries
    When I run the generator with arguments "generate --seed 1 --works 20 --files 30" into "a"
    And I run the generator with arguments "generate --seed 2 --works 20 --files 30" into "b"
    Then the outputs "a" and "b" differ

  Scenario: The ground truth describes every generated file exactly
    When I run the generator with arguments "generate --seed 42 --works 100 --files 150" into "out"
    Then every e-book file in "out" is listed in the ground truth with its size and SHA-256
    And every file listed in the ground truth of "out" exists
    And every generated EPUB in "out" starts with the ZIP signature and every generated PDF with "%PDF-"

  Scenario Outline: Duplicates in the ground truth match the file contents
    When I run the generator with arguments "generate --seed 42 --works 100 --files 150" into "out"
    Then the ground truth of "out" lists <count> files with duplicate kind "<kind>"
    And every file with duplicate kind "<kind>" in "out" belongs to the same work as its original
    And every file with duplicate kind "<kind>" in "out" <relation> its original

    Examples:
      | kind        | count | relation                     |
      | exact       | 3     | has the same SHA-256 as      |
      | same-format | 2     | has a different SHA-256 than |

  Scenario Outline: A medium library contains every variation needed for grouping tests
    When I run the generator with arguments "generate --seed 42 --works 200 --files 300" into "out"
    Then the ground truth of "out" has at least one file with the trait "<trait>"

    Examples:
      | trait                   |
      | epub2                   |
      | epub3                   |
      | cover                   |
      | isbn                    |
      | series                  |
      | author-surname-first    |
      | author-initials         |
      | author-no-diacritics    |
      | title-edition-suffix    |
      | title-leading-article   |
      | title-non-ascii         |
      | pdf-info                |
      | pdf-xmp                 |
      | pdf-no-metadata         |
      | pdf-isbn-in-text        |
      | file-name-author-title  |
      | language-de             |
      | language-en             |

  Scenario Outline: A medium library contains related and confusable works
    When I run the generator with arguments "generate --seed 42 --works 200 --files 300" into "out"
    Then the ground truth of "out" lists <count> work pairs with relation "<relation>"

    Examples:
      | relation                    | count |
      | edition                     | 4     |
      | same-title-different-author | 2     |

  Scenario Outline: Invalid arguments are usage errors
    When I run the generator with arguments "<arguments>" into "out"
    Then the generator exit code is 2
    And the generator error output contains "<message>"
    And the output "out" contains 0 e-book files

    Examples:
      | arguments                                    | message                                              |
      | generate --works 0 --files 0                 | --works must be at least 1                           |
      | generate --works 10 --files 9                | --files must be between 10 and 21 for 10 works       |
      | generate --works 10 --files 22               | --files must be between 10 and 21 for 10 works       |
      | generate --works 10                          | --works and --files must be given together           |
      | generate --preset tiny --works 10 --files 10 | --preset cannot be combined with --works or --files  |
      | generate --preset huge                       | Unknown preset 'huge'                                |
      | generate --works 10 --files 10 --words-min 9000 --words-max 8000 | --words-min must not exceed --words-max |

  Scenario: A missing output directory is a usage error
    When I run the generator with arguments "generate --works 10 --files 10"
    Then the generator exit code is 2
    And the generator error output contains "--out is required unless --plan is given"

  Scenario: The generator never writes into a non-empty directory
    Given the generator workspace directory "out" contains a file "keep.txt"
    When I run the generator with arguments "generate --seed 42 --works 10 --files 10" into "out"
    Then the generator exit code is 1
    And the generator error output contains "is not empty"
    And the output "out" contains 0 e-book files
    And the generator workspace file "out/keep.txt" still exists
```

Notes:

- `into "<dir>"` appends `--out <workspace>/<dir>` to the arguments. The variant without `into` appends nothing.
- The `epub`/`pdf` counts and the duplicate counts follow 3.4 (single-file PDF share `floor(2a/5)`; 3-file works: `ceil(c/2)` exact copies). For 100/150: exact 3, same-format 2.
- Edition and hard-negative counts for 200 works: `floor(200/50) = 4`, `floor(200/100) = 2` (3.4).
- Usage messages are shelf-owned validations, not System.CommandLine texts, so they are stable.

### 2.2 `tests/Shelf.Specs/Features/TestData/Fixtures.feature` (new)

```gherkin
@PRD-10.1 @M0
Feature: Committed e-book fixtures
  As a developer of shelf
  I want a small, documented set of real and derived e-book files in the repository
  So that parsers can be tested against known files whose redistribution is allowed

  Background:
    Given the fixture manifest "tests/fixtures/manifest.json"

  Scenario: The manifest and the fixture directory match
    Then every file under "tests/fixtures" except "README.md" and "manifest.json" is listed in the manifest
    And every file listed in the manifest exists

  Scenario: Every fixture has the size and SHA-256 recorded in the manifest
    Then every fixture file has the size and SHA-256 recorded in the manifest

  Scenario: The fixtures stay within the size budget
    Then the total size of all fixture files is at most 5242880 bytes
    And no fixture file is larger than 1572864 bytes

  Scenario: Every upstream fixture documents its provenance and license
    Then every fixture of origin "upstream" has a source URL, a retrieval date, a license, a US public-domain basis and a life+70 basis
    And every fixture of origin "upstream" is unmodified

  Scenario: Every derived fixture names the command that produced it
    Then every fixture of origin "derived" names its generator command and has no source URL

  Scenario Outline: The fixtures cover each trait needed by later parsers
    Then at least one fixture of origin "<origin>" has the trait "<trait>"

    Examples:
      | origin   | trait           |
      | upstream | epub2           |
      | upstream | epub3           |
      | upstream | cover           |
      | upstream | language-de     |
      | upstream | pdf-text-layer  |
      | any      | non-ascii-title |
      | any      | no-cover        |
      | derived  | isbn            |
      | derived  | series          |
      | derived  | empty-metadata  |
      | derived  | malformed-opf   |
      | derived  | not-a-zip       |
      | derived  | pdf-image-only  |
      | derived  | pdf-xmp         |
      | derived  | pdf-no-metadata |

  Scenario Outline: Some works are present in more than one file
    Then the fixtures contain a work of origin "<origin>" with files of the traits "<first>" and "<second>"

    Examples:
      | origin   | first | second         |
      | upstream | epub2 | epub3          |
      | derived  | epub3 | pdf-text-layer |

  Scenario: Derived fixtures are reproducible
    When the derived fixtures are regenerated into a temporary directory
    Then every regenerated file is byte-identical to the committed derived fixture with the same name
    And no committed derived fixture is missing from the regenerated set
```

Notes: `origin "any"` means upstream or derived. Upstream traits are recorded from inspection after download (step 2), not assumed. If an upstream row cannot be satisfied by a legally usable file within the budget (e.g. no small German PG EPUB with a cover), stop and report; do not move the row to `derived` without owner approval.

### 2.3 Step definitions

New files in `tests/Shelf.Specs/Steps/`; Cucumber expressions; ordinal comparisons. `Shelf.Specs.csproj` gets a `ProjectReference` to `tests/Shelf.TestData`.

`GeneratorSteps.cs`:

| Step | Behavior |
|---|---|
| `Given an empty generator workspace` | Unique temp dir `Path.GetTempPath()/shelf-specs/<guid>`; deleted in `[AfterScenario]`. |
| `Given the generator workspace directory {string} contains a file {string}` | Create dir and a 1-byte file. |
| `When I run the generator with arguments {string}` | Split on spaces (no quoting needed); `GeneratorApp.Run(args, stdout, stderr)` with `StringWriter`s; store exit code. |
| `When I run the generator with arguments {string} into {string}` | Same, appending `--out <workspace>/<dir>`. |
| `Then the generator exit code is {int}` | Equality. |
| `Then the generator error output contains {string}` | Ordinal `Contains` on stderr. |
| `Then the generator plan has the value {int} for {string}` | Parse stdout as JSON object; integer property equality. |
| `Then the output {string} contains {int} e-book files` | Recursive count of `*.epub` + `*.pdf` under `<dir>/input` (0 if missing). |
| `Then the output {string} contains {int} EPUB files and {int} PDF files` | Count by extension. |
| `Then the ground truth of {string} lists {int} works and {int} files` | `GroundTruth.Load(<dir>/ground-truth.json)`; array lengths. |
| `Then the ground truth of {string} lists {int} works with 1 file, {int} works with 2 files and {int} works with 3 files` | Group by `work.files.Count`. |
| `Then the outputs {string} and {string} are byte-identical` / `differ` | Ordinal-sorted relative paths equal and every file's bytes equal (incl. `ground-truth.json`) / at least one difference. |
| `Then every e-book file in {string} is listed in the ground truth with its size and SHA-256` | Compute and compare. |
| `Then every file listed in the ground truth of {string} exists` | `File.Exists`. |
| `Then every generated EPUB in {string} starts with the ZIP signature and every generated PDF with {string}` | First bytes `50 4B 03 04` / ASCII prefix. |
| `Then the ground truth of {string} lists {int} files with duplicate kind {string}` | Count `files[].duplicateKind`. |
| `Then every file with duplicate kind {string} in {string} belongs to the same work as its original` | `duplicateOf` path's `work` equals own `work`. |
| `Then every file with duplicate kind {string} in {string} has the same SHA-256 as its original` / `has a different SHA-256 than its original` | Two bindings. |
| `Then the ground truth of {string} has at least one file with the trait {string}` | `files[].traits` contains. |
| `Then the ground truth of {string} lists {int} work pairs with relation {string}` | Count `relations[]` by `kind`. |
| `Then the generator workspace file {string} still exists` | `File.Exists`. |

`FixtureSteps.cs`:

| Step | Behavior |
|---|---|
| `Given the fixture manifest {string}` | `FixtureCatalog.Load(RepositoryPaths.Root())`; path relative to repo root. |
| `Then every file under {string} except {string} and {string} is listed in the manifest` | Recursive enumeration, relative `/` paths, ordinal. |
| `Then every file listed in the manifest exists` | |
| `Then every fixture file has the size and SHA-256 recorded in the manifest` | |
| `Then the total size of all fixture files is at most {long} bytes` / `no fixture file is larger than {long} bytes` | Sum of manifest-listed files (on-disk sizes). |
| `Then every fixture of origin {string} has a source URL, a retrieval date, a license, a US public-domain basis and a life+70 basis` | Non-empty fields; URL is absolute `https`; date `yyyy-MM-dd`. |
| `Then every fixture of origin {string} is unmodified` | `modifications` is empty. |
| `Then every fixture of origin {string} names its generator command and has no source URL` | |
| `Then at least one fixture of origin {string} has the trait {string}` | `any` matches both origins. |
| `Then the fixtures contain a work of origin {string} with files of the traits {string} and {string}` | Group by `work`; two distinct files carrying the traits. |
| `When the derived fixtures are regenerated into a temporary directory` | `GeneratorApp.Run(["fixtures", "--out", tmp], ...)`; exit 0. |
| `Then every regenerated file is byte-identical to the committed derived fixture with the same name` / `no committed derived fixture is missing from the regenerated set` | Compare against `tests/fixtures/derived/`. |

`RepositoryPaths.Root()`: walk up from `AppContext.BaseDirectory` until a directory contains `shelf.slnx`; throw if none.

## 3. Design

### 3.1 Files

```
.gitignore                                          CHANGE  add /artifacts/
.gitattributes                                      NEW     *.epub binary, *.pdf binary, *.png binary
Directory.Packages.props                            CHANGE  VersOne.Epub 3.3.6, PdfPig 0.1.16 (test-only use)
shelf.slnx                                          CHANGE  add tests/Shelf.TestData, tests/Shelf.TestData.Tests
docs/roadmap.md                                     CHANGE  M0 Test fixtures row: Done note
tests/fixtures/README.md                            NEW     provenance table, licensing rules, how to add/refresh (3.3)
tests/fixtures/manifest.json                        NEW     (3.3)
tests/fixtures/upstream/gutenberg/*.epub            NEW     unmodified Project Gutenberg files
tests/fixtures/upstream/standardebooks/*.epub       NEW     unmodified Standard Ebooks file
tests/fixtures/upstream/pdf/*.pdf                   NEW     one real PDF with text layer (Q2)
tests/fixtures/derived/*                            NEW     output of `Shelf.TestData fixtures` (3.3)
tests/Shelf.TestData/Shelf.TestData.csproj          NEW     OutputType Exe, IsPackable false, System.CommandLine, embedded vocabulary
tests/Shelf.TestData/README.md                      NEW     usage, determinism contract, ground-truth schema, measured runtime/size
tests/Shelf.TestData/Program.cs                     NEW     => GeneratorApp.Run(args, Console.Out, Console.Error)
tests/Shelf.TestData/GeneratorApp.cs                NEW     commands `generate`, `fixtures`
tests/Shelf.TestData/Random/SplitMix64.cs           NEW     internal
tests/Shelf.TestData/Random/Xoshiro256StarStar.cs   NEW     internal
tests/Shelf.TestData/Planning/LibraryPlanner.cs     NEW     pure plan computation (3.4)
tests/Shelf.TestData/Planning/LibraryPlan.cs        NEW
tests/Shelf.TestData/Model/*.cs                     NEW     WorkSpec, FileSpec, AuthorName, Relation, Trait constants
tests/Shelf.TestData/Content/TextGenerator.cs       NEW     Zipf word sampling, paragraphs, chapters
tests/Shelf.TestData/Content/NameVariants.cs        NEW
tests/Shelf.TestData/Content/TitleVariants.cs       NEW
tests/Shelf.TestData/Content/FileNames.cs           NEW     patterns + sanitizing
tests/Shelf.TestData/Content/Isbn.cs                NEW
tests/Shelf.TestData/Vocabulary/en.txt, de.txt      NEW     hand-authored word lists (3.5)
tests/Shelf.TestData/Vocabulary/given-names.txt, surnames.txt, topics-en.txt, topics-de.txt  NEW
tests/Shelf.TestData/Formats/EpubWriter.cs          NEW     System.IO.Compression.ZipArchive
tests/Shelf.TestData/Formats/PdfWriter.cs           NEW     hand-written PDF 1.7
tests/Shelf.TestData/Formats/PngWriter.cs           NEW     hand-written, stored deflate blocks
tests/Shelf.TestData/Formats/Crc32.cs, Adler32.cs   NEW
tests/Shelf.TestData/LibraryGenerator.cs            NEW     orchestration, ground truth
tests/Shelf.TestData/GroundTruth.cs                 NEW     schema types + Load/Write (System.Text.Json)
tests/Shelf.TestData/DerivedFixtures.cs             NEW     fixed definitions of tests/fixtures/derived (3.3)
tests/Shelf.TestData/FixtureCatalog.cs              NEW     manifest reader for specs and later slices
tests/Shelf.TestData/RepositoryPaths.cs             NEW
tests/Shelf.TestData.Tests/Shelf.TestData.Tests.csproj  NEW  xunit.v3, OutputType Exe; VersOne.Epub, PdfPig
tests/Shelf.TestData.Tests/**/*Tests.cs             NEW     (section 4)
tests/Shelf.Specs/Shelf.Specs.csproj                CHANGE  ProjectReference tests/Shelf.TestData
tests/Shelf.Specs/Features/TestData/*.feature       NEW     (2.1, 2.2)
tests/Shelf.Specs/Steps/GeneratorSteps.cs           NEW
tests/Shelf.Specs/Steps/FixtureSteps.cs             NEW
```

`src/` is not changed. No `src/` project may reference `Shelf.TestData` (guard test, section 4). `.github/workflows/build.yml` is not changed: the `test` job builds the whole solution and runs the fast tests on all four platforms; nothing generates the reference library.

### 3.2 Generator CLI (`GeneratorApp`)

System.CommandLine 2.0.12 (already in `Directory.Packages.props`, ADR 0001 row 3 / ADR 0002). Same shape as `ShelfApp`: `public static int Run(string[] args, TextWriter stdout, TextWriter stderr)`, never touches the static console.

```
dotnet run --project tests/Shelf.TestData -c Release -- generate --out artifacts/synthetic-library --preset reference [--seed 42]
dotnet run --project tests/Shelf.TestData -c Release -- generate --out <dir> --works N --files M [--seed S] [--words-min 3000] [--words-max 8000] [--deflate]
dotnet run --project tests/Shelf.TestData -c Release -- generate --preset reference --plan
dotnet run --project tests/Shelf.TestData -c Release -- fixtures --out tests/fixtures/derived
```

| Rule | Detail |
|---|---|
| Size source | Exactly one of `--preset` or (`--works` and `--files`). Presets: `tiny` = 20/30, `reference` = 10,000/15,000. No default (a 0.7 GB write must be explicit). |
| Seed | `int`, 0..2147483647, default 42. |
| Validation | `--works ≥ 1`; `--files` between `works` and `maxFiles(works)` (largest `files` for which `a ≥ 0` in 3.4; 21 for 10 works); `1 ≤ words-min ≤ words-max ≤ 100000`. Messages exactly as in 2.1. Exit 2. |
| `--out` | Required unless `--plan`. Created if missing. Existing and non-empty → `error: Output directory '<path>' is not empty`, exit 1. Nothing is deleted, ever. |
| `--plan` | Prints the summary JSON (same object as `ground-truth.json` `summary`), writes nothing, exit 0. |
| `--deflate` | Deflate for ZIP entries except `mimetype`. Output is then **not** guaranteed byte-identical across platforms/runtimes (zlib-ng); documented, not covered by the golden digest. |
| stdout on success | `Generated <works> works and <files> files (<bytes> bytes) in <out>` |
| stderr | `Elapsed: <s> s` (non-deterministic data goes to stderr only); errors as `error: <message>`. |
| `fixtures --out <dir>` | Writes the derived fixtures of 3.3 (same non-empty rule). |

### 3.3 Committed fixtures (`tests/fixtures/`)

**Hosting:** plain git (Q1). `.gitattributes` marks `*.epub`, `*.pdf`, `*.png` as `binary`.

**Licensing rules (verify each claim at the source and quote it in `README.md` with URL and retrieval date; nothing below is verified, this plan was written without network access):**

| Source | Claim to verify | Rule for shelf |
|---|---|---|
| Project Gutenberg, license at https://www.gutenberg.org/policy/license.html and permission FAQ at https://www.gutenberg.org/policy/permission.html | eBooks without a copyright notice are public domain in the USA; the ebook header tells users outside the USA to check local law; "Project Gutenberg" is a registered trademark; redistributing a file that carries the PG name/trademark requires keeping the full PG License; fees/royalty terms apply only when charging; a text with all PG references and the license removed is a plain public-domain text that can be used freely | Commit PG files **byte-for-byte unmodified** (license text included), free of charge. Never edit a PG file while keeping the PG name. Derived fixtures contain no PG text at all (they use synthetic text). Only pick ebooks whose PG page says "Public domain in the USA." (not "Copyrighted"). |
| Project Gutenberg robot policy https://www.gutenberg.org/policy/robot_access.html | Automated crawling of the main site is not allowed | Download the handful of files manually (browser or single `curl` per file); no script in the repo downloads from PG. |
| Standard Ebooks, colophon inside the EPUB and https://standardebooks.org (about/licensing page) | Standard Ebooks dedicates its own production work (markup, cover composition) to the public domain via CC0 1.0; the source text is public domain in the USA; check whether the logo/name carry trademark or use restrictions | Commit unmodified. Record the colophon's exact license sentence in the manifest. |
| arXiv (only if Q2 picks it), abs page license link | Paper licensed CC0 1.0 (`creativecommons.org/publicdomain/zero/1.0/`) | Only CC0 papers; record the license URL from the abs page. arXiv's stamp on the PDF is acceptable. |
| Wikisource | Wikisource contributions are CC BY-SA (attribution/share-alike); ws-export output changes over time | Not used (Q2). |

**Public-domain basis rule** (both must hold; recorded per upstream file): published in or before 1930 (US public domain as of 2026-01-01; verify against the current US rule) **and** every author, translator and illustrator died in or before 1955 (life + 70 as of 2026-01-01, e.g. EU/Germany). README states: other jurisdictions (e.g. life + 100) are not checked; not legal advice.

**Upstream selection (candidates; ebook numbers, sizes, traits to verify in step 2; Q2):**

| ID | Candidate | Purpose | Expected traits (to verify) |
|---|---|---|---|
| U1 | PG: Charlotte Perkins Gilman, *The Yellow Wallpaper* (1892; Gilman d. 1935), EPUB3 file ("EPUB3 (E-readers incl. Send-to-Kindle)") | Short real EPUB 3 | epub3, language-en, cover? |
| U2 | Same PG ebook, legacy EPUB ("older E-readers") | Same work as EPUB 2: real multi-file work | epub2 |
| U3 | Standard Ebooks: a short work, ideally the same work as U1 if SE has it | EPUB 3 with real cover; same-format duplicate from another source (F-DUP-2) | epub3, cover |
| U4 | PG: a short German work with non-ASCII title, e.g. Theodor Storm, *Pole Poppenspäler* (1874; Storm d. 1888) or Franz Kafka, *Die Verwandlung* (1915; Kafka d. 1924; title is ASCII, then non-ascii-title comes from derived) | German text, umlauts | epub3, language-de, non-ascii-title? |
| U5 | One real PDF with a text layer ≤ 1 MB (Q2: CC0 arXiv paper recommended) | Real producer (e.g. pdfTeX), real Info/XMP | pdf-text-layer, pdf-info, pdf-xmp? |

A real image-only scan PDF is not pursued (public scans are usually > 10 MB and often carry an OCR layer); `pdf-image-only` comes from a derived fixture (Q2).

**Derived fixtures** (`tests/fixtures/derived/`, produced by `fixtures --out`, fixed definitions in `DerivedFixtures.cs`, synthetic text from seed 7; no third-party content):

| File | Format | Metadata / defect | Traits |
|---|---|---|---|
| `epub3-isbn-series.epub` | EPUB 3.0 | title "Event Streams in Practice", author "Jördis Ålander" (`file-as` "Ålander, Jördis"), `urn:isbn:9780000000019`, `belongs-to-collection` "Streaming Foundations" #2, `dc:language` en, no cover; work key `d1` | epub3, isbn, series, no-cover, non-ascii-title? (no: author only) |
| `Event Streams in Practice.pdf` | PDF 1.7 | Info + XMP title/author as above; first page text contains "ISBN 978-0-00-000001-9"; same running text as the EPUB; work key `d1` | pdf-text-layer, pdf-info, pdf-xmp, isbn |
| `epub2-isbn-cover.epub` | EPUB 2.0.1 | title "Größenordnungen verteilter Systeme", authors "Lukas Weiß", "Ana Núñez"; `dc:identifier opf:scheme="ISBN"` 9780000000026; `dc:language` de; NCX; `calibre:series` "Verteilte Systeme" / `calibre:series_index` 1; 64×96 PNG cover (`<meta name="cover">`) | epub2, isbn, series, cover, language-de, non-ascii-title |
| `Jane Doe - Quiet Systems (2004).epub` | EPUB 3.0 | `dc:title` empty, no `dc:creator`, no `dc:language`; valid ZIP/OPF otherwise | epub3, empty-metadata |
| `malformed-opf.epub` | EPUB 3.0 | OPF is not well-formed XML (unclosed `<metadata>`) | malformed-opf |
| `not-a-zip.epub` | – | 64 bytes of text "This file is not an EPUB container." padded | not-a-zip |
| `Jane Doe - Quiet Systems (2004).pdf` | PDF 1.7 | no Info dictionary, no XMP; text layer present | pdf-text-layer, pdf-no-metadata |
| `scanned-page.pdf` | PDF 1.7 | one page, one 1-bit DeviceGray image XObject (synthetic noise pattern), no text operators; Info `/Title (Scanned Page)` | pdf-image-only, pdf-info |

The two `Jane Doe` files exercise the F-IMP-3 file-name fallback. ISBNs above have valid check digits (computed: `978000000001` → 9, `978000000002` → 6); they may collide with real books (Q11).

**`manifest.json`** (System.Text.Json, camelCase, ordinal-sorted by `path`):

```json
{
  "schema": 1,
  "budget": { "totalBytes": 5242880, "fileBytes": 1572864 },
  "fixtures": [
    {
      "path": "upstream/gutenberg/1952-the-yellow-wallpaper.epub3.epub",
      "origin": "upstream",
      "work": "yellow-wallpaper",
      "format": "epub", "epubVersion": "3.0",
      "title": "The Yellow Wallpaper", "authors": ["Charlotte Perkins Gilman"], "language": "en",
      "traits": ["epub3", "cover"],
      "source": { "url": "https://www.gutenberg.org/ebooks/...", "retrieved": "2026-10-15", "publisher": "Project Gutenberg" },
      "license": "Project Gutenberg License (file unmodified, license included)",
      "publicDomain": { "us": "Published 1892 (before 1931).", "lifePlus70": "Author died 1935." },
      "modifications": [],
      "generatorCommand": null,
      "size": 0, "sha256": "..."
    }
  ]
}
```

Derived entries: `origin "derived"`, `source null`, `license "GPL-3.0-or-later (generated by shelf test code)"` (Q4), `publicDomain null`, `generatorCommand "dotnet run --project tests/Shelf.TestData -- fixtures --out tests/fixtures/derived"`. Trait names are a closed list (constants in `Model/Traits.cs`); `FixtureCatalog.Load` rejects unknown traits.

### 3.4 Library shape (`LibraryPlanner`, pure integer arithmetic)

With `w` works and `f` files, `extra = f − w`:

| Quantity | Formula | Reference (10,000/15,000) |
|---|---|---|
| 3-file works `c` | `floor(extra / 10)` | 500 |
| 2-file works `b` | `extra − 2c` | 4,000 |
| 1-file works `a` | `w − b − c` (must be ≥ 0, else usage error) | 5,500 |
| 1-file PDFs / EPUBs | `floor(2a / 5)` / rest | 2,200 / 3,300 |
| 2-file works | 1 EPUB + 1 PDF | 4,000 + 4,000 |
| 3-file works | EPUB + PDF + duplicate; first `ceil(c/2)` duplicates are exact byte copies of the EPUB (new file name), the rest a second EPUB of the same text with different metadata completeness, other EPUB version and/or cover | 250 exact, 250 same-format |
| EPUB / PDF files | sum | 8,300 / 6,700 |
| Edition pairs | `floor(w / 50)` pairs of distinct works: same title + authors, year +2..+6, different ISBN, about 15 % of paragraphs replaced | 200 |
| Same-title-different-author pairs | `floor(w / 100)`: identical normalized title, different surnames, unrelated text | 100 |
| Series works | `floor(w / 10)`, grouped into series of 2–6 volumes | 1,000 |

Work indices are assigned to roles in a deterministic shuffle of `0..w−1`. Per file, independent draws (seeded) choose: EPUB version (EPUB 2 with p = 0.3), cover (p = 0.5 for EPUB), metadata completeness, author/title variant, ISBN presence (work has ISBN p = 0.7; PDF embeds it in Info p = 0.5 and in page-1 text p = 0.5), PDF metadata mode (Info only 0.5 / Info + XMP 0.3 / none 0.2), language (work-level: de p = 0.25), folder and file-name pattern. Probabilities are compared as integers (`NextInt(1000) < 300`), never via floating point.

**Variants (traits recorded per file):**

| Trait | Rule (example for canonical "Martin Fowler" / "Patterns of Distributed Caching") |
|---|---|
| `author-surname-first` | "Fowler, Martin" |
| `author-initials` | "M. Fowler" |
| `author-no-diacritics` | "José Núñez" → "Jose Nunez" (only for names with diacritics) |
| `title-edition-suffix` | "Patterns of Distributed Caching, 2nd Edition" / "... (German Edition)" |
| `title-leading-article` | "The Patterns of ..." (en) / "Die ..." (de) |
| `title-non-ascii` | Title contains at least one non-ASCII letter (German vocabulary, names) |
| `file-name-author-title` | File name `Author - Title (Year).ext` |
| `pdf-no-metadata` | No Info, no XMP; then the file name always uses `Author - Title (Year)` |
| others | `epub2`, `epub3`, `cover`, `isbn`, `series`, `pdf-info`, `pdf-xmp`, `pdf-isbn-in-text`, `language-de`, `language-en` |

**Output layout:** `<out>/ground-truth.json` and `<out>/input/{downloads,downloads/old,ebooks-backup}/<name>`. File-name patterns: `Author - Title (Year)`, `Title`, `title_words_year` (lower snake case), `ISBN13`, `Title - Author`. Sanitizing: replace `/\:*?"<>|` and control characters by `-`, collapse spaces, NFC, trim trailing dots/spaces, ≤ 120 UTF-16 chars before the extension; collision in a folder → ` (1)`, ` (2)`, ...

**`ground-truth.json`** (schema 1; no timestamps, no assembly version; `generatorFormat` is a constant integer bumped whenever output bytes change):

```json
{
  "schema": 1, "generatorFormat": 1, "seed": 42,
  "parameters": { "works": 10000, "files": 15000, "wordsMin": 3000, "wordsMax": 8000, "compression": "stored" },
  "summary": { "works": 10000, "files": 15000, "singleFileWorks": 5500, "twoFileWorks": 4000, "threeFileWorks": 500,
               "epubFiles": 8300, "pdfFiles": 6700, "exactDuplicates": 250, "sameFormatDuplicates": 250,
               "editionPairs": 200, "hardNegativePairs": 100, "seriesWorks": 1000, "totalBytes": 0 },
  "works": [ { "key": "w00001", "title": "...", "authors": ["..."], "language": "en", "year": 2017,
               "isbn13": "978...", "series": { "name": "...", "index": 2 }, "files": ["input/downloads/..."] } ],
  "relations": [ { "kind": "edition", "a": "w00001", "b": "w00517" },
                 { "kind": "same-title-different-author", "a": "w00002", "b": "w00950" } ],
  "files": [ { "path": "input/downloads/....epub", "work": "w00001", "format": "epub", "epubVersion": "3.0",
               "pageCount": null, "size": 0, "sha256": "...", "duplicateOf": null, "duplicateKind": null,
               "traits": ["epub3", "isbn"], "embedded": { "title": "...", "authors": ["..."], "isbn13": null, "language": "en" } } ]
}
```

`--plan` prints `summary` with `totalBytes` omitted. Paths use `/`. Arrays ordinal-sorted by key/path. JSON written with `\n` line endings, no BOM.

### 3.5 Content generation

- **PRNG:** own `SplitMix64` (seeding/derivation) and `Xoshiro256StarStar` (draws); `NextInt(bound)` via Lemire's unbiased multiply-shift. `System.Random` is not used (its seeded algorithm is not a documented cross-version contract). Each work gets its own generator seeded by `SplitMix64(seed ⊕ workIndex·K)`, so work generation is order-independent and may run in `Parallel.For` while staying deterministic.
- **Text:** chapters (5–12), paragraphs (40–120 words), words sampled from the language vocabulary with Zipf weights computed as integers `floor(10^9 / rank)` and a cumulative integer table (no `Math.Pow`/`Math.Log`: libm results may differ across platforms). Total words per work uniform in `[words-min, words-max]` (default 3,000–8,000, so the PRD's ~5,000-word SimHash window is mostly filled). EPUB and PDF of one work carry the same running text; PDF adds a title page.
- **Vocabulary:** hand-authored for this repo (no copied frequency lists, no third-party text): `en.txt`, `de.txt` (~800 general + ~200 technical words each; German includes ä/ö/ü/ß), name lists with diacritics, topic lists for title templates such as `Patterns of {Topic}`, `{Topic} in Practice`, `Grundlagen der {Topic}`. Embedded resources. Every character used in PDF body text must be encodable in WinAnsiEncoding (unit-tested).
- **EPUB writer:** `ZipArchive` over a `MemoryStream`, then written to disk. Entry order: `mimetype` (stored, content `application/epub+zip`, no extra field), `META-INF/container.xml`, `OEBPS/content.opf`, nav/NCX, chapters, cover. All entries `CompressionLevel.NoCompression` unless `--deflate`; `LastWriteTime` fixed `2000-01-01T00:00:00Z`. EPUB 3: `package version="3.0"`, `dc:identifier` (`urn:isbn:` or `urn:uuid:` derived from the work hash), `dcterms:modified` fixed `2000-01-01T00:00:00Z`, `refines` for `file-as`/`role`, `belongs-to-collection` + `collection-type` + `group-position`, `nav.xhtml` (`properties="nav"`), cover `properties="cover-image"`. EPUB 2: `version="2.0"`, `opf:file-as`/`opf:role`/`opf:scheme`, `toc.ncx` with `spine toc="ncx"`, `<meta name="cover">`, `calibre:series`/`calibre:series_index`. XHTML 1.1 for EPUB 2, XHTML5 for EPUB 3; text XML-escaped. XML built as strings with `\n` newlines (or `XmlWriter` with explicit `NewLineChars="\n"`, no BOM).
- **PDF writer:** `%PDF-1.7` + binary comment line; objects Catalog, Pages, Page (US Letter 612×792), one Type1 standard font `Helvetica` with `/Encoding /WinAnsiEncoding`, uncompressed content streams (`BT /F1 11 Tf 14 TL 72 720 Td (..) Tj T* ... ET`), about 45 lines per page; Info dictionary (Title, Author, Subject, Keywords, Creator, Producer `shelf synthetic library generator`, CreationDate/ModDate `D:20000101000000Z`); optional XMP `/Metadata` stream (dc:title, dc:creator, pdf:Producer); non-ASCII Info strings as UTF-16BE with BOM (hex string), body text in WinAnsi with `(`, `)`, `\` escaped; xref with exact byte offsets (20-byte entries), trailer `/Size /Root /Info /ID` (ID from the work hash). Image-only page: `/XObject` 1-bit DeviceGray image, no filter, drawn with `cm`/`Do`.
- **PNG writer:** signature, IHDR (8-bit RGB 64×96), IDAT as zlib stream with **stored** deflate blocks (hand-written, deterministic) + Adler-32, IEND; CRC32 hand-written (no System.IO.Hashing package). Color derived from the work hash.

### 3.6 AOT, native-dependency and determinism risks

- **Not in the shelf binary:** `Shelf.TestData` lives under `tests/`, is referenced only by `Shelf.Specs` and `Shelf.TestData.Tests`, and has no `IsAotCompatible`. The CI `aot` job publishes only `src/Shelf.Cli`, so nothing changes there. A guard test fails if any `src/**/*.csproj` mentions `Shelf.TestData`.
- **No native dependencies.** `System.IO.Compression` (in-box; its zlib-ng native part is in the shared runtime) is used only for ZIP container writing with stored entries. VersOne.Epub (Unlicense, ADR 0001 row 4) and PdfPig (Apache-2.0, row 5) are managed and used only in `Shelf.TestData.Tests`, JIT. No new license review is needed; System.CommandLine is row 3.
- **Exe referenced by Exe:** `Shelf.Specs` (Exe) already references `Shelf.Cli` (Exe), so referencing `Shelf.TestData` (Exe) follows a working precedent. If NETSDK1150/1151 or a `runtimeconfig`/`deps.json` conflict appears, split into `Shelf.TestData` (classlib) + `Shelf.TestData.Generator` (Exe) and report.
- **Determinism hazards and countermeasures:** deflate output (zlib-ng may differ by CPU path and runtime version) → stored by default; ZIP header fields (`version made by` is Unix on all four targets; fixed timestamps) → verified by the golden digest on all four CI platforms; .NET SDK updates may still change ZIP bytes → golden digest fails loudly, update it with a note in the PR; culture (`CultureInfo.InvariantCulture` everywhere, test under `de-DE`); `Environment.NewLine` never used; floating point avoided in all decisions; enumeration order (ordinal sorts, no `HashSet`/`Dictionary` iteration for output order); no `Guid.NewGuid`, `DateTime.Now`, assembly version or git hash in output (`InformationalVersion` contains the commit hash, hence `generatorFormat`).
- **Size/runtime of the reference run (estimate, to be measured in step 12):** ~40 KB per EPUB, ~45 KB per PDF stored, so about 0.6–0.8 GB for 15,000 files; `--deflate` roughly a third of that. Runtime target < 10 min on an M-series machine; measured value goes into `tests/Shelf.TestData/README.md`.
- **Parser readability ≠ spec validity:** VersOne.Epub and PdfPig are lenient. epubcheck and `qpdf --check` are run manually on a sample (step 11); findings are fixed or documented.

## 4. Unit Tests (`tests/Shelf.TestData.Tests`)

xunit.v3, `OutputType=Exe`, `IsPackable=false`; references `Shelf.TestData`, `VersOne.Epub`, `PdfPig`; `Shelf.TestData` has `InternalsVisibleTo Shelf.TestData.Tests`.

| Unit | Cases |
|---|---|
| `SplitMix64`, `Xoshiro256StarStar` | First 5 outputs for seeds 0 and 42 equal golden values computed once with the reference C code (https://prng.di.unimi.it/) and frozen; `NextInt(bound)` stays in `[0, bound)` for bounds 1, 2, 3, 1000, `int.MaxValue`; bound 0 throws. |
| `Crc32`, `Adler32` | `"123456789"` → `0xCBF43926`; CRC of chunk type `IEND` with empty data → `0xAE426082`; Adler-32 of `"Wikipedia"` → `0x11E60398`. |
| `Isbn` | Check digit of `978030640615` → 7, `978144937332` → 0, `978000000001` → 9; formatting with hyphens for the derived fixture; generated ISBNs always validate. |
| `LibraryPlanner` | Theory over (w, f) → (a, b, c, epub, pdf, exact, same-format, editions, hard negatives, series) for 10/10, 10/15, 10/21, 20/30, 20/40, 100/150, 200/300, 10,000/15,000 (values as 2.1/3.4); `maxFiles(10) == 21`; (10, 22) and (10, 9) rejected. |
| `NameVariants`, `TitleVariants` | Exact outputs for the examples in 3.4; `author-no-diacritics` only applies to names with diacritics; variants are deterministic per seed. |
| `FileNames` | Each pattern for a fixed work; sanitizing of `a/b:c*?`; 130-char title truncated to 120 before the extension without splitting a surrogate pair; NFC; collision suffixes ` (1)`, ` (2)`. |
| `TextGenerator` | Word count within `[min, max]`; same seed and work → same text; edition text shares about 85 % of paragraphs (exact count for a fixed seed); all PDF body characters encodable in Windows-1252 subset of WinAnsi. |
| `EpubWriter` | First local header: signature, method 0, name `mimetype`, extra length 0, general-purpose bit 3 clear, content at offset 38; `container.xml` points to the OPF; OPF parses with `XDocument`; EPUB 3 has nav + `dcterms:modified`, EPUB 2 has NCX and no nav; `&`, `<` in titles escaped. **VersOne.Epub 3.3.6** reads title, authors, ISBN identifier, reading order (first 20 words of chapter 1) and the cover when present, for EPUB 2 and 3. |
| `PdfWriter` | Header; every xref offset points at `N 0 obj`; `startxref` correct; literal string escaping; UTF-16BE title round trip. **PdfPig 0.1.16** opens each variant: page count equals ground truth, Info title/author, page-1 text starts with the expected words incl. umlauts, XMP stream parses as XML, image-only page has no letters and one image, no-metadata variant has an empty Info. |
| `PngWriter` | Signature, IHDR fields, chunk CRCs; IDAT inflates (`ZLibStream`) to `96 × (1 + 64·3)` bytes with filter byte 0. |
| Golden digest | `generate --seed 42 --preset tiny` into a temp dir: SHA-256 over the ordinal-sorted list of `<path>\t<sha256>\n` lines equals a frozen constant. Runs on all four CI platforms (cross-platform determinism proof). Same digest with `CultureInfo.CurrentCulture = de-DE` and with `Parallel` disabled vs enabled. |
| Derived fixtures | `DerivedFixtures` output parses with VersOne/PdfPig as intended (except the deliberately broken files, which must fail to parse or return empty metadata; record the observed behavior). |
| Upstream fixtures | Every upstream EPUB opens with VersOne.Epub and its title, language and EPUB version equal the manifest; the upstream PDF opens with PdfPig and has text on page 1. |
| `FixtureCatalog` | Unknown trait or origin → exception with the path; missing field → exception; paths with `\` rejected. |
| Guard | No `src/**/*.csproj` contains `Shelf.TestData`. |
| Reference run | `[Fact(Explicit = true)]`: `--preset reference` into a temp dir; asserts the 2.1 plan values against the ground truth and writes elapsed time and total bytes to the test output. Not part of the default run or CI. |

## 5. Step Order

1. **Feature files.** Add `SyntheticLibrary.feature` and `Fixtures.feature` (2.1, 2.2). `dotnet test -c Release`: new scenarios red, existing green.
2. **Fixture research (manual, no code).** For U1–U5: open the source pages, verify the licensing claims of 3.3 and quote them with URLs and retrieval dates; check PD basis (publication year, death years of author/translator/illustrator); download each file once by hand into a scratch directory; inspect (`unzip -l`, read OPF, `file`, size). Pick files to stay within the budget. If a claim cannot be verified or a row of 2.2 cannot be satisfied, stop and ask (Q2, Q3).
3. **Scaffolding.** Create `tests/Shelf.TestData` and `tests/Shelf.TestData.Tests`, add them to `shelf.slnx`; add VersOne.Epub 3.3.6 and PdfPig 0.1.16 to `Directory.Packages.props`; `.gitignore` `/artifacts/`; `.gitattributes`. `dotnet build -c Release` clean.
4. **Primitives, test first.** Unit tests for PRNG, CRC32, Adler-32, ISBN, `LibraryPlanner` (red); implement until green.
5. **Format writers, test first.** `PngWriter`, `EpubWriter`, `PdfWriter` with their unit tests incl. VersOne/PdfPig readability.
6. **Content.** Vocabulary files, `TextGenerator`, `NameVariants`, `TitleVariants`, `FileNames`; unit tests.
7. **Generator and ground truth.** `LibraryGenerator`, `GroundTruth`, `GeneratorApp` (`generate`, `--plan`, validation); write `GeneratorSteps.cs` alongside until `SyntheticLibrary.feature` is green.
8. **Derived fixtures.** `DerivedFixtures` + `fixtures` command; run it into `tests/fixtures/derived/`; unit tests for derived files.
9. **Commit upstream fixtures and manifest.** Copy the files from step 2 unmodified; write `manifest.json` (sizes/hashes computed, traits from inspection) and `tests/fixtures/README.md` (provenance table, quoted license terms, PD rule, jurisdiction caveat, PG trademark rule, how to add or refresh a fixture, budget). `FixtureCatalog`, `RepositoryPaths`, `FixtureSteps.cs` until `Fixtures.feature` is green.
10. **Determinism.** Freeze the golden digest; run the suite under `LANG=de_DE.UTF-8 LC_ALL=de_DE.UTF-8`; identical results.
11. **Manual validation.** If Java is available: epubcheck on all derived EPUBs (except the deliberately broken ones) and on 20 generated EPUBs (EPUB 2 and 3); `qpdf --check` on the derived PDFs and 20 generated PDFs if qpdf is available. Fix errors; document remaining warnings in `tests/Shelf.TestData/README.md`. Tools are not added to the repo or CI.
12. **Reference run.** Run the explicit test or `generate --preset reference --out artifacts/synthetic-library` in Release on the dev machine; record elapsed time, total bytes, file counts and machine in `tests/Shelf.TestData/README.md`. Confirm `git status` shows nothing under `artifacts/`.
13. **Quality gates.** `dotnet format --verify-no-changes`; `dotnet build -c Release`; `dotnet test --no-build -c Release` all green; no `NoWarn`; `src/` unchanged; `du -sb tests/fixtures` (or `find ... -exec stat`) within budget.
14. **Roadmap.** M0 table, Test fixtures row: append "**Done:** N fixtures in `tests/fixtures/` (manifest + provenance), deterministic generator `tests/Shelf.TestData` ([slice plan](slices/m0-fixtures.md)); reference run not in CI".
15. Push, open the PR (reference-run numbers, fixture list with sizes, epubcheck/qpdf findings); all `ci` checks green on four platforms (this is where the golden digest proves cross-platform determinism).

## 6. Open Questions

- **Q1: Hosting of binaries.** Options: (a) plain git (recommended: ≤ 5 MiB, works with the existing `actions/checkout` without LFS settings, no LFS quota, offline clones complete); (b) git-lfs (needs `lfs: true` in CI checkouts and LFS bandwidth quota; not worth it at this size); (c) download script (CI depends on PG/SE availability; PG robot policy forbids automated crawling; hashes would still need pinning). Revisit only if fixtures exceed the budget.
- **Q2: Fixture selection.** The candidates U1–U5 are unverified. Decide: (a) which short works (recommendation: Gilman *The Yellow Wallpaper* in EPUB 3 + legacy EPUB from PG, plus the Standard Ebooks edition if it exists; a short German PG work); (b) source of the real PDF: CC0 arXiv paper (recommended: CC0 is worldwide, real producer metadata), a US federal government work (public domain only in the USA), or no real PDF; (c) a real image-only scan: recommended no (size, OCR layers), derived instead; (d) whether Wikisource stays excluded (recommended, CC BY-SA obligations); (e) whether a non-Latin-script fixture (Cyrillic, Greek or CJK title) is wanted now for F-GRP-2 normalization; recommendation: add one derived EPUB later if the M1 normalization slice needs it.
- **Q3: Project Gutenberg files in the repo.** Recommendation: commit unmodified with the license, which the PG License permits for free redistribution (to verify, 3.3). Alternative: strip all PG references and license to get plain PD text, which would make them "modified" derived files and loses realism of real-world PG files. The README mentions "Project Gutenberg" only as a factual source; confirm this is acceptable under the trademark terms.
- **Q4: License of derived fixtures and generator output.** Plan: GPL-3.0-or-later like the repo (test data generated by repo code). Alternative: CC0 for all generated test data so others can reuse it freely. Owner decision.
- **Q5: Generator location.** Plan: `tests/Shelf.TestData` (test infrastructure, matches PRD 10.1 `tests/` and the `Shelf.*` naming, referenced by specs in-process). Alternatives: `tools/Shelf.TestData` (new top-level folder not in PRD 10.1) or a packaged `dotnet tool` (manifest + pack step, no benefit now). Confirm.
- **Q6: Synthetic languages and locales.** Plan: English 75 %, German 25 % (PRD 4: "partly German, partly English"), hand-authored vocabularies, names with diacritics from several European languages. Should other languages or scripts appear? Should the ratio be configurable (`--german-share`)? Plan: not configurable.
- **Q7: Text volume and compression.** Plan: 3,000–8,000 words per work (fills the SimHash window, keeps the reference set at ~0.7 GB); real technical books are 20–50 times longer, which matters for the 9.1 targets "initial indexing < 15 min" and chunk counts for M3. Decide in the M1 performance slice whether to raise `--words-*` for benchmarks. Stored ZIP entries by default (byte determinism) vs deflate (smaller, platform-dependent bytes): plan stored, `--deflate` opt-in.
- **Q8: Infrastructure features in `Shelf.Specs`.** The two features test test infrastructure, not product behavior, tagged `@PRD-9.1 @M0` / `@PRD-10.1 @M0` as requested. Alternative: unit tests only. Recommendation: keep the features (they document the contract later slices rely on); confirm.
- **Q9: Parser packages now.** VersOne.Epub and PdfPig enter `Directory.Packages.props` as test-only references before the AOT spike and the M1 extraction slice. Recommendation: yes (licenses cleared in ADR 0001; early real-world exposure); the extraction slice later adds them to `src/` with its own AOT check.
- **Q10: Reference run in CI.** Not run by default. Options: `workflow_dispatch` job, nightly job, or part of the M1 benchmark job (roadmap: benchmarks in CI from M1 on). Recommendation: decide in M1 together with benchmark hardware/variance; generation cost (~0.7 GB, minutes) per run.
- **Q11: Synthetic ISBNs.** Valid-checksum ISBNs may belong to real books. No reserved test range exists (to verify). Recommendation: accept; never use them for online lookup (F-IMP-6 is off by default anyway).
- **Q12: Acceptance-criterion-1 shape.** AC1 needs 500 files including exactly 100 EPUB+PDF works plus an expected-merge list. Plan: the M1 grouping calibration slice adds `--preset ac1` on top of this generator and its ground truth. Confirm.
- **Q13: Exit code for a non-empty output directory.** Plan: 1 (runtime error, not a usage error), mirroring shelf's 0/1/2 convention. Confirm.
