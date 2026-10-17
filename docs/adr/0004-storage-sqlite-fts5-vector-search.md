# ADR 0004: Storage, SQLite + FTS5 and vector search

| | |
|---|---|
| **Status** | Accepted for `osx-arm64` and `linux-x64`: A (SQLite + FTS5) confirmed and B (sqlite-vec v0.1.9) chosen on both; `osx-x64` and `linux-arm64` are no longer supported platforms (PRD 9.2), see Decision |
| **Date** | 2026-10-16 |
| **Scope** | PRD G3 (search < 100 ms at 10,000 works), G7 (single-file binary), 9.1 (cold start < 150 ms, search p95 < 100 ms, semantic p95 work < 300 ms / chunk < 800 ms), 9.2 (four RIDs, Native AOT), 9.3 (WAL, CLI/web/MCP in parallel), 10.3 (schema), F-SRC-1/F-SRC-2 (FTS5, BM25; enabling only), F-SEM-4 (sqlite-vec; enabling only), risk table 14 ("sqlite-vec as a native dependency") |
| **Roadmap** | M0 "AOT spike", storage part only (Microsoft.Data.Sqlite + FTS5, sqlite-vec); exit criterion "every incompatible library has a documented fallback (... brute-force cosine instead of sqlite-vec)" |
| **Inputs** | [ADR 0001](0001-dependency-licenses-and-maintenance.md) rows 8-10 and sources S50-S74, [ADR 0002](0002-cli-framework.md) (gate format, timing method), [ADR 0003](0003-toml-parser.md) (spike/ADR structure), slice plan `docs/slices/m0-spike-storage.md` |

This ADR was written (sections Context to Method) **before any measurement** was taken, but, as for ADR 0003, it is not committed separately before the measurements (the implementer does not commit); the order is documented here and not provable from the repository history. Research (facts from sources and from inspecting the downloaded files) was done before the criteria were written; no gate was evaluated at that time.

## Context

shelf stores metadata in SQLite with FTS5 (PRD 10.3, F-SRC-1/F-SRC-2) and chunk embeddings for semantic search in SQLite via sqlite-vec (F-SEM-4). CLI, web and MCP processes use the same database in WAL mode concurrently (9.3). The CLI is a Native AOT binary (9.2) with a cold start below 150 ms (9.1). ADR 0001 adopted `Microsoft.Data.Sqlite` 10.0.12 with `SQLitePCLRaw.bundle_e_sqlite3` 2.1.12 (SQLite 3.53.3 per static inspection) and listed sqlite-vec v0.1.9 as a pre-v1 native extension with an unresolved licensing detail ("either license") and no AOT claim. This ADR answers, for the storage part: does that stack work in a Native AOT binary on all four RIDs, is sqlite-vec loadable there and fast enough, and what is the fallback.

Owner decisions taken before the work (they fix scope and rules):

- Official sqlite-vec GitHub release assets may be downloaded (single requests, no crawling) with a pinned SHA-256 and are **executed (loaded)** by the prototype; they are stored in a git-ignored folder and never committed. This is a deliberate exception to ADR 0001's "nothing downloaded was executed".
- **G7:** an archive per platform with the binary and the native libraries next to it is accepted until M5; the cost is measured and documented. No static-link variant is evaluated.
- If sqlite-vec and brute force both pass the budgets, **sqlite-vec wins** (F-SEM-4 stays). If sqlite-vec fails on any platform: brute force on all platforms, with the PRD deviation recorded here.
- A separate, non-required workflow `.github/workflows/spike-storage.yml` runs `check.sh` on `osx-arm64` and `linux-x64` (the two supported RIDs). It is removed when M0 closes. It first ran on PR #15, where both jobs passed.
- Budget split 100 ms (work level) / 400 ms (chunk level) for the vector search part of the PRD's end-to-end targets; gates on 384 dimensions and 100,000 chunks; no umlaut expansion (ae/oe/ue, ss) in M1; SQLitePCLRaw stays on 2.1.12; macOS signing and notarization belong to M5; sqlite-vec reaches the M3 build through a pinned download with checksum.
- Source index of this ADR starts at S1; ADR 0001 sources are cited as "ADR 0001 Sxx".

## Options

- **A. sqlite-vec v0.1.9, official loadable extension** (`vec0.dylib`/`vec0.so`), loaded with `SqliteConnection.LoadExtension`, `vec0` virtual tables.
- **B. Brute-force cosine in C#**: vectors stored as BLOBs in a normal SQLite table (or in memory), scanned with `TensorPrimitives.Dot` (System.Numerics.Tensors) or a hand-written `Vector128/256` loop, top-k with a heap. No native extension.
- Not evaluated (owner decision): sqlite-vec statically linked (the official `static` assets target `SQLITE_CORE` builds and would need a self-built SQLite), other vector stores, SQLitePCLRaw 3.x.

## Criteria (fixed before measuring)

Part A is the SQLite + FTS5 baseline. A failed A gate stops the slice and goes to the owner (no alternative database in the PRD).

| ID | Gate |
|---|---|
| A1 | AOT publish of the `Storage` prototype (`osx-arm64`, `-p:TrimmerSingleWarn=false`) with zero `IL*`/AOT warnings, no `NoWarn`, no `SuppressTrimAnalysisWarnings`. |
| A2 | All gated F-checks (below) pass on the AOT binary on `osx-arm64`, and in `check.sh` on all four RIDs. |
| A3 | WAL concurrency: zero failed operations in W1 and W2; reader p95 in W1 < 100 ms. |
| A4 | Metadata FTS (`works_fts`, 10,000 works): in-process query p95 < 100 ms over the full query set (PRD G3). |
| A5 | Cold start: process `search <db> <query>` (open, WAL pragmas, one FTS query, print) p95 < 150 ms on `osx-arm64` (PRD 9.1). |

**Target, not gate:** content FTS (`chunks_fts`, 100,000 chunks) p95 < 100 ms. A miss is an M1 risk, not a rejection.

Part B: sqlite-vec (fail = sqlite-vec out on that RID; fallback rule applies).

