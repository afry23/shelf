# ADR 0003: TOML parser

| | |
|---|---|
| **Status** | Accepted: CsToml 1.8.6; Native AOT confirmed on `osx-arm64` and `linux-x64` |
| **Date** | 2026-10-14 |
| **Scope** | PRD 9.2 (config locations, Native AOT, single binary), F-GRP-6 (thresholds in `config.toml`; reader only), 7.2 (`mcp.readOnly` as an example key) |
| **Roadmap** | M0 exit criterion "CLI framework and TOML parser are chosen"; TOML parser line of the "AOT spike" work package (osx-arm64 part only) |
| **Inputs** | [ADR 0001](0001-dependency-licenses-and-maintenance.md) (license policy and research method; TOML excluded there by decision Q5), [ADR 0002](0002-cli-framework.md) (gate format, benchmark method), slice plan `docs/slices/m0-toml-parser.md` |

## Context

shelf reads settings from `~/.config/shelf/config.toml` (global) and `<root>/.shelf/config.toml` (per library), PRD 9.2. F-GRP-6 puts the grouping thresholds there. The reader must work in the Native AOT single binary (PRD 9.2) without reflection, and be a small, replaceable part of `Shelf.Core`. ADR 0001 deliberately did not assess TOML libraries; this ADR does, with the same method (NuGet metadata, `.nupkg` listing, repository license file, release list, commit activity, project file at the release commit; read-only, nothing downloaded was executed).

Target format is TOML 1.0.0. Whether 1.1 is also accepted is recorded per candidate, not required (owner decision, slice question Q9).

## Options

- **A. Tomlyn 2.10.1** (xoofx/Tomlyn, BSD-2-Clause).
- **B. Tomlet 6.2.0** (`Samboy063.Tomlet`, SamboyCoding/Tomlet, MIT).
- **C. CsToml 1.8.6** (prozolic/CsToml, MIT).
- **D. Tommy 3.1.2** (dezhidki/Tommy, MIT).
- **E. Nett 0.15.0** (paiden/Nett, MIT): screening only.
- **F. Hand-written TOML 1.0 subset parser in `Shelf.Core`**: documented fallback, prototyped only if A to D all fail. Not needed.

The latest **stable** version of each is assessed; prereleases are not considered (same rule as ADR 0002).

## Criteria (fixed before measuring)

Gates, weights and bands below were confirmed by the project owner (slice questions Q1 and Q2) before any measurement was taken, with one change to the plan: **G2 (archived repository or deprecated package) is a flag, not a hard gate**, like the maintenance signals in ADR 0001. Q2: the write path is measured and recorded with a weight of 10 % but is not integrated. This section was written before `measure.sh` was run, but the criteria were not committed separately before the measurements, so this order is documented here and not provable from the repository history. A smoke test of each prototype on one sample file (to make sure the prototypes build and print a value) was done earlier; no gate or score was evaluated at that time.

### Gates (a candidate that fails a gate is out, regardless of score)

| ID | Gate |
|---|---|
| G1 | License GPL-3.0-compatible per ADR 0001 policy and FSF list. |
| G2 | **Flag only (owner decision):** repository archived or package deprecated/unlisted on NuGet. Recorded, goes into the maintenance score, does not exclude. |
| G3 | The read path shelf uses is reflection-free: untyped DOM or source-generated types; no API annotated `RequiresUnreferencedCode`/`RequiresDynamicCode` on that path. |
| G4 | AOT publish (`osx-arm64`, `-p:TrimmerSingleWarn=false`) with zero `IL*`/AOT warnings, no `NoWarn`, no `SuppressTrimAnalysisWarnings`. |
| G5 | All functional checks pass on the AOT binary: every row of `ConfigFile.feature` (values, absent keys, errors with the expected line). |
| G6 | Claims TOML 1.0.0 support. |

### Weighted score (1 to 5 per criterion)

