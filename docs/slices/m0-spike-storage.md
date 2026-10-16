# Slice m0-spike-storage: AOT Spike Part 1, SQLite + FTS5, sqlite-vec, Brute-Force Fallback

| | |
|---|---|
| **Slice ID** | `m0-spike-storage` |
| **Branch** | `feat/m0-spike-storage` |
| **Milestone** | M0 – Setup & Spikes |
| **Roadmap** | "AOT spike" work package, storage part only: Microsoft.Data.Sqlite + FTS5 and sqlite-vec (roadmap line 56); exit criterion "The AOT spike runs on all four platforms, or every incompatible library has a documented fallback (... brute-force cosine instead of sqlite-vec)" (line 64), storage part; "Cold start measurement" (line 58), storage delta only |
| **PRD references** | G3 (search < 100 ms at 10,000 works), G7 (single-file binary), 9.1 (cold start < 150 ms, search p95 < 100 ms, semantic p95 work < 300 ms / chunk < 800 ms), 9.2 (four RIDs, Native AOT, ReadyToRun fallback), 9.3 (WAL, CLI/web/MCP in parallel), 10.3 (schema), F-SRC-1/F-SRC-2 (FTS5, BM25 with field weights; enabling only), F-SEM-4 (sqlite-vec; enabling only), risk table 14 ("sqlite-vec as a native dependency") |
| **Inputs** | ADR 0001 rows 8, 9, 10 and sources S50-S74 (research method, license policy), ADR 0002 (gate format, timing method), ADR 0003 and `docs/slices/m0-toml-parser.md` (spike/ADR structure), `experiments/toml-parser/` (isolation pattern, `measure.sh`), `tests/Shelf.TestData` (synthetic library generator, vocabulary files) |

---

## 1. Scope

**In scope**

1. Research (ADR 0001 method) of the facts listed in 3.2: sqlite-vec release assets per platform, license files, entry point, build flags, macOS signing/quarantine, Linux glibc floor; e_sqlite3 compile options at runtime; Microsoft.Data.Sqlite `LoadExtension` behavior at the 10.0.12 tag; Native AOT placement of native libraries.
2. Reproducible AOT experiment in `experiments/storage/` (not in `shelf.slnx`, not built by the existing CI jobs): (a) SQLite + FTS5 functional checks, WAL concurrency, FTS5 latency on the synthetic 10,000-work library; (b) sqlite-vec as a loadable extension, KNN on 384/1024-dim vectors; (c) brute-force cosine in C#; cold start, binary/output size.
3. A portable check script and a **separate, non-required** workflow `.github/workflows/spike-storage.yml` that runs the functional checks on all four RIDs (decision 3.7, Q8).
4. `docs/adr/0004-storage-sqlite-fts5-vector-search.md`: criteria fixed before measuring, research, measurements, decision (sqlite-vec vs. fallback, with a size threshold), consequences and hand-offs.
5. Roadmap: note the storage part of the AOT spike (link ADR 0004) in M0.

**Requirements covered:** none implemented. The slice produces a decision (ADR) and evidence for M0's exit criterion. PRD IDs above are enabled, not tagged (no feature files, section 2).

**Out of scope**

- Any product code: no `src/Shelf.Index`, no `src/Shelf.Semantic`, no change to `src/` or `tests/` (no pure helper is justified; every candidate helper, e.g. vector BLOB encoding or bm25 weight mapping, depends on decisions this ADR makes and belongs to the M1/M3 slice that consumes it).
- Schema design and migrations (M1 `Shelf.Index` slice). The experiment uses the PRD 10.3 fragments as-is.
- Query language (F-SRC-3), title normalization (F-GRP-2), stemming (F-SRC-6), fuzzy/trigram search (F-SRC-4): only the availability of the trigram tokenizer is recorded.
- ONNX Runtime / query embedding time, the `osx-x64` ONNX decision (PRD 13.2 Q8), and every other AOT spike library (VersOne.Epub, PdfPig, MCP SDK, ASP.NET Core + RazorSlices, CsToml four-platform check). They belong to the other spike parts and the aggregation slice.
- SQLitePCLRaw 3.x and SQLite builds other than the one Microsoft.Data.Sqlite 10.0.12 pins (Q9).
- Code signing/notarization of shelf releases, Homebrew packaging (M5). Only the behavior relevant to the decision is recorded.
- Changes to `.github/workflows/build.yml`, `Directory.Build.props`, `Directory.Packages.props`, `global.json`, `shelf.slnx`, ADR 0001-0003.

## 2. Feature Files

**None in this slice.** How the BDD rules in `CLAUDE.md` apply to a spike:

- Rules 1-5 bind product behavior. This slice adds no product behavior and no code under `src/`; its deliverable is a decision (ADR 0004) backed by an experiment, like ADR 0002 and the research part of ADR 0003. Writing feature files now would specify `Shelf.Index` behavior before its design exists and would have no step implementation in this slice (red scenarios on `main` violate rule 6).
- The experiment's functional checks (3.4) play the role of concrete examples: every check is a row with fixed input and expected output, executed on the AOT binary. Rows whose expected value is a fact to observe (not a requirement) are marked **observation** and are recorded, not gated.
- Rule 6 still applies: `dotnet test -c Release` on the solution stays green (nothing in the solution changes).
- **Hand-off to M1** (search slice; not written here): the observed tokenizer behavior (rows F10-F19 in 3.4) becomes `Examples` rows of the M1 search feature tagged `@F-SRC-1 @M1` / `@F-SRC-2 @M1`; the WAL result becomes a scenario tagged `@PRD-9.3 @M1`; vector KNN behavior goes to the M3 semantic slice (`@F-SEM-4 @M3`).