| ID | Gate |
|---|---|
| B1 | `LoadExtension` of the official v0.1.9 binary succeeds in the AOT binary on `osx-arm64`, and in `check.sh` on all four RIDs, with the library placed next to the binary (variant V3b). |
| B2 | All V-checks pass; KNN top-10 equals the exact brute-force top-10 (same IDs, same order, distance difference < 1e-5) for 100 queries per table. |
| B3 | A1 still holds with the extension code path included. |
| B4 | Latency within budget: work level 10,000 x 384 p95 <= 100 ms; chunk level 100,000 x 384 p95 <= 400 ms; both per process (open + load extension + query) on `osx-arm64`. |
| B5 | License acceptable per ADR 0001 policy. |

Part C: brute-force fallback.

| ID | Gate |
|---|---|
| C1 | Zero AOT warnings (same binary as A1). |
| C2 | Exact results: top-10 equals a scalar double-precision reference for 100 queries. |
| C3 | Same budget as B4, per process, for mode `bf-sql` (all vectors read from SQLite in each process). |

Clarifications fixed before the full run (the plan did not say): C3 and the brute-force part of the fallback threshold are gated on `--threads 1` (conservative at chunk level; `--threads all` is recorded); the dot-product variant gated is `TensorPrimitives.Dot` (the hand-written SIMD loop is recorded to decide whether the package is worth a dependency); both clarifications (`--threads 1` and `TensorPrimitives`) turned out to be the slower variants at the gated size (chunk level), but they were fixed after some data had been seen (earlier smoke runs on 10,000 x 384, before the full run), so they are not strictly pre-registered. B4/C3 use one query seed per process and 50 runs after 5 warmups (p95 = nearest rank over 50 runs, i.e. the 48th value).

**Decision rule** (confirmed by the owner):

1. A1-A5 pass: Microsoft.Data.Sqlite 10.0.12 + bundle_e_sqlite3 2.1.12 confirmed for M1.
2. B1-B5 pass on all four RIDs **and** C passes or fails: sqlite-vec v0.1.9 adopted for M3 (F-SEM-4 stays). Brute force stays documented, not implemented. (Owner decision: sqlite-vec wins when both pass.)
3. B fails on any RID: brute force on **all** RIDs (one code path) if C passes; this ADR records the F-SEM-4 deviation.
4. Fallback threshold: from the C measurements, the largest vector count per dimension (384, 1024) for which `bf-sql` p95 stays within the budget; interpolate linearly between measured sizes, never extrapolate beyond 2x the largest measured size.
5. B and C both fail the chunk budget: record the gap and escalate; do not decide alone.

### Functional checks (fresh temp DB each, executed on the AOT binary)

FTS table: `CREATE VIRTUAL TABLE works_fts USING fts5(title, subtitle, authors, description, tags, tokenize = 'unicode61 remove_diacritics 2')`; ranking `ORDER BY bm25(works_fts, 10.0, 5.0, 8.0, 1.0, 3.0)` (spike weights).

| ID | Kind | Setup | Query / action | Expected |
|---|---|---|---|---|
| F1 | gate | - | `SELECT sqlite_version()` | `3.53.3` (if different: record and continue) |
| F2 | gate | - | `PRAGMA compile_options` | contains `ENABLE_FTS5`; no `OMIT_LOAD_EXTENSION` |
| F3 | gate | - | create `works_fts` | succeeds |
| F4 | gate | w1 title `Kubernetes Patterns`; w2 description `... deploying on kubernetes ...` | `MATCH 'kubernetes'` by weighted bm25 | `w1, w2` |
| F5 | gate | w1 `Event Sourcing in Practice`; w2 `Sourcing Events Daily` | `MATCH '"event sourcing"'` | `w1` only |
| F6 | gate | `Kubernetes Patterns` | `MATCH 'kube*'` | 1 hit |
| F7 | gate | w1 title `Kubernetes Patterns`; w2 authors `Kubernetes Team`, title `Other` | `MATCH 'title : kubernetes'` | `w1` only |
| F8 | gate | w1 `Cloud Patterns` tags `cloud/azure`; w2 `Cloud Native` tags `cloud/aws` | `MATCH 'cloud NOT azure'` | `w2` only |
| F9 | gate | `Designing Data-Intensive Applications` | `snippet(works_fts, 0, '[', ']', '...', 8)` for `MATCH 'data'` | contains `[Data]` |
| F10 | gate | authors `Thomas Müller` | `MATCH 'muller'` | 1 hit |
| F11 | gate | authors `Thomas Müller` | `MATCH 'MÜLLER'` | 1 hit |
| F12 | gate | authors `Jördis Ålander` | `MATCH 'alander'`, `MATCH 'jordis'` | 1 hit each |
| F13 | gate | title `Café Société` | `MATCH 'cafe societe'` | 1 hit |
| F14 | gate | table with `remove_diacritics 0`, authors `Thomas Müller` | `MATCH 'muller'` | 0 hits |
| F15 | observation | authors `Thomas Müller` | `MATCH 'mueller'` | expected 0 |
| F16 | observation | title `Größenordnungen verteilter Systeme` | `grossenordnungen` / `großenordnungen` / `grössenordnungen` | expected 0 / 1 / 0 |
| F17 | observation | title `Die Straße` | `strasse` / `STRASSE` / `straße` | expected 0 / 0 / 1 |
| F18 | observation | tags `architektur/ddd` | `architektur`, `ddd`, `"architektur ddd"` | expected 1/1/1 |
| F19 | observation | - | `trigram` tokenizer, `MATCH 'ubernet'` on `Kubernetes` | record availability |
| F20 | gate | - | `PRAGMA journal_mode=WAL` on a file DB, reopen | `wal`, persists |

WAL runs (separate processes, one connection each, `Default Timeout=5`, `journal_mode=WAL; synchronous=NORMAL; busy_timeout=5000`, copy of the 10k-work DB, 30 s): **W1** 1 writer + 4 readers; **W2** 2 writers + 4 readers (writer: `BEGIN IMMEDIATE`, update 50 random works and their FTS rows, `COMMIT`, sleep 10 ms; reader: random query, read all hits); **W3** (observation) a writer holding a transaction 6 s and a second writer: is `SQLITE_BUSY` surfaced after 5 s and how.