| Criterion | Weight | Scoring |
|---|---|---|
| Conformance on the hand-written corpus (valid accepted with correct values, invalid rejected) | 25 % | 100 %: 5, >= 95 %: 4, >= 90 %: 3, >= 80 %: 2, less: 1 |
| Error reporting (line and column present and correct on the invalid corpus; message usable for a user) | 15 % | Judgement, justified |
| Binary size overhead vs. Baseline | 15 % | <= 0.5 MB: 5, <= 1 MB: 4, <= 2 MB: 3, <= 4 MB: 2, more: 1 (MB = 10^6 bytes) |
| Load time overhead vs. Baseline (`get sample/config.toml grouping.auto_merge`, median) | 10 % | <= 1 ms: 5, <= 3 ms: 4, <= 10 ms: 3, <= 30 ms: 2, more: 1 |
| Maintenance (ADR 0001 signals and flags, incl. G2) | 15 % | Judgement |
| API fit (lines in the adapter mapping to `ConfigDocument`, type coverage incl. datetimes, no leaking types) | 10 % | Judgement |
| Write capability (round-trip, recorded, not integrated) | 10 % | 1 point each: unchanged round-trip byte-identical; comments kept after edit; key order kept; formatting/whitespace kept; new key insertable into an existing table; max 5 |

If no candidate passes all gates (G1, G3 to G6), the decision is escalated to the project owner and nothing is integrated.

### Benchmark method

Prototypes in `experiments/toml-parser/` (outside `shelf.slnx`, not built by CI, own props that do not import the root, `TreatWarningsAsErrors=false`, `PublishAot=true`, `IsAotCompatible=true`, `InvariantGlobalization` not set): `Baseline` (reads the file, no parser), `Tomlyn`, `Tomlet`, `CsToml`, `Tommy`. All share one harness (`Shared/Harness.cs`) with three commands: `get <file> <key>`, `corpus <dir>`, `roundtrip <in> <outPrefix>`. Corpus: 38 valid and 23 invalid hand-written files from the TOML 1.0.0 specification (no third-party test files); checks (G5): 16 valid and 6 invalid files that replicate the rows of `ConfigFile.feature`. `measure.sh` publishes each prototype with `-c Release -r osx-arm64 -p:TrimmerSingleWarn=false`, counts warnings by code, runs checks and corpus on the AOT binary, assesses the write path, records size and load time (5 warmups, 50 runs), and re-runs the float read under `LANG=de_DE.UTF-8`.

## Research

Retrieved read-only on 2026-10-14 (source index at the end). Nothing downloaded was executed; `.nupkg` files were unzipped and listed only (the prototypes that use them were built later, see Measurements). "flag" = maintenance risk flag as defined in ADR 0001 (no release in 12 months, or no commit in 6 months, or a single maintainer without an organization). Date of research: 2026-10-14, so "last 6 months" means since 2026-04-14 and "last 12 months" since 2025-10-14.