## 3. Design

### 3.1 Files

```
experiments/storage/                              NEW  not in shelf.slnx
  Directory.Build.props                           NEW  copy of experiments/toml-parser (no root import, TreatWarningsAsErrors=false,
                                                       PublishAot=true, IsAotCompatible=true, InvariantGlobalization not set)
  Directory.Packages.props                        NEW  Microsoft.Data.Sqlite 10.0.12, SQLitePCLRaw.bundle_e_sqlite3 2.1.12 (explicit pin,
                                                       same as transitive), System.Numerics.Tensors <10.0.x, R11>
  .gitignore                                      NEW  bin/ obj/ out/ data/ vendor/ results/* !results/measure.out !results/ci/
  README.md                                       NEW  how to run fetch, measure, check; data sizes; nothing else
  fetch-sqlite-vec.sh                             NEW  downloads the v0.1.9 loadable assets (and license files) for one or all
                                                       RIDs into vendor/<rid>/, verifies pinned SHA-256, never installs anything
  sqlite-vec.sha256                               NEW  pinned asset checksums (from R1)
  measure.sh                                      NEW  full run, osx-arm64 (3.6)
  check.sh                                        NEW  portable functional run, any of the four RIDs (3.7)
  queries/fts-fixed.txt                           NEW  hand-written queries for F-checks and latency (3.4)
  Shared/Harness.cs                               NEW  timing, percentiles, xoshiro256** PRNG, vector generation, key=value output
  Baseline/  Baseline.csproj, Program.cs          NEW  proto-storage-baseline: prints "ok", no SQLite reference
  Storage/   Storage.csproj, Program.cs,          NEW  proto-storage: all commands of 3.5
             Fts.cs, Wal.cs, Vectors.cs, Vec.cs,
             GroundTruthJson.cs (source-generated STJ context, works[] subset only)
  StaticLink/ (optional, time-boxed, 3.8)         NEW  proto-storage-static
.github/workflows/spike-storage.yml               NEW  non-required four-RID check (3.7)
docs/adr/0004-storage-sqlite-fts5-vector-search.md NEW
docs/roadmap.md                                   CHANGE  M0 AOT spike row: storage part done, link ADR 0004
```

Assembly names: `proto-storage-baseline`, `proto-storage`, `proto-storage-static`. Data (`data/`: DBs, flat vector files, ~1-3 GB) and downloaded binaries (`vendor/`) are git-ignored and never committed. The synthetic library is generated into the repository's git-ignored `artifacts/` by the existing generator; only its `ground-truth.json` is read.

### 3.2 Research (facts to verify; nothing below is verified)

Same method as ADR 0001 (read-only metadata, release API, repo files at the release tag, `.nupkg`/archive listings) with one deliberate exception (Q7): the sqlite-vec loadable binaries are **executed** (loaded into the prototype), because that is the point of the spike. Every downloaded archive goes into its own new empty directory under `vendor/<rid>/`, is verified against `sqlite-vec.sha256`, and is only loaded by the prototype; no downloaded script is run. ADR 0004 gets its own source index S1... (as ADR 0003 does); ADR 0001 sources are cited as `ADR 0001 S55`.