Vector checks (vectors: seeded xoshiro256**, Box-Muller Gaussian, L2-normalized float32; BLOB = little-endian float32):

| ID | Kind | Action | Expected |
|---|---|---|---|
| V1 | gate | `LoadExtension(<abs path>/vec0.<ext>)`, `SELECT vec_version()` | `v0.1.9` |
| V2 | gate | `vec0` table `float[384] distance_metric=cosine`, 1,000 vectors, KNN `MATCH ? AND k = 10` | equals brute-force top-10 for 20 queries |
| V3a | observation | absolute path with suffix | ok/error |
| V3b | gate (B1) | library copied next to the binary, `Path.Combine(AppContext.BaseDirectory, "vec0")` (no suffix) | ok |
| V3c | observation | bare name `vec0`, library next to the binary, other working directory | record |
| V3d | observation | bare name with `DYLD_LIBRARY_PATH`/`LD_LIBRARY_PATH` set | record |
| V4 | gate | `proc: null` and `proc: "sqlite3_vec_init"` | both load |
| V5 | gate | `LoadExtension` of a missing file | `SqliteException`, process continues |
| V6 | gate | `float[1024]` table, same as V2 | passes |
| V7 | observation | 383 floats into `float[384]` | record error |
| V8 | observation | `LoadExtension` without `EnableExtensions(true)` | record |
| V9 | observation, macOS | `com.apple.quarantine` set on a copy of the dylib, load | record |

Latency data: `works`/`works_fts` 10,000 rows from the synthetic library's `ground-truth.json`; `chunks`/`chunks_fts` 100,000 rows (10 per work, 400-600 words of the generator's vocabularies, Zipf-weighted); vectors 10,000 x 384 and x 1024, 100,000 x 384 and x 1024 (seeded random unit vectors); optional 1,000,000 x 384 behind `--large` (information only). FTS queries `ORDER BY bm25(...) LIMIT 20`: a fixed hand-written file plus a generated set (10 terms each from three document-frequency buckets and 10 two-term ANDs). Vector queries: 100 seeded vectors, `k = 10`.

### Method

Prototype in `experiments/storage/` (outside `shelf.slnx`, not built by the existing CI jobs, own props that do not import the root, `TreatWarningsAsErrors=false`, `PublishAot=true`, `IsAotCompatible=true`). `Baseline` (prints `ok`, no SQLite) and `Storage` (all commands). `measure.sh` publishes with `-c Release -r osx-arm64 -p:TrimmerSingleWarn=false`, runs all checks on the AOT binary, loads data, benchmarks (in-process percentiles; per process: 5 warmups, 50 runs, page cache warm), and prints a gate summary. `check.sh <rid>` does the functional subset (no timing gates) for the CI workflow. Percentiles use nearest rank (same rule as `measure.sh`). Measurements are taken on AC power.

Brute-force variants: `bf-mem` (vectors in memory, compute only), `bf-sql` (read all BLOBs from SQLite then scan; the realistic CLI path, gate C3), `bf-mmap` (flat file, informational). Cosine = dot product on unit vectors; `TensorPrimitives.Dot` and a hand-written `Vector128/256` loop are both recorded; `--threads 1` and `all`.

## Research

Retrieved and inspected 2026-10-16 (source index at the end). One deliberate exception to ADR 0001's method: the four official sqlite-vec loadable binaries were downloaded (single requests to the release asset URLs, SHA-256 equal to the `digest` field of the release API, pinned in `experiments/storage/sqlite-vec.sha256`) and the `osx-arm64` one was **executed** (loaded). The other three were only inspected (`file`, `nm`, `llvm-objdump -T`, `llvm-readelf -d`, `otool -L`, `codesign -dv`), not executed. Every archive was unpacked into its own new directory under the git-ignored `experiments/storage/vendor/<rid>/`, no script from the archive exists (each archive holds exactly one file) or was run.