| | Tomlyn | Tomlet | CsToml | Tommy | Nett |
|---|---|---|---|---|---|
| Package ID | `Tomlyn` | `Samboy063.Tomlet` | `CsToml` | `Tommy` | `Nett` |
| Latest stable (date) | 2.10.1 (2026-07-02) | 6.2.0 (2025-12-24) | 1.8.6 (2026-08-15) | 3.1.2 (2022-01-11) | 0.15.0 (2020-03-02) |
| Newer prerelease | none | none | none | none | none |
| License (nuspec / repo file) | BSD-2-Clause / BSD 2-clause text (`license.txt`) | MIT / MIT (`LICENSE`) | MIT / MIT (`LICENSE`) | file `LICENSE` (MIT text) / MIT | MIT / MIT |
| GPL-3.0 compatible (FSF list, S1) | yes (FreeBSD license) | yes (Expat) | yes (Expat) | yes (Expat) | yes (Expat) |
| Archived / deprecated / unlisted (G2 flag) | no / no / no | no / no / no | no / no / no | no / no / no | **archived** / no / no |
| Stable releases, last 12 months | 23 | 1 | 9 | 0 | 0 |
| Commits, last 6 months (last commit) | 26 (2026-08-18) | 1 (2026-08-17) | 23 (2026-10-06) | 0 (2022-01-16) | 0 (2020-04-22) |
| Contributors; main author's share | 19; xoofx 354 | 14; SamboyCoding 53, next 11 | 3; prozolic 579, next 10 (dependabot) | 4; dezhidki 218, next 3 | 8; paiden 495, next 5 |
| Open issues + PRs | 5 | 15 | 4 | 14 | 13 |
| Owner type | User | User | User | User | User |
| Maintenance flags | single maintainer (no org) | single maintainer; 1 commit in 6 months | single maintainer (no org) | no release in 12 months, no commit in 6 months, single maintainer | archived, same three flags |
| TOML version claim | **1.1.0 only; README: "does **not** support TOML 1.0"** (package description: "TOML 1.1 parser") | 1.0.0 ("The entire TOML 1.0.0 specification is implemented", README badge) | 1.0.0 by default, 1.1.0 optional (`TomlSpec.Version100`/`Version110`); README claims the toml-test suites pass (not verified here) | 1.0.0 ("Full implementation of TOML 1.0.0 spec") | no version claim found in README/nuspec (screened out) |
| AOT/trim claim | `IsAotCompatible=true`, `IsTrimmable=true` for net8.0/net10.0 (project file at the release commit); README "NativeAOT ready"; reflection-based mapping is annotated `RequiresUnreferencedCode`/`RequiresDynamicCode` | `IsTrimmable=true` only, no `IsAotCompatible` | `IsAotCompatible=true`; README "Core APIs are compatible with Native AOT" | no claim | no claim (reflection-based) |
| TFMs in package | net10.0, net8.0, netstandard2.0 | net9.0, net8.0, net7.0, net6.0, netstandard2.0, net35 | net10.0, net9.0, net8.0 | netstandard2.0, net35 | netstandard2.0, net40 |
| Sources | S3, S4, S5 | S6, S7 | S8, S9 | S10 | S11 |
| Native assets (`runtimes/`) | none | none | none | none | none |
| Dependencies on net10.0 | none | none | `System.IO.Hashing` 10.0.11 (Microsoft, MIT) | none (`Microsoft.NETFramework.ReferenceAssemblies` is a build-time reference package) | netstandard2.0 only |
| Source generator | yes (analyzer in package, optional) | no | `CsToml.Generator` (separate package, not needed) | no | no |

### NuGet screening (S2 for the search; S12 for the nuspec files of the wrapper packages; query `toml`, 352 hits, the first 10 hits of the search sorted by total downloads, stable only; the search service's order is not strictly by download count)