| ID | Question | Where to look / how | Record in ADR |
|---|---|---|---|
| R1 | sqlite-vec v0.1.9 release assets per platform | `gh:repos/asg017/sqlite-vec/releases/tags/v0.1.9` (asset names, sizes, `digest` field if present); list each `-loadable-<os>-<arch>.tar.gz` and `-static-...` | Table: RID → asset name, archive size, contained files (`vec0.dylib`/`vec0.so`), file size, SHA-256 (computed after download) |
| R2 | License files and the "either license" question (open in ADR 0001 row 10) | Contents of each tarball (does it contain `LICENSE-*`?); repo README "License" section, `Cargo.toml`/`package.json`/`pyproject` license fields at the v0.1.9 tag | Exact license statement with source; what a shelf release must ship (notices) |
| R3 | Extension entry point | `sqlite-vec.c` at v0.1.9 (`SQLITE_EXTENSION_INIT1`, exported `sqlite3_vec_init`, other `*_init` symbols); `nm -gU vec0.dylib` / `nm -D vec0.so`; SQLite docs on default entry point derivation from the file name (`vec0` → `sqlite3_vec_init`) | Entry point name; whether `proc: null` works (check V4) |
| R4 | How release binaries are built | Release workflow and `Makefile` at v0.1.9: compiler flags, `SQLITE_CORE` on/off for the static libs, SIMD flags (NEON/AVX) and runtime dispatch | Whether the static asset is usable for 3.8; CPU feature requirements per RID (e.g. AVX on x64) |
| R5 | macOS signing and quarantine | `codesign -dv --verbose=4` on both dylibs and on the AOT `proto-storage` binary; `xattr -l` after `curl` download; Apple docs: Gatekeeper and `com.apple.quarantine` for `dlopen`, library validation under hardened runtime, `com.apple.security.cs.disable-library-validation`; arm64 requirement that all code is signed (ad-hoc suffices) | Signature state per file; what blocks loading (check V9); consequence for a Homebrew/tarball release (Q10) |
| R6 | Linux binary compatibility | `objdump -T vec0.so` highest `GLIBC_x.y`; `ldd`/`readelf -d` NEEDED entries (must not need `libsqlite3.so`, ADR 0001 S66 segfault risk) | Minimum glibc per Linux RID; dependency list |
| R7 | e_sqlite3 2.1.12 build options (ADR 0001 only had `strings`) | Runtime: `SELECT sqlite_version()`, `PRAGMA compile_options` on the AOT binary on all four RIDs; `nm` for `sqlite3_load_extension`, `sqlite3_enable_load_extension`, `sqlite3_auto_extension` exports | Full option list (appendix), `OMIT_LOAD_EXTENSION` absent?, `ENABLE_FTS5`, `THREADSAFE` value, `DEFAULT_WAL_SYNCHRONOUS` |
| R8 | `SqliteConnection.LoadExtension` at the 10.0.12 tag | `dotnet/efcore` source at tag `v10.0.12`: `SqliteConnection.LoadExtension(string file, string? proc)`, whether it calls `sqlite3_enable_load_extension` itself, how a bare file name is resolved (probing of `NATIVE_DLL_SEARCH_DIRECTORIES` from `AppContext`?), behavior when that AppContext key is absent (Native AOT); `EnableExtensions(bool)`; dotnet docs page (ADR 0001 S55) | Resolution algorithm; which path variants of V3 are expected to work and why |
| R9 | AOT claims at the release tags | `Microsoft.Data.Sqlite.Core.csproj` at `v10.0.12` (ADR 0001 checked `main` only); SQLitePCLRaw 2.1.12 provider init path (`Batteries_V2.Init`, any reflection, `DynamicDependency`) | Claim per package; explanation for any warning in G-A1 |
| R10 | Native AOT and native libraries | .NET docs on Native AOT: where `runtimes/<rid>/native/*` land on publish (next to the binary), that `IncludeNativeLibrariesForSelfExtract` does not apply to AOT, static linking via `<NativeLibrary>` + `<DirectPInvoke>`; whether SQLitePCLRaw 2.1.12 offers a static e_sqlite3 for these RIDs | Options for G7 (3.8, Q2) |
| R11 | `System.Numerics.Tensors` | nuspec of the version matching .NET 10.0.12 (license, TFMs), `IsAotCompatible` in the csproj at the tag; whether `TensorPrimitives` is in-box for net10.0 (expected: no, separate package) | Version, license, AOT claim; new dependency for ADR 0001's transitive list |
| R12 | sqlite-vec v0.1.9 features | README/docs at v0.1.9: `vec0` syntax, `distance_metric=cosine`, `k = ?` vs `LIMIT`, max dimensions, whether any ANN index exists in 0.1.x (expected: no, exact scan), metadata/partition columns, `vec_version()` | Feature list relevant to F-SEM-4/F-SEM-6 |
| R13 | Third-party packaging (`HiraokaHyperTools.sqlite-vec` 0.1.9, ADR 0001 S74) | Unzip the `.nupkg`, compare SHA-256 of each native file with R1 | Identical or not; informational, no recommendation unless Q11 decides |

### 3.3 Decision criteria (write into ADR 0004 with status `Proposed` and commit before measuring)

**Gates, part A: SQLite + FTS5** (fail = escalate; there is no alternative database in the PRD, so a failed A gate stops the slice and goes to the owner)

| ID | Gate |
|---|---|
| A1 | AOT publish of `proto-storage` (`-p:TrimmerSingleWarn=false`) with zero `IL*`/AOT warnings, no `NoWarn`, no `SuppressTrimAnalysisWarnings`. |
| A2 | All gated F-checks (3.4) pass on the AOT binary on `osx-arm64`, and in `check.sh` on all four RIDs. |
| A3 | WAL concurrency (3.4, W-runs): zero failed operations in W1 and W2; reader p95 in W1 < 100 ms. |
| A4 | Metadata FTS (`works_fts`, 10,000 works): in-process query p95 < 100 ms over the full query set (G3). |
| A5 | Cold start: process `search <db> <query>` (open, WAL pragmas, one FTS query, print) p95 < 150 ms on `osx-arm64` (9.1). |

**Target, not gate:** content FTS (`chunks_fts`, 100,000 chunks) p95 < 100 ms. A miss is recorded as an M1 risk with the queries that miss, not a rejection.

**Gates, part B: sqlite-vec** (fail = sqlite-vec out on that RID; fallback rule applies)

| ID | Gate |
|---|---|
| B1 | `LoadExtension` of the official v0.1.9 binary succeeds in the AOT binary on `osx-arm64`, and in `check.sh` on all four RIDs, with the library placed next to the binary (variant V3b). |
| B2 | All V-checks (3.4) pass; KNN top-10 equals the exact brute-force top-10 (same IDs, same order, distance difference < 1e-5) for 100 queries per table. |
| B3 | A1 still holds with the extension code path included (no new warnings). |
| B4 | Latency within the vector budget (Q1): work level 10,000 × 384 p95 ≤ 100 ms; chunk level 100,000 × 384 p95 ≤ 400 ms; both measured per process (open + load extension + query) on `osx-arm64`. |
| B5 | License acceptable per ADR 0001 policy (R2). |

**Gates, part C: brute-force fallback**

| ID | Gate |
|---|---|
| C1 | Zero AOT warnings (covered by A1, same binary). |
| C2 | Exact results: top-10 equals a scalar double-precision reference implementation for 100 queries. |
| C3 | Same budget as B4, measured per process for mode `bf-sql` (vectors read from SQLite each process). |

**Decision rule** (proposal, Q1/Q3):