| ID | Finding | Source |
|---|---|---|
| R1 | Release `v0.1.9`, published 2026-03-31, not a prerelease. Per RID the loadable asset is `sqlite-vec-0.1.9-loadable-<os>-<arch>.tar.gz` and contains **one file** (`vec0.dylib` / `vec0.so`), no license file. `osx-arm64` = `macos-aarch64`: archive 50,836 B, `vec0.dylib` 161,896 B; `osx-x64` = `macos-x86_64`: 51,404 B, 127,632 B; `linux-x64` = `linux-x86_64`: 61,507 B, `vec0.so` 159,816 B; `linux-arm64` = `linux-aarch64`: 61,046 B, 156,520 B. SHA-256 of the archives: `82821263...bfd29` (macos-aarch64), `53ad76e4...a4713` (macos-x86_64), `b959baa1...2d5d7` (linux-x86_64), `ea03d395...a37c` (linux-aarch64); full values in `sqlite-vec.sha256`. Statically linked and amalgamation assets also exist and were not used. | S1, S9 |
| R2 | Repository root has `LICENSE-MIT` ("Copyright (c) 2024 Alex Garcia") and `LICENSE-APACHE` (Apache License 2.0). `sqlite-dist.toml` declares `license = "MIT OR Apache"`. This resolves the "either license" question of ADR 0001 row 10: dual licensed, the recipient may choose; both are GPL-3.0-compatible per ADR 0001 policy (Apache-2.0 and MIT). The tarballs carry no license text, so a shelf release must ship the notice itself (MIT text with the copyright line, or Apache-2.0 text). The README has no license section. **B5: pass.** | S2, S5 |
| R3 | Entry point: exported `sqlite3_vec_init` (`nm -gU` / `nm -D`), also `sqlite3_vec_numpy_init`, `sqlite3_vec_static_blobs_init`. The file name `vec0` derives the default entry `sqlite3_vec_init` in SQLite, so `proc: null` works (check V4 passed). `sqlite-vec.c` declares `SQLITE_EXTENSION_INIT1`, i.e. it is built as a loadable extension using the `sqlite3_api_routines` indirection (no link-time dependency on a `libsqlite3`). | S2, S4, S9 |
| R4 | Built by `make loadable static` in `release.yaml` on `ubuntu-22.04` (x64), `ubuntu-22.04-arm`, `macos-15-intel`, `macos-14` (arm64). The Makefile adds SIMD flags **only on Darwin**: `-mavx -DSQLITE_VEC_ENABLE_AVX` on x86_64 macOS (the `osx-x64` binary therefore requires AVX), `-mcpu=apple-m1 -DSQLITE_VEC_ENABLE_NEON` on arm64 macOS. Linux binaries are built without the SIMD macros (scalar C). SIMD is chosen at compile time, there is no runtime dispatch. Whether the `osx-x64` binary really contains AVX instructions was not verified by disassembly. Largest vector dimension 8192 (`SQLITE_VEC_VEC0_MAX_DIMENSIONS`). | S2, S3, S4 |
| R5 | `osx-arm64` `vec0.dylib` from the release: signature `adhoc,linker-signed` (`codesign -dv`), no team id, no quarantine attribute after `curl` download, links only `/usr/lib/libSystem.B.dylib`; install name `dist/vec0.dylib`. `osx-x64` `vec0.dylib`: "code object is not signed at all" (x86_64 does not require a signature). The AOT `proto-storage` binary and `libe_sqlite3.dylib` are also ad-hoc linker-signed without hardened runtime. Measured, not documented: a copy with `com.apple.quarantine` set loads fine by absolute path (V9). Not verified: Apple's documentation on Gatekeeper, quarantine and library validation was not consulted, and no notarized or hardened-runtime build was tested (M5, owner decision). | S9, measure.out |
| R6 | `linux-x64` `vec0.so`: needs only `libc.so.6`; highest required symbol version `GLIBC_2.14`. `linux-arm64`: `libc.so.6` and `ld-linux-aarch64.so.1`; highest `GLIBC_2.17`. No `libsqlite3.so` in `NEEDED` (ADR 0001 S66 risk does not apply). Both binaries are not stripped. The glibc floor of the extension is far below any current distribution; the floor of the AOT binary itself is set by the .NET runtime image and the `libe_sqlite3.so` build, which the CI artifact records only indirectly (not analyzed here). | S9 |
| R7 | `libe_sqlite3` of `SQLitePCLRaw.lib.e_sqlite3` 2.1.12, observed through `PRAGMA compile_options` on the AOT binary (`osx-arm64`; the other RIDs are recorded by `check.sh` in the CI artifacts): SQLite 3.53.3, `ENABLE_FTS3/4/5`, `ENABLE_RTREE`, `ENABLE_GEOPOLY`, `ENABLE_MATH_FUNCTIONS`, `ENABLE_PREUPDATE_HOOK`, `ENABLE_SESSION`, `ENABLE_SNAPSHOT`, `ENABLE_COLUMN_METADATA`, `THREADSAFE=1`, `DEFAULT_WAL_SYNCHRONOUS=2`, `DEFAULT_SYNCHRONOUS=2`, `DEFAULT_WAL_AUTOCHECKPOINT=1000`, `DEFAULT_FOREIGN_KEYS`, `DQS=0`, `MAX_VARIABLE_NUMBER=32766`, `TEMP_STORE=1`, `DEFAULT_PAGE_SIZE=4096`, no `OMIT_LOAD_EXTENSION`; `sqlite3_load_extension`, `sqlite3_enable_load_extension`, `sqlite3_auto_extension` are exported. Full list in `measure.out`. Consequences for M1: double-quoted string literals are errors (`DQS=0`), foreign keys are on by default, `synchronous` defaults to FULL (`2`) and the spike pragma `synchronous=NORMAL` is therefore a deliberate setting. | measure.out, S9 |
| R8 | `SqliteConnection.LoadExtension(file, proc)` at tag `v10.0.12`: if the connection is open and extensions are not enabled it calls `sqlite3_db_config(ENABLE_LOAD_EXTENSION)` itself (so `EnableExtensions(true)` is not required; V8 confirms). It tries `file` as given first; only if `file` has no directory part does it also try `lib<file>` (non-Windows) and then each directory of `AppContext.GetData("NATIVE_DLL_SEARCH_DIRECTORIES")`. In the Native AOT binary that key is not populated, so a bare name is resolved only by the operating system's `dlopen` rules: V3c (bare `vec0`, library next to the binary, other working directory) **fails**; V3d (bare name with `DYLD_LIBRARY_PATH`) works. A path built from `AppContext.BaseDirectory` works (V3b). SQLite appends the platform suffix itself (V3a/V3b without suffix ok). | S6, measure.out |
| R9 | `Microsoft.Data.Sqlite.Core.csproj` at `v10.0.12` sets `IsAotCompatible=true` (ADR 0001 only checked `main`); its only package reference is `SQLitePCLRaw.core`; `Microsoft.Data.Sqlite` 10.0.12 depends on `Microsoft.Data.Sqlite.Core` 10.0.12 and `SQLitePCLRaw.bundle_e_sqlite3` 2.1.12. The AOT publish of the prototype, including `SqliteConnection`, parameters, readers, `LoadExtension` and exceptions, produced **0 warnings** and the checks pass at runtime (so provider initialization of the bundle works without reflection problems). The SQLitePCLRaw 2.1.12 source itself (provider init path) was not read. | S6, S7, S8, measure.out |
| R10 | Native AOT publish writes `libe_sqlite3.dylib` **next to** the binary (`out/Storage/`, 1,696,624 B); it is not embedded. So are any extension libraries. Where the .NET documentation says the same (including that `IncludeNativeLibrariesForSelfExtract` does not apply) was not looked up; the observation is what counts. No static e_sqlite3 variant was evaluated (owner decision G7). | measure.out |
| R11 | `System.Numerics.Tensors` 10.0.12: MIT, group `net10.0` with no dependencies (also `net8.0`, `net9.0`, `netstandard2.0`, `net462`). Not in-box for `net10.0` (the prototype needs the package for `TensorPrimitives.Dot`). The `IsAotCompatible` claim was not read at the tag; the AOT publish with `TensorPrimitives.Dot` produced 0 warnings. | S8, measure.out |
| R12 | README (v0.1.9): "pre-v1, so expect breaking changes"; float, int8 and binary vectors in `vec0` virtual tables; metadata, auxiliary and partition-key columns; pure C. `distance_metric=cosine` and `k = ?` KNN syntax work (V2). The source contains no ANN index (no IVF/DiskANN/HNSW code; only a comment crediting hnswlib): KNN is an exact scan, consistent with the linear time growth measured below. | S2, S4, S5 |
| R13 | `HiraokaHyperTools.sqlite-vec` 0.1.9 (MIT, authors kenjiuno and Alex Garcia) ships `runtimes/{linux-x64,linux-arm64,osx-x64,osx-arm64,win-x64}/native/vec0.*`; the four files are **byte-identical** (SHA-256) to the official release assets. Informational; the M3 decision (Q11) stays: pinned download of the official assets. | S10 |