| Package | Downloads | Verdict |
|---|---|---|
| Tomlyn.Signed | 20.0 M | Signed build of Tomlyn (same commit and code); covered by option A. |
| Nett | 8.0 M | Option E: archived 2020, no TOML 1.0 claim found, reflection-based mapper; out (G6, G2 flag). |
| Tomlyn | 6.1 M | Option A. |
| Alexinea.Extensions.Configuration.Toml | 187 k | `Microsoft.Extensions.Configuration` provider on top of Nett 0.15.0 (file license); not a parser. |
| Carbon.Toml | 100 k | "A simple TOML parser", depends on `Carbon.Json`, no TOML version claim, 0.x; out (G6, not claimed). |
| Samboy063.Tomlet | 540 k | Option B. |
| CsToml | 21 k | Option C. |
| Tommy | 185 k | Option D. |
| Logary.TOML | 30 k | Part of the Logary logging library (F#, FParsec), Apache-2.0 URL only, not a standalone parser; out. |
| Tomlyn.Extensions.Configuration | 88 k | Configuration provider on top of Tomlyn 2.10.1; not a parser. |

Seen and not screened further: `PicoToml` (nuspec has no license field, serializer for the PicoHex ecosystem, 8 k downloads; out by G1 until a license is declared), `CsToml.Generator`/`CsToml.Extensions*` (parts of option C), `Flxble.Toml`, `HyperTomlProcessor` (old, 6 to 9 k downloads).

On paper, Tomlyn fails G6 (TOML 1.1.0 only) and Nett fails G6. The plan drops such candidates before prototyping; Tomlyn was prototyped and measured anyway, **for information only**, because it is the best-known library and the owner may want to know what the G6 reading costs (see Decision). Nett was not prototyped.

## Measurements

Reproduce: `experiments/toml-parser/measure.sh` (publishes each prototype with `dotnet publish <p> -c Release -r osx-arm64 -p:TrimmerSingleWarn=false`, no suppressions). The prototypes stay in the repository until M0 closes and are then deleted (see Consequences). `results/measure.out` is the unedited output of `./measure.sh > results/measure.out 2>&1`.

**Environment:** 2026-10-14, macOS 26.6.2 arm64, Apple M4, 24 GiB RAM, .NET SDK 10.0.401, machine on **AC power**. `hyperfine` was not installed, so the documented fallback was used: a zsh loop with `$EPOCHREALTIME`, 5 warmup runs, 50 measured runs, stdout to `/dev/null`, process start to exit (includes `fork`/`exec`). Numbers are local `osx-arm64` only; the differences between prototypes are what counts. Library versions: Tomlyn 2.10.1, Tomlet 6.2.0, CsToml 1.8.6, Tommy 3.1.2.

### Results (osx-arm64, Native AOT)

| | Baseline | Tomlyn | Tomlet | CsToml | Tommy |
|---|---|---|---|---|---|
| AOT/trim warnings (distinct lines, G4) | 0 | **0** | **0** | **0** | **0** |
| Checks, `ConfigFile.feature` rows (G5), passed | n/a | 22/22 (after workaround, see notes) | **21/22** (`n = 01` accepted) | 22/22 | 22/22 |
| Corpus passed (38 valid + 23 invalid) | n/a | 59/61 | 57/61 | **61/61** | 59/61 |
| Corpus misses | n/a | invalid accepted: integer overflow, trailing comma in inline table (valid in 1.1) | valid rejected/not found: whitespace in table header `[ j . "k" . 'l' ]`; invalid accepted: `n = 01`, table redefined via dotted keys, trailing comma in inline table | none | invalid rejected with a raw exception instead of a parse error: empty hex (`ArgumentOutOfRangeException`), integer overflow (`OverflowException`) |
| Error line correct (of rejected files with expected line) | n/a | 21/21 | 20/20 | 23/23 | 21/21 |
| Error column reported | n/a | yes | no | no | yes |
| Binary size (B) | 1,518,576 | 2,281,504 | 3,193,968 | 3,774,656 | 2,945,320 |
| Size overhead vs. baseline | - | +762,928 B (0.76 MB) | +1,675,392 B (1.68 MB) | +2,256,080 B (2.26 MB) | +1,426,744 B (1.43 MB) |
| Output directory | binary + `.dSYM` | binary + `.dSYM` | binary + `.dSYM` | binary + `.dSYM` | binary + `.dSYM` |
| `get sample/config.toml grouping.auto_merge` median / p95 / min / max (ms) | 3.37 / 4.16 / 3.14 / 4.83 | 7.80 / 8.16 / 7.54 / 8.50 | 8.88 / 9.71 / 8.52 / 9.84 | 3.84 / 5.29 / 3.46 / 5.68 | 8.03 / 8.67 / 7.70 / 9.05 |
| Overhead vs. baseline (median) | - | +4.43 ms | +5.51 ms | +0.47 ms | +4.66 ms |
| `de_DE` run, float output | n/a | `float:0.9` | `float:0.9` | `float:0.9` | `float:0.9` |
| Adapter size in prototype (non-blank lines of `Program.cs`, incl. write path) | 9 | 64 | 74 | 79 | 59 |

### Write path (not integrated, Q2)

Input `sample/config.toml` (1 KB, 9 comment lines, tables, arrays of tables, inline table, multi-line array). Edit: set `grouping.auto_merge = 0.85`, add `grouping.new_key = "x"`.

| | Tomlyn (syntax tree) | Tomlet | CsToml | Tommy |
|---|---|---|---|---|
| Unchanged round-trip byte-identical | **yes** | no (comment lines 9 -> 6, reformatted) | no (comment lines 9 -> 2, comments dropped) | no (comment lines 9 -> 3, arrays reflowed, tables reordered) |
| Comments kept after edit | no, 8 of 9 (the trailing comment of the replaced value is lost) | no (2) | no (0) | no (2) |
| Key order kept | yes | no | no | no |
| Formatting/whitespace kept | yes (3 changed lines in the diff) | no (44 changed lines) | no (51) | no (35) |
| New key insertable into an existing table | yes | yes | yes (via a dictionary copy; emitted as inline tables) | yes |
| **Points (max 5)** | **4** | **1** | **1** | **1** |

CsToml's `TomlDocument` has no mutation API; the dictionary copy (`ToDictionary<string, object>()`) loses comments, order and formatting, and re-emits every table as an inline table. Tomlyn is the only candidate with a lossless syntax tree.

### Notes per candidate

- **Tomlyn 2.10.1.** Reflection-free read path requires a source-generated context (`[TomlSerializable(typeof(TomlTable))]`); the convenience overload `TomlSerializer.Deserialize<T>(string, TomlSerializerOptions)` is annotated `RequiresUnreferencedCode`/`RequiresDynamicCode` (verified by reflection, S13 and by the build warnings of a first attempt). **`TomlSerializer.Deserialize` silently accepts duplicate keys and duplicate tables, also with `DuplicateKeyHandling.Error`** (verified); only `SyntaxParser.ParseStrict(text, name, validate: true)` reports them, so a correct reader parses twice (in a preliminary run before `measure.sh`, without it 20 of 22 checks passed; `measure.out` records only the 22/22 result with it). Parses TOML 1.1: the trailing comma in an inline table is accepted. Integer `9223372036854775808` is accepted without error (corpus). Error messages carry line and column but also a prefix (`input(2,4) : error : ...`).
- **Tomlet 6.2.0.** Plain DOM (`TomlParser.Parse` -> `TomlDocument`), no annotated API on the path, `IsTrimmable` only. `LineNumber` of `TomlExceptionWithLine` is protected; the public surface exposes the line only inside the message text ("... on line 3"), which the prototype extracts by string search. No column. Accepts `n = 01` (leading zero), a table redefined through dotted keys, a trailing comma in an inline table; does not find keys under `[ j . "k" . 'l' ]` (whitespace around dots in a header). Float getters return `Single` (`GetFloat`), `TomlDouble.Value` is the double.
- **CsToml 1.8.6.** `CsTomlSerializer.Deserialize<TomlDocument>(ReadOnlySpan<byte>)` takes UTF-8 bytes (no string round trip when reading a file). Untyped nodes are `TomlDocumentNode` structs with a `TomlValueType` enum; an absent key yields a node of type `Empty` (no exception), an empty table needs `IsTableHeader`. `document.ToDictionary<string, object>()` gives a plain tree (`string`, `long`, `double`, `bool`, `DateTimeOffset`, `DateTime`, `DateOnly`, `TimeOnly`, `object[]`, `Dictionary`), which the adapter maps into shelf's own tree; datetimes arrive as .NET values, not as source text. Parse failure is `CsTomlSerializeException.ParseExceptions`, each with a public `LineNumber`; no column. **The exception `Message` embeds the exception type name and a stack trace**, which the adapter strips. Messages are terse (`Unexpected value found.`, and `Escape characters 10 were included.` for a newline in a basic string). 1.0 is the default; 1.1 is opt-in. Only candidate with 61/61 on the corpus and the lowest load overhead (no culture-sensitive text APIs on the path, byte-based parser).
- **Tommy 3.1.2.** Simplest API (`TOML.Parse(TextReader)`, `TomlNode.IsTable`/`TryGetNode`), parse errors in `TomlParseException.SyntaxErrors` with line and column. Two invalid inputs escape as raw `OverflowException` / `ArgumentOutOfRangeException`, so a robust adapter must catch all exceptions. Last release January 2022, no commit since April 2022.

Raw logs, per-check output and warning lists are kept locally in `experiments/toml-parser/results/` (git-ignored except `measure.out`). Timing varies from run to run; the differences between CsToml and the other three (4 to 5 ms) are far above the noise and the scores do not change. Sizes and warning counts are deterministic.

## Gate results

| Gate | Tomlyn | Tomlet | CsToml | Tommy |
|---|---|---|---|---|
| G1 license | pass (BSD-2-Clause) | pass (MIT) | pass (MIT) | pass (MIT) |
| G2 flag: archived/deprecated | no | no | no | no |
| G3 reflection-free read path | pass (source-generated context; reflection overload annotated and not used) | pass (no annotated API; 0 warnings) | pass (no annotated API; 0 warnings) | pass (no annotated API; 0 warnings) |
| G4 AOT publish, 0 warnings | pass (0) | pass (0) | pass (0) | pass (0) |
| G5 functional on AOT binary | pass (22/22 with the `ParseStrict` workaround; 20/22 without it in a preliminary run, not in `measure.out`) | **fail** (21/22: `n = 01` accepted) | pass (22/22) | pass (22/22) |
| G6 claims TOML 1.0.0 | **fail** (1.1.0 only) | pass | pass | pass |
| Result | **out (G6)** | **out (G5)** | candidate | candidate |

## Scores (candidates that pass all gates; the other two are scored for information)

| Criterion (weight) | CsToml | Tommy | Tomlet (out) | Tomlyn (out) | Basis |
|---|---|---|---|---|---|
| Conformance (25 %) | 5 | 4 | 3 | 4 | 100 %, 96.7 %, 93.4 %, 96.7 % of the corpus |
| Error reporting (15 %) | 2 | 5 | 3 | 4 | Line always correct for all four. Tommy: line and column, clean messages. Tomlyn: line and column, noisy prefix, parses twice. Tomlet: line only (extracted from the message), readable. CsToml: line only; message embeds type name and stack trace (stripped), terse reasons. |
| Binary size (15 %) | 2 | 3 | 3 | 4 | +2.26, +1.43, +1.68, +0.76 MB |
| Load time (10 %) | 5 | 3 | 3 | 3 | +0.47, +4.66, +5.51, +4.43 ms |
| Maintenance (15 %) | 4 | 1 | 3 | 4 | CsToml: 9 releases and 23 commits in the window, flag: single maintainer. Tommy: three flags, no activity since 2022. Tomlet: 1 release, 1 commit, flag: single maintainer. Tomlyn: 23 releases, 26 commits, flag: single maintainer. |
| API fit (10 %) | 3 | 4 | 3 | 3 | Tommy: simplest DOM, TextReader input, line and column, needs a catch-all. CsToml: byte input (good), `ToDictionary` gives a clean tree, but absent keys are `Empty` nodes, error message cleanup needed. Tomlet: typed nodes, line only through message text. Tomlyn: needs a second validating pass for correctness. |
| Write capability (10 %) | 1 | 1 | 1 | 4 | see write path table |
| **Weighted total** | **3.35** | **3.15** | 2.80 | 3.80 | e.g. CsToml: 0.25 x 5 + 0.15 x 2 + 0.15 x 2 + 0.10 x 5 + 0.15 x 4 + 0.10 x 3 + 0.10 x 1 |

Sensitivity: the harness classified a raw exception on invalid input as a failure from the start (the harness text predates the measurements). If those two Tommy cases were counted as "rejected", Tommy would score 5 for conformance and 3.40, a tie with CsToml (3.35) within the judgement margin. The decision below therefore also rests on the maintenance gap (Tommy: no release since January 2022, no commit since April 2022) and the load time, not only on the total.

## Decision

**Option C: CsToml 1.8.6** (MIT, TOML 1.0 by default, `IsAotCompatible=true`). It passes all gates, scores 3.35, is the only candidate with a clean corpus (61/61), has the lowest load overhead (+0.5 ms; the others cost +4.4 to +5.5 ms), and is actively maintained. Tommy (3.15) also passes all gates but is unmaintained since 2022 and throws raw exceptions on some invalid input. Tomlet fails G5. **Tomlyn fails G6 (TOML 1.1.0 only) and is out, although it has the highest total (3.80)**, driven by its write path (4 of 5 points) and smaller size; if the project owner decides that accepting TOML 1.0 documents is sufficient (G6 relaxed from "claims 1.0.0" to "reads every valid 1.0 document"), Tomlyn would become the candidate to re-open, with the caveats above (two parse passes, accepts 1.1-only syntax and integer overflow, load time +4.4 ms).

Weaknesses accepted with CsToml: terse error messages and no column (the adapter formats `<file>:<line>: <message>`), a stack trace inside the library's exception message (stripped in the adapter), no comment-preserving write path, +2.26 MB binary size, and a one-person project (flag). The adapter (`TomlConfigParser`, one internal file) is the swap point; no library type is public.

Details decided in this slice:

- **Missing file** is an empty configuration; an unreadable file (permissions, directory) or invalid UTF-8 is a `ConfigException` (owner decision Q4).
- **Integer where a float is requested** is widened; a float where an integer is requested is a type mismatch (owner decision Q5).
- **TOML 1.0 is pinned** (`TomlSpec.Version100`) so that 1.1-only syntax (trailing comma in inline tables, newlines in inline tables, times without seconds) is rejected; covered by unit tests.
- **UTF-8 BOM** at the start of a file is accepted and ignored (CsToml 1.8.6 behavior, asserted in a unit test; Q12).
- **Datetimes** are kept as ISO-8601 text rebuilt from the .NET value (`O` for offset datetimes, `yyyy-MM-dd'T'HH:mm:ss.FFFFFFF`, `yyyy-MM-dd`, `HH:mm:ss.FFFFFFF`), not as the original source text, because CsToml does not expose the source. No consumer reads datetimes in M0.
- **Parse errors** have a line (`ConfigException.Line`), no column (CsToml gives none); the message is `<source>:<line>: <reason>` (the format `<source>:<line>:<column>: ...` is used when a column is known, currently never; that branch is therefore unreachable with CsToml and not covered by a test).
- **Quoted keys with a dot** are not addressable by dotted key; a documented limitation.
- **Package version:** `CsToml` 1.8.6 pinned in `Directory.Packages.props`; transitive `System.IO.Hashing` 10.0.11 (Microsoft, MIT) enters the dependency graph (to be listed when ADR 0001's transitive check is done after the AOT spike).

## Consequences

- `src/Shelf.Core` references CsToml 1.8.6. The integrated adapter was additionally published as a Native AOT console app on `osx-arm64` (scratch project, not committed) with `-p:TrimmerSingleWarn=false`: 0 warnings, loads `sample/config.toml`, reports a line for a syntax error. `src/Shelf.Cli` does not call the parser in M0, so the CI `aot` job does not compile it; the `shelf` binary size is unchanged (3,061,848 B on `osx-arm64`).
- **Hand-off to the AOT spike work package (owner decision Q8), done 2026-10-17:** CsToml in an AOT binary on `linux-x64`. `experiments/toml-parser/check.sh <rid>` publishes the CsToml prototype with `-p:TrimmerSingleWarn=false` and runs the G5 checks, the conformance corpus and the round-trip; the non-required workflow `spike-toml.yml` ran it on `osx-arm64` and `linux-x64` (CI run 39041563501 on PR #20, both jobs green). `linux-x64`: 0 AOT/trim warnings, `proto-cstoml` 3,869,640 B, checks 22/22 (lines 6/6), corpus 61/61 (lines 23/23), round-trip ok (the unchanged round-trip is not byte-identical, as already recorded for CsToml on `osx-arm64`), `get grouping.auto_merge` under `de_DE` prints `0.9`. `osx-x64` and `linux-arm64` are no longer supported platforms (PRD 9.2) and were not run. The integrated `Shelf.Core` adapter was not published on `linux-x64`; only the prototype was. The functional (JIT) behavior runs on the supported platforms in the CI `test` job.
- **Write capability (Q2):** CsToml cannot edit a config file without losing comments, order and formatting (1 of 5 points); Tomlyn's syntax tree could (4 of 5). Nothing in the PRD writes `config.toml` (no `shelf config` command). When a writer appears (for example `shelf config set` or `shelf init`), either generate a fresh commented template, use a targeted line edit, or re-open this ADR.
- **Re-check triggers:** CsToml 2.0 or loss of its single maintainer; Tomlyn adding TOML 1.0 mode or the owner relaxing G6; a requirement for comment-preserving edits; the AOT spike failing on another platform.
- **Follow-ups:** M1 wires config loading into `Shelf.Cli`; precedence between global and library files, key naming, unknown-key policy and the library list (F-LIB-1) are separate decisions (slice questions Q3, Q6, Q11). `experiments/toml-parser/` is kept until M0 closes and is then deleted, with this ADR pointing to the last commit that contains it.

## Source index

All sources retrieved read-only on 2026-10-14. `nuspec(id, version)` is `https://api.nuget.org/v3-flatcontainer/<id>/<version>/<id>.nuspec` (lower-case id), `catalog(id)` is `https://api.nuget.org/v3/registration5-gz-semver2/<id>/index.json`, `nupkg(id, version)` is `https://api.nuget.org/v3-flatcontainer/<id>/<version>/<id>.<version>.nupkg` (unzipped and listed, never executed), `gh:` means `https://api.github.com/<path>`, `search` is `https://azuresearch-usnc.nuget.org/query?q=toml&take=30&prerelease=false&semVerLevel=2.0.0&sortBy=totalDownloads`.

| ID | Source |
|---|---|
| S1 | FSF license list https://www.gnu.org/licenses/license-list.html (entries FreeBSD and Expat) |
| S2 | `search` (screening, download counts, deprecation field) |
| S3 | `nuspec`/`nupkg` Tomlyn 2.10.1 (license, TFMs, dependencies, `analyzers/`, `readme.md` with the TOML 1.1.0-only statement) |
| S4 | `catalog(tomlyn)`; `gh:repos/xoofx/Tomlyn`, `/commits?since=2026-04-14`, `/releases`, `/contributors` |
| S5 | `gh:repos/xoofx/Tomlyn/contents/src/Tomlyn/Tomlyn.csproj?ref=6cc82dd` (IsAotCompatible, IsTrimmable, TFMs), `.../license.txt?ref=6cc82dd` |
| S6 | `nuspec`/`nupkg` Samboy063.Tomlet 6.2.0; `catalog(samboy063.tomlet)`; `gh:repos/SamboyCoding/Tomlet` (+ commits, tags, contributors) |
| S7 | `gh:repos/SamboyCoding/Tomlet/contents/Tomlet/Tomlet.csproj?ref=7c5f4c7` (IsTrimmable, TFMs, description "compliant with version 1.0.0"), `.../LICENSE`, README (`gh:repos/SamboyCoding/Tomlet/readme`) |
| S8 | `nuspec`/`nupkg` CsToml 1.8.6 (license, TFMs, dependency `System.IO.Hashing`, `README.md` with TOML 1.0 default/1.1 optional and AOT statements); `catalog(cstoml)`; `gh:repos/prozolic/CsToml` (+ commits, releases, contributors) |
| S9 | `gh:repos/prozolic/CsToml/contents/src/CsToml/CsToml.csproj?ref=6e3b51a` (IsAotCompatible), `.../LICENSE` |
| S10 | `nuspec`/`nupkg` Tommy 3.1.2; `catalog(tommy)`; `gh:repos/dezhidki/Tommy` (+ commits, releases, contributors); `.../contents/Tommy/Tommy.csproj`, `.../LICENSE`, README (TOML 1.0.0 claim) |
| S11 | `nuspec`/`nupkg` Nett 0.15.0; `catalog(nett)`; `gh:repos/paiden/Nett` (`archived: true`); README (no TOML version claim) |
| S12 | `nuspec` of Alexinea.Extensions.Configuration.Toml 7.0.0, Carbon.Toml 0.36.0, Logary.TOML 3.6.1, Tomlyn.Extensions.Configuration 2.0.1, Tomlyn.Signed 2.10.1, PicoToml 2026.10.0 |
| S13 | Reflection listing of the public API and `RequiresUnreferencedCode`/`RequiresDynamicCode` attributes of Tomlyn, Tomlet, CsToml and Tommy (local scratch tool, assemblies loaded for inspection only) |

## M0 close: prototype removed (2026-10-17)

M0 is closed (see the roadmap). `experiments/toml-parser/`, `.github/workflows/spike-toml.yml` were deleted from the repository. The last commit that contains them is `f6089e6a490d8ada02cca8db79ab034f671bbad1` (`git show f6089e6:<path>` or `git checkout f6089e6`). The measurements and the decision in this ADR stand; the commands under "Reproduce" and the paths named above refer to that commit.