1. A1-A5 pass → Microsoft.Data.Sqlite 10.0.12 + bundle_e_sqlite3 2.1.12 confirmed for M1 with the recorded pragmas and tokenizer.
2. B1-B5 pass on all four RIDs → sqlite-vec v0.1.9 adopted for M3 (PRD F-SEM-4); brute force stays documented, not implemented.
3. B fails on any RID → brute force on **all** RIDs (one code path), if C passes; ADR states the PRD F-SEM-4 deviation for the owner (Q3).
4. **Fallback threshold:** from the C measurements, the largest vector count per dimension (384, 1024) for which `bf-sql` p95 stays within the budget is recorded as "fallback acceptable up to N". Interpolate linearly between measured sizes; never extrapolate beyond 2x the largest measured size.
5. B and C both fail the chunk budget → record the gap, escalate (options: int8/bit quantization in sqlite-vec, smaller chunk count, in-memory cache in `serve`/`mcp`); do not decide alone.

### 3.4 Checks and runs

All checks run in a fresh temp DB (`Path.GetTempPath()/shelf-spike/<guid>.db`), created by `fts-check` / `vec-check`. Output per row: `pass <id>` / `fail <id>: <detail>` / `observed <id>: <value>`; summary `passed X/Y`. Exit 1 if any gated row fails.

**FTS5 table under test** (PRD 10.3 columns):

```sql
CREATE VIRTUAL TABLE works_fts USING fts5(title, subtitle, authors, description, tags,
  tokenize = 'unicode61 remove_diacritics 2');
-- ranking: ORDER BY bm25(works_fts, 10.0, 5.0, 8.0, 1.0, 3.0)  (weights are spike values, M1 calibrates)
```

| ID | Kind | Setup (rows inserted) | Query / action | Expected |
|---|---|---|---|---|
| F1 | gate | – | `SELECT sqlite_version()` | `3.53.3` (ADR 0001 S62; if different, record and continue) |
| F2 | gate | – | `PRAGMA compile_options` | contains `ENABLE_FTS5`; no `OMIT_LOAD_EXTENSION` |
| F3 | gate | – | create `works_fts` as above | succeeds |
| F4 | gate | title `Kubernetes Patterns` (w1); description `... deploying on kubernetes ...` (w2) | `works_fts MATCH 'kubernetes'` ordered by weighted bm25 | `w1, w2` |
| F5 | gate | title `Event Sourcing in Practice` (w1); title `Sourcing Events Daily` (w2) | `MATCH '"event sourcing"'` | `w1` only |
| F6 | gate | title `Kubernetes Patterns` | `MATCH 'kube*'` | 1 hit |
| F7 | gate | w1 title `Kubernetes Patterns`; w2 authors `Kubernetes Team`, title `Other` | `MATCH 'title : kubernetes'` | `w1` only |
| F8 | gate | w1 title `Cloud Patterns`, tags `cloud/azure`; w2 title `Cloud Native`, tags `cloud/aws` | `MATCH 'cloud NOT azure'` | `w2` only |
| F9 | gate | title `Designing Data-Intensive Applications` | `snippet(works_fts, 0, '[', ']', '…', 8)` for `MATCH 'data'` | contains `[Data]` |
| F10 | gate | authors `Thomas Müller` | `MATCH 'muller'` | 1 hit |
| F11 | gate | authors `Thomas Müller` | `MATCH 'MÜLLER'` | 1 hit |
| F12 | gate | authors `Jördis Ålander` | `MATCH 'alander'` and `MATCH 'jordis'` | 1 hit each |
| F13 | gate | title `Café Société` | `MATCH 'cafe societe'` | 1 hit |
| F14 | gate | same table created with `remove_diacritics 0`, authors `Thomas Müller` | `MATCH 'muller'` | 0 hits (proves the option is effective) |
| F15 | observation | authors `Thomas Müller` | `MATCH 'mueller'` | expected 0 hits (no umlaut expansion) |
| F16 | observation | title `Größenordnungen verteilter Systeme` | `MATCH 'grossenordnungen'`, `MATCH 'großenordnungen'`, `MATCH 'grössenordnungen'` | expected 0 / 1 / 0 (ß is not folded to ss) |
| F17 | observation | title `Die Straße` | `MATCH 'strasse'`, `MATCH 'STRASSE'`, `MATCH 'straße'` | expected 0 / 0 / 1 |
| F18 | observation | tags `architektur/ddd` | `MATCH 'architektur'`, `MATCH 'ddd'`, `MATCH '"architektur ddd"'` | record (expected 1/1/1: `/` separates tokens); input for F-TAG search in M1 |
| F19 | observation | – | `CREATE VIRTUAL TABLE t USING fts5(x, tokenize='trigram')`, insert `Kubernetes`, `MATCH 'ubernet'` | record availability (F-SRC-4 input) |
| F20 | gate | – | `PRAGMA journal_mode=WAL` on a file DB, reopen | returns `wal`; persists after reopen |

**WAL runs** (processes, not threads; each process one connection, `Default Timeout=5`, `PRAGMA journal_mode=WAL; PRAGMA synchronous=NORMAL; PRAGMA busy_timeout=5000`), on a copy of the 10k-work DB, 30 s each:

| ID | Processes | Writer loop | Reader loop | Record |
|---|---|---|---|---|
| W1 | 1 writer + 4 readers | `BEGIN IMMEDIATE`; update 50 random `works` rows and their `works_fts` rows; `COMMIT`; sleep 10 ms | random query from the query set, read all hits | ops and failed ops per process, reader latency p50/p95/max, max WAL file size, checkpoint count |
| W2 | 2 writers + 4 readers | as W1, both writers | as W1 | as W1, plus writer wait time p95 |
| W3 (observation) | 1 writer holding a transaction open 6 s + 1 writer | – | – | does the second writer fail after 5 s with `SQLITE_BUSY` (expected) and how does Microsoft.Data.Sqlite surface it (exception type, `SqliteErrorCode`) |