## Measurements

Reproduce: `experiments/storage/measure.sh --large > results/measure.out 2>&1` (the committed `measure.out` is the unedited output). `check.sh <rid>` is the functional subset for the four RIDs.

**Environment:** 2026-10-16 (UTC 17:23), macOS 26.6.2 arm64, Apple M4 (`sysctl hw.ncpu` = 10; `threads=all` ran with 10 threads), 24 GiB RAM, .NET SDK 10.0.401, machine on **AC power** (`pmset`: "Now drawing from 'AC Power'"). `hyperfine` is not installed, so the fallback of ADR 0003 was used: zsh loop with `$EPOCHREALTIME`, 5 warmups, 50 runs, stdout to `/dev/null`, process start to exit, page cache warm. Libraries: Microsoft.Data.Sqlite 10.0.12, SQLitePCLRaw.bundle_e_sqlite3 2.1.12 (SQLite 3.53.3), System.Numerics.Tensors 10.0.12, sqlite-vec v0.1.9. Data: synthetic reference library (10,000 works, seed 42), 100,000 chunks of 400-600 words, vectors Gaussian unit float32. Earlier complete runs of the same script gave the same pass/fail results; timings varied by a few percent, the p95 of the `threads=1` brute-force process runs by up to 20 % (155 to 176 ms at 100,000 x 384), which does not touch any decision. The data set is limited: the generator's vocabulary has 1,484 distinct terms in the works index and 1,116 in the chunk index, so the "df 1-10" bucket is **empty** and all chunk-index queries use terms occurring in more than 2,000 chunks (worst case for posting-list length, optimistic for rare-term queries).

### Part A: SQLite + FTS5 (AOT binary, `osx-arm64`)

- AOT publish: **0 warnings** (all codes) for `Baseline` and `Storage`. Sizes: `proto-storage-baseline` 1,236,104 B; `proto-storage` 3,792,736 B (+2,556,632 B for Microsoft.Data.Sqlite, JSON source generator, System.Numerics.Tensors and the prototype code; not split further); `libe_sqlite3.dylib` 1,696,624 B; `vec0.dylib` 161,896 B; payload binary + native libs 5,651,256 B (without `.dSYM`).
- Functional checks: `fts-check` 15/15 gated rows pass (F1 equals `3.53.3`). Observations: F15 `mueller` finds 0 hits (no umlaut expansion); F16 `großenordnungen` 1, `grossenordnungen` 0, `grössenordnungen` 0 (ß is not folded to ss); F17 `straße` 1, `strasse` 0, `STRASSE` 0; F18 `architektur/ddd`: `architektur` 1, `ddd` 1, `"architektur ddd"` 1 (the slash separates tokens); F19 trigram tokenizer available (`ubernet` finds `Kubernetes`). Output is identical under `LANG=de_DE.UTF-8 LC_ALL=de_DE.UTF-8`.
- Latency, in process (`ORDER BY bm25(...) LIMIT 20`, 10 warmups, each query 20 times): `works_fts` (10,000 works, 42 queries, 840 samples) overall p50 0.123 ms, **p95 2.898 ms**, max 3.338 ms; buckets: df 100-300 p95 0.130 ms, df >= 2,000 p95 2.943 ms, two-term AND p95 0.549 ms, fixed hand-written queries (prefix, phrase, column filter, umlaut) p95 1.566 ms. `chunks_fts` (100,000 chunks, 20 queries) overall p50 5.454 ms, **p95 48.106 ms** (target < 100 ms: met on this data), max 50.680 ms; two-term AND p95 16.093 ms.
- Cold start, per process: no-op `proto-storage-baseline` median 3.35 ms, p95 3.71 ms; `search` on `works_fts` (open, WAL pragmas, query, print 20 hits) median 6.79 ms, **p95 7.91 ms**; `search --content` median 16.66 ms, p95 17.27 ms.
- Data loading (100,000 chunks, external-content `chunks_fts`): load of the 10,000 works 152 ms, chunks 5,686 ms; database 532,312,064 B (508 MiB) after `optimize`.
- WAL (separate processes, `busy_timeout=5000`, 30 s): **W1** (1 writer + 4 readers): 1,564 writer transactions of 50 updated works and FTS rows, **0 failed operations** in all processes, writer lock wait p95 0.05 ms, reader p50 0.57-0.61 ms, p95 4.94-4.98 ms (max 58.17 ms). **W2** (2 writers + 4 readers): 1,456 and 1,453 transactions, **0 failed**, writer lock wait p95 9.16 / 9.92 ms, reader p95 5.38-5.39 ms. **W3** (observation): a second writer waiting for a transaction held 6 s fails after 5,202 ms with `SqliteException`, `SqliteErrorCode` 5, `SqliteExtendedErrorCode` 5, message "SQLite Error 5: 'database is locked'". **Observation, M1 risk:** with readers that query without pause (W1, W2), the WAL file grew to 1.12 GB (W1) and 2.08 GB (W2) during 30 s because the automatic checkpoint (every 1,000 pages) cannot reset the log while a reader always holds a snapshot; with readers that pause 20 ms between queries (W1t) the maximum WAL size was 8.2 MB. All WAL files were checkpointed and removed when the last connection closed. M1 must decide checkpoint policy and `journal_size_limit` with a realistic reader pattern (CLI processes are short, `serve`/`mcp` may hold connections).

### Part B/C: vectors (`osx-arm64`)

Process-level time of one KNN query (k = 10, one query vector per process) over 50 runs after 5 warmups; p95 in ms, median in brackets. `vec` = sqlite-vec (`LoadExtension` + `MATCH ... AND k = 10`); `bf-sql` = brute force over all BLOBs read from SQLite (`TensorPrimitives.Dot`; `t1` = one thread, `all` = 10 threads); `bf-mmap` = flat file, informational.

| Rows x dim | vec | bf-sql t1 (gated) | bf-sql all | bf-mmap t1 | bf-mmap all |
|---|---|---|---|---|---|
| 10,000 x 384 (work level) | 10.40 (9.81) | 20.78 (19.96) | 29.71 (20.37) | 7.51 (5.54) | 5.33 (4.69) |
| 10,000 x 1024 | 17.54 (17.16) | 41.51 (30.39) | 28.95 (28.18) | 7.28 (6.57) | 6.12 (5.63) |
| 100,000 x 384 (chunk level) | 46.29 (43.49) | 175.60 (142.58) | 143.64 (139.97) | 15.73 (15.41) | 9.66 (9.11) |
| 100,000 x 1024 | 112.04 (111.02) | 270.00 (232.51) | 230.15 (225.67) | 34.92 (34.20) | 16.64 (15.80) |
| 1,000,000 x 384 (information) | 446.92 (415.93) | 1,565.81 (1,394.16) | 1,509.89 (1,365.53) | 138.44 (111.08) | not run |

Phase breakdown, one process, 100,000 x 384 (three runs each, ms): `vec`: open 1.1-1.2, extension load 0.24-0.25, KNN 36.9-37.7, total 38.7-39.5. `bf-sql`: open 1.2, **BLOB read into memory 129.9-131.8**, scan 3.6-3.7 (`Dot` from `TensorPrimitives`) or 2.7 (hand-written `Vector128` loop), total 135-137. In-process (vectors already in memory, 100 queries, p50 / p95, ms): `vec` 36.85 / 37.42 at 100,000 x 384 and 104.62 / 109.75 at x 1024; `TensorPrimitives` scan 3.61 / 4.66 (x 384) and 11.39 / 12.05 (x 1024); hand-written SIMD loop 2.31 / 2.59 and 6.54 / 8.97; with 10 threads 1.51 and 3.91. At 10,000 x 384: `vec` 3.50, `bf` 0.36 (tp), 0.22 (manual). So the brute-force **scan** is 10x faster than the sqlite-vec scan, but reading the vectors out of SQLite each process costs 130 ms at 100,000 x 384 (BLOB copy through `SqliteDataReader.GetBytes`), which dominates; a persistent in-memory cache (`serve`/`mcp`) would remove it, a short-lived CLI process cannot.

**Correctness:** `knn-bench` compares 100 queries per data set with a scalar double-precision reference: sqlite-vec (`vec`) and all brute-force variants returned identical IDs, order and distances (< 1e-5) for 10,000 and 100,000 rows at 384 and 1024 dimensions (100/100 each), and 20/20 at 1,000,000 x 384. `vec-check`: 6/6 gated rows pass.

**Extension loading (osx-arm64, AOT binary):** V1 absolute path with suffix ok; V3a absolute path without suffix ok; **V3b** `Path.Combine(AppContext.BaseDirectory, "vec0")` ok (gate); V3c bare `vec0` with the library next to the binary and another working directory: **error** (`dlopen` finds nothing; `SqliteException`, `SqliteErrorCode` 1); V3d bare name with `DYLD_LIBRARY_PATH=vendor/osx-arm64` ok, control without the variable fails; V4 `proc: null` and `proc: "sqlite3_vec_init"` both load; V5 missing file: `SqliteException` (`SqliteErrorCode` 1, message begins "dlopen(...) ... (no such file)"), the process continues and a later valid load succeeds; V7 383 floats into `float[384]`: error "Dimension mismatch for inserted vector for the "embedding" column. Expected 384 dimensions but received 383."; V8 loading without `EnableExtensions(true)` works (R8); V9 quarantined copy loads by absolute path. `vec_version()` = `v0.1.9`.

Database sizes: 10,000 x 384 vectors take 15,998,976 B in a `vec0` table (about the 15.4 MB of the raw floats) plus the BLOB copy; 100,000 x 384 156,602,368 B; 100,000 x 1024 413,757,440 B; 1,000,000 x 384 1,562,054,656 B. Load time into `vec0`: 1.15 s for 100,000 x 384, 11.0 s for 100,000 x 1024, 14.4 s for 1,000,000 x 384.

### Four platforms