**Vector checks** (`vec-check --ext <path>`; vectors: seeded xoshiro256**, Gaussian via Box-Muller, L2-normalized float32; BLOB = little-endian float32 bytes):

| ID | Kind | Action | Expected |
|---|---|---|---|
| V1 | gate | `LoadExtension(<abs path>/vec0.<ext>)`, then `SELECT vec_version()` | `v0.1.9` |
| V2 | gate | `CREATE VIRTUAL TABLE vec_works USING vec0(work_id INTEGER PRIMARY KEY, embedding float[384] distance_metric=cosine)`; insert 1,000 vectors; KNN `WHERE embedding MATCH ? AND k = 10 ORDER BY distance` | equals brute-force top-10 for 20 queries (IDs, order, distance diff < 1e-5) |
| V3a | observation | `LoadExtension` with an absolute path | record ok/error |
| V3b | gate (B1) | library copied next to the binary, path `Path.Combine(AppContext.BaseDirectory, "vec0")` (no suffix; SQLite appends `.dylib`/`.so`) | ok |
| V3c | observation | bare name `"vec0"` with the library next to the binary and the working directory elsewhere | record (depends on R8 probing) |
| V3d | observation | bare name with `DYLD_LIBRARY_PATH`/`LD_LIBRARY_PATH` pointing to `vendor/<rid>` | record |
| V4 | gate | `proc: null` vs `proc: "sqlite3_vec_init"` | both load |
| V5 | gate | `LoadExtension` of a missing file | `SqliteException` (record `SqliteErrorCode` and message), process continues: this is the detection path for a runtime fallback |
| V6 | gate | `float[1024]` table, same check as V2 | passes |
| V7 | observation | dimension mismatch on insert (383 floats into `float[384]`) | record error text |
| V8 | observation | `LoadExtension` without `EnableExtensions(true)` | record (R8) |
| V9 | observation, macOS only | add `com.apple.quarantine` to a copy of the dylib (`xattr -w com.apple.quarantine "0081;00000000;Safari;" vec0.dylib`), load | record ok/error; also `codesign -dv` output |

**Latency data** (`load` commands, 3.5):

| Dataset | Rows | Source |
|---|---|---|
| `works` + `works_fts` | 10,000 | `artifacts/synthetic-library/ground-truth.json` (`works[]`: title, authors, language, year, isbn13, series); `description` = 30-80 words and `tags` = 0-3 tags from a fixed list of 40 hierarchical tags, both seeded from `tests/Shelf.TestData/Vocabulary/{en,de}.txt` by the work key |
| `chunks` + `chunks_fts` | 100,000 (10 per work), optional 1,000,000 | 400-600 words per chunk from the same vocabularies, Zipf-weighted like the generator (integer weights `floor(10^9/rank)`) |
| `emb_works` (BLOB) / `vec_works` | 10,000 × 384 and × 1024 | seeded random unit vectors |
| `emb_chunks` / `vec_chunks` | 100,000 × 384, 100,000 × 1024; optional 1,000,000 × 384 | same |

Query sets: `queries/fts-fixed.txt` (hand-written: the F-check forms over real vocabulary words: 3 prefixes, 3 phrases, 3 `title :`/`authors :` filters, 3 umlaut words) plus a generated set written by `load` to `data/<db>.queries.txt`: 10 terms each from three document-frequency buckets (df 1-10, 100-300, ≥ 2,000) and 10 two-term ANDs. FTS queries use `ORDER BY bm25(...) LIMIT 20`. Vector queries: 100 seeded query vectors, `k = 10`.

### 3.5 Prototype commands (`proto-storage`)

Writes only to the `TextWriter`s it gets in `Main`; numbers with `InvariantCulture`; one result line `key=value ...` per measurement for `measure.sh` to grep.

| Command | Purpose |
|---|---|
| `info` | `sqlite_version`, compile options (one per line), runtime RID, `RuntimeInformation.OSDescription` |
| `fts-check` | F1-F20 |
| `load <ground-truth.json> <db> --vocab <dir> [--chunks N] [--seed 42]` | Schema (PRD 10.3 `works`, `chunks`, `works_fts`, `chunks_fts`; `files`/`tags` omitted), insert in one transaction per 1,000 rows; prints row counts, elapsed, DB size; writes the generated query set |
| `search <db> <query> [--content] [--limit 20]` | One process-level search (A5): open, pragmas, query, print hits |
| `fts-bench <db> <queries-file> [--content]` | In-process: 10 warmup, then each query 20 times; p50/p95/max overall and per bucket (A4, target) |
| `wal-writer <db> --seconds 30 --batch 50 --id N` / `wal-reader <db> --seconds 30 --queries <file>` / `wal-hold <db> --seconds 6` | W1-W3 |
| `vec-gen <db> --table works\|chunks --count N --dim D --seed S [--flat <file>]` | Fills `emb_<table>` (BLOB) and optionally a flat file (`count × dim` float32, header with count/dim) |
| `vec-load <db> --ext <path> --table ... --dim D` | Copies `emb_<table>` into `vec_<table>` (vec0, cosine); prints insert time and DB size delta |
| `vec-check --ext <path>` | V1-V9 (V9 driven by `measure.sh`) |
| `knn <db> --mode vec\|bf-sql\|bf-mmap --table ... --dim D --query-seed S [--ext <path>] [--threads 1\|all]` | One process-level KNN query (B4, C3); prints top-10 and phase timings (open, extension load, vector load, scan) |
| `knn-bench <db> --mode vec\|bf-mem\|bf-sql\|bf-mmap ... --queries 100` | In-process p50/p95/max; verifies results against `bf-ref` (C2, B2) |

Brute-force implementation (experiment only): vectors are unit length, so cosine = dot product. Scan with `TensorPrimitives.Dot` (R11) and, as a second variant, a hand-written `Vector128/Vector256` loop (records whether the package is worth a dependency). Top-k with a fixed-size min-heap. `--threads all` partitions with `Parallel.For` (record both). `bf-ref` is a scalar `double` loop used only for verification. Modes: `bf-mem` (vectors already in memory: compute only), `bf-sql` (read all BLOBs via `SqliteDataReader.GetStream`/`GetBytes` into one pooled `float[]`, then scan: the realistic CLI path), `bf-mmap` (`MemoryMappedFile` over the flat file: what a non-SQLite store would cost; informational, F-SEM-4 says vectors live in SQLite).

### 3.6 `measure.sh` (osx-arm64, full run)

bash 3.2, `set -euo pipefail`, structure of `experiments/toml-parser/measure.sh`; environment block as in ADR 0002/0003 (incl. power source; must run on AC power, Q1). Steps:

1. `fetch-sqlite-vec.sh osx-arm64` (skip if verified files exist).
2. Publish `Baseline` and `Storage` with `-c Release -r osx-arm64 -p:TrimmerSingleWarn=false`; warnings by code (A1, B3).
3. Output directory listing with sizes: binary, `libe_sqlite3.dylib`, `.dSYM`, anything else; copy `vec0.dylib` next to the binary. Size table: binary Baseline vs Storage (managed+AOT delta), plus each native side file (G7 input, Q2).
4. `info`, `fts-check`, `vec-check` on the AOT binary (A2, B1, B2, V3-V9 incl. the quarantine copy).
5. If `artifacts/synthetic-library/ground-truth.json` is missing: print the generator command (`dotnet run --project tests/Shelf.TestData -c Release -- generate --out artifacts/synthetic-library --preset reference`) and exit 1; never generate implicitly.
6. `load` (10k works, 100k chunks), `vec-gen` and `vec-load` for every dataset of 3.4; record load times and DB sizes.
7. `fts-bench` works and chunks (A4, target).
8. Process-level timing (hyperfine `-N` or the zsh `$EPOCHREALTIME` fallback; 5 warmups, 50 runs): `proto-storage-baseline` (no-op), `search` works (A5), `search --content`, `knn --mode vec` / `bf-sql` / `bf-mmap` for each dataset, `--threads 1` and `all` (B4, C3). Page cache warm (every run after warmup); a page-cache-cold run (`sudo purge`) only if available without new installs, informational.
9. `knn-bench` all modes (C2, B2 at full size).
10. W1, W2, W3 (A3).
11. Optional 1M datasets behind `--large` (Q4).
12. Summary block at the end: every gate with pass/fail and the measured value; fallback threshold table input.

`results/measure.out` is the unedited output of `./measure.sh > results/measure.out 2>&1` (committed; everything else in `results/` git-ignored).

### 3.7 Four-platform check and CI

**Decision: add a separate, non-required workflow now** (Q8). Reason: the riskiest facts (sqlite-vec loading on `linux-arm64`/`osx-x64`, glibc floor, e_sqlite3 options per RID) cannot be checked locally, and handing them to the aggregation slice would leave the B gates unanswered in ADR 0004.

`check.sh <rid>` (bash, portable: `uname`, `stat -c`/`stat -f` switch, no zsh, no timing gates):

1. `fetch-sqlite-vec.sh <rid>`; record `codesign -dv` (macOS) or `objdump -T | grep GLIBC_` max and `readelf -d` NEEDED (Linux).
2. Publish `Storage` AOT for `<rid>`; fail on any `IL*`/AOT warning.
3. Copy `vec0.*` next to the binary; run `info`, `fts-check`, `vec-check`.
4. Smoke data: `load` with a 200-work ground truth (`generate --works 200 --files 300` into `$RUNNER_TEMP`), `vec-gen` 10,000 × 384, `knn --mode vec` and `bf-sql` once each; print timings (informational, CI timing is not a gate).
5. Short W1 (10 s); fail on any failed operation.
6. Write `results/ci/<rid>.txt` (uploaded as artifact; the implementer copies the four files into the repo for the ADR, they are small text).