`experiments/storage/check.sh <rid>` ran locally on `osx-arm64` (`results/ci/osx-arm64.txt`: publish with 0 warnings, `fts-check`, `vec-check`, smoke data, short W1 with 0 failed operations, all ok). `linux-x64` was run by the `spike-storage` workflow (CI run 39013676292 on PR #15 (`spike-storage`, 2026-10-17), `check linux-x64: ok`); `linux-arm64` and `osx-x64` were dropped as target platforms and never run. Static facts of those binaries (R1, R3, R4, R6, `osx-x64` unsigned) are verified from the release files. **What B1 and B2 mean on the CI RIDs:** B1 = `vec-check` V1 and V3b pass (official `vec0.*` loads from `AppContext.BaseDirectory` in the AOT binary); B2 = `vec-check` V2 and V6 (1,000 vectors, 20 queries each at 384 and 1024 dims, exact against the reference) plus `knn-bench --mode vec` with 100 queries on 10,000 x 384 and on 1,500 x 1024 vectors (100/100 verified). The full-size B2 data sets (100,000 rows) and all timing gates (A4, A5, B4, C3) are measured on `osx-arm64` only.

## Gate results (`osx-arm64`, AOT binary)

| Gate | Result | Measured |
|---|---|---|
| A1 AOT publish, 0 warnings | **pass** | 0 warnings |
| A2 F-checks, AOT binary | **pass** on `osx-arm64` and `linux-x64` | 15/15 |
| A3 WAL, 0 failed ops, W1 reader p95 < 100 ms | **pass** | W1 0 failed, p95 4.98 ms; W2 0 failed, p95 5.39 ms |
| A4 works FTS p95 < 100 ms | **pass** | 2.898 ms |
| A5 cold start `search` p95 < 150 ms | **pass** | 7.91 ms (baseline process 3.71 ms) |
| Target: chunk FTS p95 < 100 ms | met | 48.106 ms |
| B1 `LoadExtension` next to the binary | **pass** on `osx-arm64` and `linux-x64` | V3b ok |
| B2 exact KNN | **pass** | 100/100 per table and dimension |
| B3 0 warnings with the extension path | **pass** | same binary |
| B4 work <= 100 ms, chunk <= 400 ms, per process | **pass** | 10.40 ms and 46.29 ms |
| B5 license | **pass** | MIT OR Apache-2.0 (R2) |
| C1 / C2 / C3 brute force | **pass / pass / pass** | exact; `bf-sql` t1 20.78 ms and 175.60 ms |

## Decision

**A (SQLite + FTS5): confirmed on `osx-arm64` and `linux-x64`.** On `linux-x64` (CI run 39013676292 on PR #15 (`spike-storage`, 2026-10-17)) A1 (0 AOT/trim warnings) and A2 (15/15) pass. `osx-x64` and `linux-arm64` are not supported platforms and were not run. Microsoft.Data.Sqlite 10.0.12 with `SQLitePCLRaw.bundle_e_sqlite3` 2.1.12 (SQLite 3.53.3) passes A1-A5 in a Native AOT binary on `osx-arm64`, with large margins (works search p95 2.9 ms in process; whole `search` process p95 7.9 ms). The `unicode61 remove_diacritics 2` tokenizer behaves as planned (rows F10-F18).

**Vector search: sqlite-vec v0.1.9 (option A), as owner-decided when both options pass** (F-SEM-4 stays). Both options pass the budgets on `osx-arm64`: sqlite-vec 10.4 ms / 46.3 ms against the 100 / 400 ms budgets, the brute-force fallback 20.8 ms / 175.6 ms. sqlite-vec is also 2 to 4 times faster per process than the realistic brute-force path (2.0x at 10,000 x 384, 3.8x at 100,000 x 384) and leaves more of the PRD's 300 / 800 ms for query embedding. The conclusion was conditional on the other RIDs (B1/B2 pass on `linux-x64`, see below): rule 3 says that if B1 or B2 fails on any RID, brute force becomes the single code path on all RIDs (the PRD F-SEM-4 deviation then has to be recorded here and shown to the owner). B1/B2 on `linux-x64` pass (CI run 39013676292 on PR #15 (`spike-storage`, 2026-10-17)): `vec0.so` loads next to the AOT binary (`vec_version=v0.1.9`, V3b), `vec-check` 6/6, `knn-bench` 100/100 verified at 10,000 x 384 (p50 4.39 ms, p95 4.56 ms) and 1,500 x 1024 (p95 3.35 ms); `LoadExtension` without `EnableExtensions(true)` works (V8). The short W1 run had 0 failed operations; timing numbers on the runner are informational only. B1/B2 do not fail on any supported RID, so rule 3 is not triggered and sqlite-vec stays.

**Fallback threshold** (rule 4; brute force `bf-sql`, `threads=1`, linear interpolation between measured sizes, no extrapolation beyond 2x the largest measured size): at 384 dimensions the chunk-level budget (400 ms) is reached at about **245,000 vectors** (between 100,000 at 175.6 ms and 1,000,000 at 1,565.8 ms), the work-level budget (100 ms) at about **56,000 vectors**; at 1024 dimensions about **151,000 vectors** (chunk level, extrapolated from 10,000 and 100,000 within the 2x limit) and about **33,000 vectors** (work level). With real chunking (Q4 of the plan: 150-250 chunks per work, 1.5-2.5 million chunks at 10,000 works) the fallback would **not** meet the chunk budget (about 2.3-3.9 s at 1.5-2.5 million x 384 by linear scaling of the 1,000,000 figure; informational only, 2.5 million is outside rule 4's 2x limit of the largest measured size); sqlite-vec would not either (about 447 ms already at 1,000,000 x 384, so about 0.7-1.1 s at 1.5-2.5 million). That is rule 5 territory for M3 (candidates: `int8`/binary quantization in sqlite-vec, fewer or larger chunks, an in-memory index in `serve`/`mcp`), not a decision of this spike; the gate was set at 100,000 chunks by the owner.

**G7 (single-file binary):** with SQLite the Native AOT output is not one file. Per platform the archive contains `shelf` (3.8 MB for the prototype), `libe_sqlite3.<dylib|so>` (1.7 MB) and `vec0.<dylib|so>` (0.16 MB), the libraries next to the binary; the extension is loaded through `Path.Combine(AppContext.BaseDirectory, "vec0")`. Accepted until M5 (owner decision).

## Consequences

- **Pins:** Microsoft.Data.Sqlite 10.0.12, SQLitePCLRaw.bundle_e_sqlite3 2.1.12 (explicit in `Directory.Packages.props` with M1), sqlite-vec v0.1.9. `System.Numerics.Tensors` is **not** needed if sqlite-vec is adopted; if the fallback is ever implemented, the hand-written `Vector128/256` loop was 1.5x to 1.8x faster than `TensorPrimitives.Dot` on `osx-arm64` and avoids a package.
- **Hand-off M1 (`Shelf.Index`):** connection setup `journal_mode=WAL`, `synchronous=NORMAL`, `busy_timeout=5000` (`Default Timeout=5`); tokenizer `unicode61 remove_diacritics 2`; FTS5 `bm25(works_fts, 10.0, 5.0, 8.0, 1.0, 3.0)` as starting weights; single quotes only for string literals (`DQS=0`), foreign keys on by default; external-content `chunks_fts`; WAL checkpoint policy and `journal_size_limit` under a realistic reader pattern (WAL growth observation); `SQLITE_BUSY` surfaces as `SqliteException` with `SqliteErrorCode` 5 after the timeout. Unit tests to write there: WAL/pragma setup on connection open, bm25 weight mapping. Feature rows from F10-F19 become `Examples` rows of the M1 search feature (`@F-SRC-1 @M1`, `@F-SRC-2 @M1`); the WAL result becomes a scenario tagged `@PRD-9.3 @M1`. No umlaut expansion (`mueller`, `strasse` do not match `Müller`, `Straße`) in M1 (owner decision), to be revisited with F-SRC-6 (M5).
- **Hand-off M3 (`Shelf.Semantic`):** load the extension with an absolute path from `AppContext.BaseDirectory` (never a bare name, never `NATIVE_DLL_SEARCH_DIRECTORIES`); catch `SqliteException` on load to detect a missing or incompatible library (V5) and fall back or report; `vec0` tables with `distance_metric=cosine`, `k = ?`; vectors as little-endian float32 BLOBs (unit length is not required by sqlite-vec but the brute-force path assumes it); unit tests: vector BLOB codec, extension path resolution; recalibrate with real chunking (1.5-2.5 million chunks) and decide quantization before committing to the chunk budget. The build fetches the official release assets with a pinned SHA-256 (as `fetch-sqlite-vec.sh` does), not the third-party package.
- **Distribution (M5):** per platform ship the binary, `libe_sqlite3.*` and `vec0.*` together; add the sqlite-vec license notice (MIT or Apache-2.0 text, "Copyright (c) 2024 Alex Garcia") and the SQLite public-domain notice to the notices file. `osx-arm64`: everything is ad-hoc linker-signed; a quarantined `vec0.dylib` still loads (no hardened runtime). With a hardened-runtime or notarized `shelf` build the dylib needs the same team signature or `com.apple.security.cs.disable-library-validation` (not tested; M5). `osx-x64` `vec0.dylib` is unsigned in the release and requires AVX (compile flag `-mavx`). Linux: glibc floor of `vec0.so` is 2.14 (x64) / 2.17 (arm64); the floor of the whole archive is set by the .NET runtime and `libe_sqlite3.so` (not analyzed).
- **Re-check triggers:** sqlite-vec 0.2 or 1.0 (pre-v1, on-disk format and SQL may change; F-LIB-6 makes the index rebuildable), SQLitePCLRaw 3.x or Microsoft.Data.Sqlite 11, a result of the `spike-storage` workflow that contradicts B1/B2 on another RID, a requirement for a literal single file (needs a self-built static SQLite and sqlite-vec per RID), chunk counts above 250,000.
- **Workflow and prototype:** `.github/workflows/spike-storage.yml` is not required, is not part of the `ci` aggregate of `build.yml`, and runs `check.sh` on `ubuntu-24.04` and `macos-15`. It is removed when M0 closes, together with `experiments/storage/` (the ADR then points to the last commit that contains them), or replaced by the aggregation slice's combined spike job. The `results/ci/<rid>.txt` files of the first run are in the workflow artifacts `storage-check-<rid>`; copy them into the repository before the experiment is removed.
- **Not verified in this ADR:** `linux-arm64` and `osx-x64` (not supported); the e_sqlite3 compile options at run time on `linux-x64` (only the functional F-checks were run there); Apple documentation on quarantine/library validation; the SQLitePCLRaw 2.1.12 provider source; the .NET documentation on native library placement; real-world embedding times (ONNX spike); sqlite-vec behavior with real chunk counts of 1.5-2.5 million.

## Source index

Retrieved 2026-10-16. ADR 0001 sources are cited as "ADR 0001 Sxx" (for example the package metadata sources S50-S74); this index starts at S1. `gh:` means `https://api.github.com/<path>`.

| ID | Source |
|---|---|
| S1 | `gh:repos/asg017/sqlite-vec/releases/tags/v0.1.9` (asset names, sizes, `digest` SHA-256 values, publication date); the four `-loadable-` assets downloaded from the `browser_download_url` of that response, one request each |
| S2 | `gh:repos/asg017/sqlite-vec/contents/?ref=v0.1.9` (file list), `.../LICENSE-MIT`, `.../LICENSE-APACHE`, `.../VERSION` (`0.1.9`), `.../sqlite-dist.toml` (`license = "MIT OR Apache"`), `.../Makefile`, `.../ARCHITECTURE.md` at `ref=v0.1.9` |
| S3 | `gh:repos/asg017/sqlite-vec/contents/.github/workflows/release.yaml?ref=v0.1.9` |
| S4 | `gh:repos/asg017/sqlite-vec/contents/sqlite-vec.c?ref=v0.1.9` (`SQLITE_EXTENSION_INIT1`, `SQLITE_VEC_ENABLE_AVX`/`NEON`, `SQLITE_VEC_VEC0_MAX_DIMENSIONS`, `sqlite3_vec_init`) |
| S5 | `gh:repos/asg017/sqlite-vec/readme?ref=v0.1.9` |
| S6 | `gh:repos/dotnet/efcore/contents/src/Microsoft.Data.Sqlite.Core/SqliteConnection.cs?ref=v10.0.12` (`EnableExtensions`, `LoadExtension`, `GetLoadExtensionPaths`, `GetNativeDllSearchDirectories`) |
| S7 | `gh:repos/dotnet/efcore/contents/src/Microsoft.Data.Sqlite.Core/Microsoft.Data.Sqlite.Core.csproj?ref=v10.0.12` (`IsAotCompatible`, package references) |
| S8 | `https://api.nuget.org/v3-flatcontainer/<id>/<version>/<id>.nuspec` for `microsoft.data.sqlite` 10.0.12, `microsoft.data.sqlite.core` 10.0.12, `sqlitepclraw.bundle_e_sqlite3` 2.1.12, `system.numerics.tensors` 10.0.12 (license, dependencies, target frameworks); version list of `system.numerics.tensors` |
| S9 | Local inspection of the downloaded `vec0.dylib` / `vec0.so` files (`file`, `nm -gU`, `nm -D`, `otool -L`, `codesign -dv`, `xattr -l`, `llvm-objdump -T`, `llvm-readelf -d`); output in `experiments/storage/results/measure.out` and `results/ci/osx-arm64.txt` |
| S10 | `https://api.nuget.org/v3-flatcontainer/hiraokahypertools.sqlite-vec/0.1.9/hiraokahypertools.sqlite-vec.0.1.9.nupkg` (unzipped, listed, SHA-256 compared; also ADR 0001 S74) |