`.github/workflows/spike-storage.yml`: triggers `workflow_dispatch` and `pull_request` with `paths: [experiments/storage/**, .github/workflows/spike-storage.yml]`; `permissions: contents: read`; same four-RID matrix, runner labels and architecture assertion as `build.yml`; Linux installs `clang zlib1g-dev`; `actions/setup-dotnet@v6` 10.0.x; steps `check.sh ${{ matrix.rid }}`, upload `experiments/storage/results/ci/`. Not added to the `ci` aggregate job, so it never blocks merges. Removed (or replaced by the aggregation slice's combined spike job) when M0 closes.

### 3.8 Single-file variant (optional, time-box 1 day)

Native AOT puts `libe_sqlite3.dylib`/`.so` (and any loadable extension) **next to** the binary, so with SQLite the output is no longer one file (G7). To know the cost of a true single file, `StaticLink/` tries:

- Compile the SQLite amalgamation (the version from F1, public domain) with the compile options recorded in R7 plus `sqlite-vec.c` (v0.1.9 amalgamation release asset) into one static library per RID with `clang`, link with `<NativeLibrary Include="...a" />` and `<DirectPInvoke Include="e_sqlite3" />`, keep `SQLitePCLRaw.provider.e_sqlite3` (its `DllImport("e_sqlite3")` resolves statically).
- Register sqlite-vec with `sqlite3_auto_extension(sqlite3_vec_init)` through a `[DllImport("e_sqlite3")]` / function pointer, before the first connection.
- Run `fts-check` and `vec-check` (without V3/V9) and record: works or not, binary size, build steps per RID, whether CI runners can build it.

Result is informational and feeds Q2. If it does not work within the time-box, record where it stopped; no gate depends on it.

### 3.9 AOT and native-dependency risks

- **Native side files:** `libe_sqlite3.*` (about 1.5-1.7 MB per RID, ADR 0001 S62) and `vec0.*` are separate files. Breaks a literal reading of G7 (Q2).
- **Initialization under AOT:** Microsoft.Data.Sqlite locates and initializes the SQLitePCLRaw bundle; if any of that is reflection-based and unannotated, A1 shows nothing but A2 fails at runtime. Both are measured; R9 explains.
- **Extension lookup:** Microsoft.Data.Sqlite may rely on `NATIVE_DLL_SEARCH_DIRECTORIES`, which a Native AOT host may not populate (R8). V3b (path from `AppContext.BaseDirectory`) is the gated variant so the decision does not depend on probing.
- **macOS:** arm64 refuses unsigned code; quarantine and library validation may block `dlopen` (R5, V9). AOT binaries get an ad-hoc linker signature and no hardened runtime by default (to verify in R5).
- **Linux:** `vec0.so` built against a newer glibc than the user's system fails to load (R6). A globally loaded system `libsqlite3` next to `libe_sqlite3` can crash (ADR 0001 S66); `vec0.so` must not link `libsqlite3` (R6).
- **CPU features:** release binaries compiled with AVX/NEON paths without runtime dispatch would crash with `SIGILL` on older x64 CPUs (R4). Record; no hardware to test.
- **sqlite-vec pre-v1:** on-disk format and SQL may change (ADR 0001 row 10). Pin v0.1.9; F-LIB-6 (index is rebuildable) limits the damage.
- **New managed dependency:** `System.Numerics.Tensors` (R11) only if the hand-written loop is not good enough; record the size delta.
- **Big test data:** up to ~1 GB for the default datasets, ~3 GB with `--large`; all under `experiments/storage/data/` (git-ignored). `measure.sh` prints the total and deletes nothing outside `data/`.

## 4. Unit Tests

No product unit tests (no product code). The experiment carries self-checks that run inside `fts-check`/`vec-check`/`knn-bench` and fail the run if broken:

| Unit | Self-check |
|---|---|
| Percentile/median | Fixed arrays (`[1..10]` → median 5.5, p95 10; `[3,1,2]` → median 2), same nearest-rank rule as `measure.sh` |
| BLOB encoding | `float[] {1f, -2.5f}` → bytes `00 00 80 3F 00 00 20 C0`; round trip; `BitConverter.IsLittleEndian` asserted |
| Vector generation | Same seed → identical first vector bytes; every vector has `|‖v‖ − 1| < 1e-5` |
| Brute force | `TensorPrimitives` / SIMD loop / `bf-ref` agree on 1,000 random pairs (abs diff < 1e-5); heap top-k equals full sort top-k |
| Culture | `fts-check` and `knn` output identical under `LANG=de_DE.UTF-8 LC_ALL=de_DE.UTF-8` (`measure.sh` step 4 runs both) |

Hand-off to M1/M3 unit tests (listed in ADR 0004 Consequences, not written here): vector BLOB codec, bm25 weight mapping, extension path resolution, WAL/pragma setup on connection open.

## 5. Step Order

1. **Criteria first.** Create `docs/adr/0004-storage-sqlite-fts5-vector-search.md` (status `Proposed`): Context (PRD G3, G7, 9.1, 9.2, 9.3, F-SRC-1/2, F-SEM-4, risk table, ADR 0001 rows 8-10), options (A: sqlite-vec v0.1.9 loadable; B: brute force in C#; C: sqlite-vec statically linked, informational), criteria 3.3, method 3.4-3.7. Get Q1 confirmed. Commit before any measurement.
2. **Research.** R1-R13 (3.2), ADR source index. Download and pin checksums into `sqlite-vec.sha256`. If B5 fails on paper (license), stop and ask. Commit.
3. **Scaffold.** `experiments/storage/` props, `.gitignore`, README, `Shared/Harness.cs`, `Baseline`. Confirm `dotnet build -c Release` at the root does not build anything under `experiments/`.
4. **Fetch script.** `fetch-sqlite-vec.sh` (one RID or all; own empty directory per archive; checksum verify; prints `vendor/<rid>/vec0.*`).
5. **FTS part.** `Storage`: `info`, `fts-check`. Publish AOT, check warnings (A1). Record F1-F20.
6. **Data and FTS latency.** Generate the reference library if absent (generator command in 3.6); `load`, `search`, `fts-bench`.
7. **WAL.** `wal-writer`, `wal-reader`, `wal-hold`; W1-W3 by hand once.
8. **Vectors and brute force.** `vec-gen`, `knn`/`knn-bench` modes `bf-*` with self-checks.
9. **sqlite-vec.** `vec-load`, `vec-check`, `knn --mode vec`; V3-V9 including the quarantine copy.
10. **`measure.sh`** (3.6); full run on AC power, plus the `de_DE` run. Commit `results/measure.out`.
11. **`check.sh` and workflow** (3.7). Push; run on all four RIDs; copy `results/ci/<rid>.txt` into the repo.
12. **Optional** `StaticLink/` (3.8), time-boxed.
13. **Decide.** Fill ADR: environment, commands, research tables, results (warnings, check rows, latency tables per dataset and mode, phase breakdown, sizes, cold start delta vs Baseline, WAL results, four-RID table), gate table, decision per rule 3.3 incl. fallback threshold, distribution effort per platform (asset, size, signature, glibc, how shipped), consequences (pins: Microsoft.Data.Sqlite 10.0.12, bundle 2.1.12, sqlite-vec v0.1.9; pragmas; tokenizer; hand-offs to M1 `Shelf.Index` and M3 `Shelf.Semantic`; notices file items; re-check triggers: sqlite-vec 0.2/1.0, SQLitePCLRaw 3.x, Microsoft.Data.Sqlite 11; delete `experiments/storage/` and `spike-storage.yml` when M0 closes). Status `Accepted`, or stop and ask if rule 3.3 step 5 applies or an A gate fails.
14. **Roadmap.** M0 "AOT spike" row: append "Storage part (Microsoft.Data.Sqlite + FTS5, sqlite-vec / brute-force fallback): **Done:** [ADR 0004](adr/0004-storage-sqlite-fts5-vector-search.md)"; leave the row's other libraries open.
15. **Quality gates.** `dotnet format --verify-no-changes`, `dotnet build -c Release`, `dotnet test --no-build -c Release` all green (solution unchanged); `git status` shows nothing under `experiments/storage/data|vendor|out`.
16. Push, open the PR (gate table and four-RID results in the description); `ci` green; `spike-storage` green on four RIDs.

## 6. Open Questions

- **Q1: Gates and budgets** (3.3) are proposals. The PRD gives end-to-end targets only (work level < 300 ms and chunk level < 800 ms **including** query embedding, whose cost the ONNX spike part measures). Proposal: vector search budget work ≤ 100 ms, chunk ≤ 400 ms per process, leaving the rest for embedding and output. Also: A4 measured in-process, A5 per process; measurements on AC power. Confirm before step 10.
- **Q2: Single-file binary (G7).** With SQLite the AOT output is binary + `libe_sqlite3.*` (+ `vec0.*`, later ONNX Runtime with ~45 MB). Is "one archive per platform, binary plus native libraries in the same directory" acceptable? Recommendation: yes for M1-M4; the release slice (M5) revisits static linking with the 3.8 result. If a literal single file is required, the cost is a C toolchain in the build and self-built SQLite/sqlite-vec per RID.
- **Q3: If sqlite-vec has no advantage.** sqlite-vec 0.1.x is expected to be an exact scan too (R12), so brute force may be equally fast without a native dependency. Recommendation: rule 3.3 as written (sqlite-vec if it passes everywhere, since F-SEM-4 is Must); if the owner prefers fewer native dependencies when both pass, brute force on all RIDs plus a PRD F-SEM-4 change. Owner decision.
- **Q4: Chunk count for the gate.** Synthetic texts give ~10 chunks per work (100,000); real technical books (100,000+ words) give 150-250 chunks per work, i.e. 1.5-2.5 million chunks at 10,000 works. Recommendation: gate on 100,000 now, run 1,000,000 × 384 with `--large` as information, state the extrapolation in the ADR, and recalibrate in M3 with real chunking.
- **Q5: Dimensions.** 384 (multilingual-e5-small, PRD Q3) is gated; 1024 (bge-m3, deferred) is measured for information. Confirm.
- **Q6: German search behavior.** unicode61 with `remove_diacritics 2` is expected to match `muller` → `Müller` but not `mueller`, and not to fold `ß` to `ss` (F15-F17). Is that acceptable for M1 F-SRC-1, or does M1 need query expansion (ä→ae, ß→ss) or a custom tokenizer? Recommendation: accept for M1 and revisit with F-SRC-6 (M5); record the observed behavior.
- **Q7: Executing downloaded binaries.** ADR 0001's method forbids executing downloads; this spike must load the official sqlite-vec binaries. Mitigation: official GitHub release only, pinned SHA-256, own directories, no scripts from the archive run. Confirm.
- **Q8: CI.** Recommendation (3.7): a separate, non-required `spike-storage.yml` now, removed at M0 close. Alternative: no workflow; four-RID check handed to the aggregation slice, and ADR 0004 records B1/B2 for `osx-arm64` only (status then "Accepted for osx-arm64"). Confirm.
- **Q9: SQLitePCLRaw line.** Stay on 2.1.12 (what Microsoft.Data.Sqlite 10.0.12 pins; 3.x not evaluated, ADR 0001 row 9). Recommended. Re-check when Microsoft.Data.Sqlite moves to 3.x.
- **Q10: macOS signing and notarization of releases.** Not decided (M5). If shelf is ever shipped with hardened runtime, loading `vec0.dylib` needs the same team signature or `disable-library-validation`. The spike records only the current behavior (R5, V9).
- **Q11: How sqlite-vec reaches the build (M3).** Recommendation: a pinned download of the official release assets with checksum verification in the build (as `fetch-sqlite-vec.sh` does), not the third-party `HiraokaHyperTools.sqlite-vec` package; R13 is informational. Alternative: build from the amalgamation ourselves (needed anyway if Q2 requires static linking).
- **Q12: Linux baseline.** The PRD names `linux-x64`/`linux-arm64` without a minimum distribution or glibc. R6 records what sqlite-vec needs; the owner sets the floor (proposal: glibc of Ubuntu 22.04 or newer; musl out of scope).
- **Q13: Keep `experiments/storage/`** until M0 closes, then delete with an ADR note pointing to the last commit (as ADR 0002/0003). Recommended.
- **Q14: Source numbering.** ADR 0004 gets its own source index starting at S1 (as ADR 0003), citing ADR 0001 sources as "ADR 0001 S55". If continuous numbering after ADR 0001 (S139 onward) is preferred, say so before step 2.
