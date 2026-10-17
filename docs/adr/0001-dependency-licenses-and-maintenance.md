# ADR 0001: Dependency licenses and maintenance status

| | |
|---|---|
| **Status** | Accepted (license policy and shelf's license confirmed by the project owner; rows with verdict `pending` are listed under Decision) |
| **Date** | 2026-10-12 (all sources retrieved on this date) |
| **Scope** | PRD 10.2 candidates, plus the native parts they pull in, the embedding models (PRD 6.7 / Q3) and the Svelte fallback (PRD Q1) |
| **Roadmap** | M0 work package "License and maintenance check" |

## Context

- PRD 10.2 requires that all technology candidates are checked for license and maintenance status before the project starts. This ADR records that check. It does not choose between alternatives that a separate M0 work package decides (CLI framework, AOT spike).
- **shelf's license:** GPL-3.0-or-later (decision of the project owner, 2026-10-12). `LICENSE` holds the GPLv3 text. No SPDX header or `PackageLicenseExpression` exists yet (follow-up, see Consequences).
- **License policy (decision of the project owner):** GPL-3.0-compatible. Permissive licenses (MIT, Apache-2.0, BSD, ISC, public domain) and GPL-3.0-compatible copyleft are acceptable. AGPL, non-OSI and usage-restricted licenses get verdict `reject` or `pending` with a stated reason.
- **Distribution model that drives the question** (PRD 9.2): one self-contained single-file binary per RID (`osx-arm64`, `osx-x64`, `linux-x64`, `linux-arm64`) that bundles managed and native code. JS assets are embedded in the binary. The embedding model is downloaded at runtime into `.shelf/models/` and is not shipped in the binary or mirrored by the project.
- **Not legal advice.** This is an engineering assessment. Compatibility is judged against the FSF license list (source S1).
- **Research method.** Facts come from NuGet metadata and `.nupkg` file listings, GitHub repositories (license files, releases, project files at the release commit, README), Hugging Face model repositories, the FSF license list, and the Microsoft .NET release metadata. Everything was retrieved read-only on 2026-10-12. Anything that could not be verified is marked `unverified`. Downloaded `.nupkg`/`.tgz` files were only listed or inspected statically (file listing, `strings`, `file`); nothing downloaded was executed or built.
- **Only direct dependencies** (and the native bundle each one needs) are assessed here. Transitive managed dependencies are checked once the real dependency graph exists after the AOT spike (decision Q10).
- **Out of scope by decision (Q5):** TOML parser, ULID generator, stemming library, trigram implementation. These are covered by later decisions (the TOML parser together with the CLI framework decision).

## Criteria

| Criterion | How it was determined |
|---|---|
| Version | Latest **stable** version on NuGet / GitHub releases / Hugging Face. Newest prerelease noted separately. |
| License | SPDX ID from package metadata **and** the license file in the repository. Differences are flagged. |
| GPL-3.0 compatibility | `yes` if the FSF list (S1) says the license is compatible with the GNU GPL (version 3), `no` if incompatible, `unclear` otherwise. |
| Maintenance | Signals: last release, stable releases in the last 12 months, last commit, commits in the last 6 months, open issues + PRs (GitHub counts both together), maintainers/organization. **Maintenance risk flag** if there is no release in 12 months, or no commit in 6 months, or a single maintainer without an organization. A flag does not reject a candidate by itself. Thresholds confirmed by the project owner. |
| AOT/trimming | Documented claim only: README/docs, or `IsAotCompatible`/`IsTrimmable` in the project file at the release commit. Values: `claimed`, `claimed with limits`, `no claim`, `documented incompatible`. **Nothing here is a verified AOT result; that is the AOT spike's job.** |
| Native assets per RID | `runtimes/<rid>/native/` entries of the `.nupkg` (listed with `unzip -l`) or GitHub release asset names, for `osx-arm64`, `osx-x64`, `linux-x64`, `linux-arm64`. |
| Models | License from the model card metadata, ONNX availability and size, tokenizer type, embedding dimension, max sequence length. |

Verdict values: `adopt`; `adopt with fallback` (usable, a risk exists, fallback named; PRD fallbacks: ReadyToRun instead of Native AOT, Svelte instead of RazorSlices, brute-force cosine in C# instead of sqlite-vec); `pending` (deciding step named); `reject`. Row 23 (bge-m3) carries `deferred, informational`: the license is assessed, but the model is not in use.

## Summary

Source IDs (S-numbers) in the "Sources" columns and in the notes refer to the source index at the end. Every source was retrieved on 2026-10-12.

Legend: RIDs are `osx-arm64 / osx-x64 / linux-x64 / linux-arm64`, `Y` = prebuilt native binary present, `N` = absent, `-` = no native code in this package, `?` = unverified. "flag" = maintenance risk flag. Dates are release dates.

### Rows 1-20 (PRD 10.2)

| # | Candidate | Version (date) | License (SPDX) | GPL-3.0 compatible | Maintenance | AOT/trim claim | Native RIDs | Verdict | Sources |
|---|---|---|---|---|---|---|---|---|---|
| 1 | .NET 10 runtime/SDK (ASP.NET Core, Extensions.Logging, ILCompiler) | 10.0.12 (2026-09-08); LTS, EOL 2028-11-14 | MIT | yes | ok (Microsoft) | ASP.NET Core Minimal APIs: partial; MVC: not supported | - (see Consequences) | adopt | S1, S3, S4, S5, S6, S7, S8, S9, S10 |
| 2 | Spectre.Console.Cli (+ Spectre.Console) | 0.57.2 (2026-09-29) | MIT | yes | ok | **documented not AOT-compatible** (`IsAotCompatible=false`) | - | adopt (license/maintenance); selection: CLI framework decision | S1, S11-S17 |
| 3 | System.CommandLine | 2.0.12 (2026-09-08); 3.0.0-rc.1 prerelease | MIT | yes | ok (Microsoft) | claimed (`IsAotCompatible=true`) | - | adopt (license/maintenance); selection: CLI framework decision | S1, S18-S22 |
| 4 | VersOne.Epub | 3.3.6 (2026-03-15) | Unlicense | yes | flag: single maintainer | no claim | - | adopt with fallback | S1, S23-S27 |
| 5 | PdfPig (UglyToad.PdfPig) | 0.1.16 (2026-08-22) | Apache-2.0 | yes | ok | no claim | - | adopt | S1, S28-S32 |
| 6 | PDFtoImage | 5.4.0 (2026-08-16); 6.0.0-preview | MIT | yes | flag: single maintainer | claimed (`IsAotCompatible=true`) | Y / Y / Y / Y (via row 7 and SkiaSharp) | adopt with fallback | S1, S33-S43 |
| 7 | PDFium (bblanchon/pdfium-binaries) | 157.0.8086 (2026-10-05); PDFtoImage pins 152.0.7961 | BSD-3-Clause + Apache-2.0 (upstream); MIT (packager); NuGet metadata says Apache-2.0 | yes (PDFium; bundled third-party libs unverified, see Q10/transitive check) | flag: single maintainer (packager) | n/a (native) | Y / Y / Y / Y | adopt with fallback | S1, S37, S40, S41, S44-S49 |
| 8 | Microsoft.Data.Sqlite | 10.0.12 (2026-09-08) | MIT | yes | ok (Microsoft) | claimed (`IsAotCompatible=true` on Microsoft.Data.Sqlite.Core at `main`) | - (via row 9) | adopt | S1, S50-S55 |
| 9 | SQLitePCLRaw.bundle_e_sqlite3 + bundled SQLite | bundle 2.1.12 (2026-07-14), SQLite 3.53.3 | Apache-2.0 (wrapper), public domain (SQLite) | yes | ok (one main maintainer, SourceGear LLC; consumed by Microsoft.Data.Sqlite) | no claim found | Y / Y / Y / Y; FTS5 documented and observed | adopt | S1, S56-S66 |
| 10 | sqlite-vec | v0.1.9 (2026-03-31); v0.1.10-alpha.4 prerelease | Apache-2.0 and MIT (two license files) | yes | flag: single maintainer; pre-v1 | n/a (native, loaded as extension) | Y / Y / Y / Y (GitHub release assets, no official NuGet) | adopt with fallback | S1, S67-S74 |
| 11 | Microsoft.ML.OnnxRuntime | 1.31.0 (2026-10-09) | MIT | yes | ok (Microsoft) | no claim for the managed part | Y / **N** / Y / Y | **pending** (osx-x64 gap; fallback: pin 1.23.x) | S1, S75-S81, S138 |
| 12 | Microsoft.ML.Tokenizers | 2.0.0 (2025-11-11); 3.0.0-preview.26457.2 | MIT | yes | ok (Microsoft/.NET) | no claim | - | adopt | S1, S82-S91 |
| 13 | ModelContextProtocol (+ .Core, .AspNetCore) | 2.2.0 (2026-08-13) | Apache-2.0 (package); repo notes a transition from MIT | yes | ok | claimed (`IsAotCompatible=true`); measured pass on `osx-arm64` (ADR 0006) | - | adopt | S1, S92-S96 |
| 14 | RazorSlices | 0.12.1 (2026-09-28) | MIT | yes | flag: single maintainer; pre-1.0 | claimed (README + `IsAotCompatible=true`) | - | not used (PRD Q1 changed to Svelte, 2026-10-17); verdict at the time of the check: adopt with fallback | S1, S97-S102 |
| 15 | htmx | v4.0.0 (2026-08-28) | 0BSD (LICENSE file; GitHub shows NOASSERTION) | yes | ok | n/a (JS) | - | not used (PRD Q1 changed to Svelte, 2026-10-17); verdict at the time: adopt | S1, S103-S106 |
| 16 | Alpine.js (optional) | v3.17.4 (2026-09-21) | MIT | yes | ok | n/a (JS) | - | not used (PRD Q1 changed to Svelte, 2026-10-17); verdict at the time: adopt | S1, S107-S110 |
| 17 | PDF.js | v6.4.299 (2026-10-03) | Apache-2.0 (PRD claim verified) | yes | ok (Mozilla) | n/a (JS) | - | adopt | S1, S111-S114 |
| 18 | foliate-js | no release, no tag; last commit 2026-05-01 | MIT (PRD claim verified) | yes | flag: no release, single maintainer; README says "not stable" | n/a (JS) | - | adopt with fallback | S1, S115-S121 |
| 19 | Reqnroll + Reqnroll.xunit.v3 (test-only) | 3.3.4 (2026-03-23) | BSD-3-Clause | n/a (not distributed) | ok | n/a | - | adopt | S1, S122-S124 |
| 20 | xunit.v3 (test-only) | 4.0.2 (2026-10-09) | Apache-2.0 | n/a (not distributed) | ok | n/a | - | adopt | S125-S127 |

### Rows 21-23 (named outside 10.2)

| # | Candidate | Revision (date) | License | Redistribution | Size | Verdict | Sources |
|---|---|---|---|---|---|---|---|
| 21 | multilingual-e5-small (`intfloat/multilingual-e5-small`) | `614241f6` (2026-04-02) | MIT (model card metadata) | MIT permits it; training-data terms unverified | ONNX 470,268,510 bytes | adopt (download at runtime, not mirrored) | S128-S131, S85 |
| 22 | Svelte | 5.57.2 (2026-10-06) | MIT | n/a (fallback only) | n/a | adopt (frontend since PRD Q1 of 2026-10-17; checked here as the fallback) | S1, S132-S135 |
| 23 | bge-m3 (`BAAI/bge-m3`) | `5617a9f6` (2024-07-03) | MIT (model card metadata) | MIT permits it; training-data terms unverified | ONNX external data 2,266,820,608 bytes | deferred, informational (license acceptable) | S136, S137 |

## Candidate notes

### 1. .NET 10 runtime + SDK (ASP.NET Core, Microsoft.Extensions.Logging, ILCompiler)

- Package/ID, repo: dotnet/runtime, dotnet/aspnetcore (S6, S7); `Microsoft.Extensions.Logging` 10.0.12 on NuGet (S9).
- Version: 10.0.12 (2026-09-08), LTS, support phase active; 11.0.0-rc.1 is the newest preview (S3).
- License: MIT (dotnet/runtime `LICENSE.TXT`, dotnet/aspnetcore `LICENSE.txt`, S6, S7). `Microsoft.Extensions.Logging` 10.0.12: MIT in nuspec (S9). Compatible: the FSF list names the Expat license ("MIT") as GPL-compatible (S1).
- Maintenance: Microsoft/.NET Foundation; monthly servicing releases (10.0.1 to 10.0.12 between 2025-12 and 2026-09, S4). LTS support from 2025-11-11 to **2028-11-14** (S4; `eol-date` in S3).
- AOT/trimming: ASP.NET Core docs list Minimal APIs as "partial support" and MVC as "not supported" for Native AOT (S8). `Microsoft.Extensions.Logging` project file shows no explicit `IsAotCompatible`: no documented claim found in the project file (S10); not further checked.
- Native assets: not applicable to the package; .NET 10 supported OS list includes macOS Arm64 and x64 and Linux Arm64 and x64 (S5). Native AOT cross-compilation and the `ILCompiler` runtime packs per RID: unverified (not checked here; input for the AOT spike).
- Risks: ASP.NET Core Native AOT is only partially supported (S8). Only Minimal APIs are in the PRD, which matches.
- Verdict: **adopt**.
- Sources: S1, S3, S4, S5, S6, S7, S8, S9, S10.

### 2. Spectre.Console.Cli (+ Spectre.Console)

- Package/ID, repo: `Spectre.Console.Cli`, https://github.com/spectreconsole/spectre.console.cli (S11). Depends on `Spectre.Console` 0.57.2 (S12), which depends on `Spectre.Console.Ansi` 0.57.2 (S13).
- Version: 0.57.2 (2026-09-29); prerelease 1.0.0-alpha.0.16 (S14). Pre-1.0.
- License: MIT in nuspec for all three packages (S11, S12, S13) and repo license MIT (S15). Compatible (S1).
- Maintenance: GitHub organization `spectreconsole`; 6 stable releases in the last 12 months (several published on 2026-09-29, apparently from the move of the CLI package into its own repository; this reading is an inference); 20 commits in the last 6 months; last push 2026-09-29; 55 open issues+PRs; top contributors patriksvensson, 0xced, FrankRay78 (S15, S16, S14).
- AOT/trimming: **`IsAotCompatible=false` and `IsTrimmable=false`** for the net8.0/net9.0/net10.0 targets in the project file at the release commit `9c88a343` (S17). No README or docs claim found. Value: `documented incompatible`.
- Native assets: none (managed).
- Risks: documented as not AOT-compatible; if chosen, this conflicts with the single-AOT-binary goal (PRD 9.2) and ReadyToRun would be the fallback.
- Verdict: **adopt** for license and maintenance. Selection: CLI framework decision work package.
- Sources: S1, S11-S17.

### 3. System.CommandLine

- Package/ID, repo: `System.CommandLine`, https://github.com/dotnet/command-line-api (S18; nuspec repository URL points to the dotnet/dotnet monorepo).
- Version: 2.0.12 stable (2026-09-08); prerelease 3.0.0-rc.1.26425.128 (2026-09-08) (S18, S19). Stable since 2.0.0 (2025-11-11, S20).
- License: MIT (nuspec S18; repo S21). Compatible (S1).
- Maintenance: Microsoft/.NET; 13 stable releases in the last 12 months (S19); last push 2026-10-12; 67 commits in the last 6 months; 485 open issues+PRs (S21).
- AOT/trimming: `IsTrimmable=true`, `EnableTrimAnalyzer=true`, `IsAotCompatible=true` for the minimum net TFM in the project file at `main` (S22). Value: `claimed` (checked at `main`, not at the 2.0.12 commit).
- Native assets: none.
- Risks: 3.0 is in preview; API change between 2.x and 3.x not assessed.
- Verdict: **adopt** for license and maintenance. Selection: CLI framework decision work package.
- Sources: S1, S18-S22.

### 4. VersOne.Epub

- Package/ID, repo: `VersOne.Epub`, https://github.com/vers-one/EpubReader (S23).
- Version: 3.3.6 (2026-03-15) (S24). No prerelease.
- License: nuspec `Unlicense` (S23), repo license Unlicense (S25). The FSF list says the Unlicense is a public domain dedication compatible with the GNU GPL (S1). Compatible.
- Maintenance: 2 stable releases in the last 12 months, last release 2026-03-15; last commit 2026-04-18 (177 days ago, under the 6-month limit); 6 commits in the last 6 months; 5 open issues+PRs; 193 commits by `vers-one`, next contributor 3 commits, no organization (S24, S25, S26). **Flag: single maintainer.**
- AOT/trimming: no claim in README or project file (targets `net46`, `netstandard1.3`, `netstandard2.0`) (S27).
- Native assets: none (managed).
- Risks: single-maintainer library on a path that is core to PRD F-IMP.
- Verdict: **adopt with fallback**: AOT failure falls back to ReadyToRun (PRD 9.2). Replacement library if abandoned: open.
- Sources: S1, S23-S27.

### 5. PdfPig (UglyToad.PdfPig)

- Package/ID, repo: NuGet ID `PdfPig` (namespace `UglyToad.PdfPig`), https://github.com/UglyToad/PdfPig (S28). Sub-packages are not required for text/metadata extraction (not checked further).
- Version: 0.1.16 (2026-08-22) (S29). Nightly `0.1.17-alpha-*` prereleases (latest 2026-10-12). Pre-1.0.
- License: Apache-2.0 (nuspec S28, repo S30). The FSF list says Apache 2.0 is compatible with GPL version 3 (S1).
- Maintenance: 5 stable releases in the last 12 months; last push 2026-10-12; 100+ commits in the last 6 months (API page limit); 30 open issues+PRs; two main maintainers (EliotJones 960, BobLd 589 commits), no organization (S29, S30, S31).
- AOT/trimming: no claim in project file at `a7bb3566` (S32). Corrected by [ADR 0005](0005-epub-pdf-parsers-aot.md) R3: `IsAotCompatible=true` via `src/Directory.Build.targets` for net8.0+ TFMs (claimed).
- Native assets: none (managed).
- Risks: pre-1.0 API; extraction quality on real books belongs to the PDF import slice.
- Verdict: **adopt**.
- Sources: S1, S28-S32.

### 6. PDFtoImage

- Package/ID, repo: `PDFtoImage`, https://github.com/sungaila/PDFtoImage (S33).
- Version: 5.4.0 (2026-08-16); prerelease 6.0.0-preview (2026-09-28) (S34).
- License: MIT (nuspec S33, repo S35). Compatible (S1).
- Dependencies (from nuspec, S33): `SkiaSharp` 4.150.1 and `SkiaSharp.NativeAssets.*` (MIT, S36), `bblanchon.PDFium.*` 152.0.7961 (Apache-2.0, S37) and `Sungaila.PDFium.BlazorWebAssembly`.
- Maintenance: 4 stable releases in the last 12 months; last push 2026-10-03; 15 commits in the last 6 months; 2 open issues+PRs; 10 contributors, one owner (S34, S35). **Flag: single maintainer.**
- AOT/trimming: `IsAotCompatible=true` for TFMs compatible with net7.0 in the project file at `5948628c` (S38). Value: `claimed`. README: PDFium is not thread-safe, calls are serialized within a process (S39).
- Native assets, per RID:
  - PDFium (`bblanchon.PDFium.macOS` 152.0.7961): `runtimes/osx-arm64/native/libpdfium.dylib` Y, `runtimes/osx-x64/native/libpdfium.dylib` Y (S40).
  - PDFium (`bblanchon.PDFium.Linux` 152.0.7961): `runtimes/linux-x64/native/libpdfium.so` Y, `runtimes/linux-arm64/native/libpdfium.so` Y (S41).
  - SkiaSharp (`SkiaSharp.NativeAssets.macOS` 4.150.1): `runtimes/osx/native/libSkiaSharp.dylib`, a universal binary with x86_64 and arm64 slices (S42, `file` output). `SkiaSharp.NativeAssets.Linux.NoDependencies` 4.150.1: `linux-x64` Y, `linux-arm64` Y (S43).
- Risks: pulls in SkiaSharp (a second native library and a large managed API) only to get a bitmap; single maintainer.
- Verdict: **adopt with fallback**. Replacement for the binding: open. AOT failure: ReadyToRun.
- Sources: S1, S33-S43.

### 7. PDFium (library and prebuilt binaries)

- Upstream: https://pdfium.googlesource.com/pdfium. Prebuilt binaries: https://github.com/bblanchon/pdfium-binaries, also published as `bblanchon.PDFium.*` NuGet packages (S37).
- Version: chromium/8086 = NuGet 157.0.8086 (2026-10-05); weekly releases (S44, S45). PDFtoImage 5.4.0 pins 152.0.7961.
- License: upstream `LICENSE` file holds a BSD-3-Clause text followed by an Apache-2.0 text (S46). Packager repository license: MIT (S47). NuGet metadata of `bblanchon.PDFium.Linux`/`.macOS`: `Apache-2.0` (S37). **Metadata and files differ across layers**; for the compiled binary the upstream BSD-3-Clause/Apache-2.0 terms apply and both are GPL-3.0-compatible (S1).
- Bundled third-party code: the release archive `pdfium-linux-x64.tgz` contains `licenses/` with abseil, agg23, dragonbox (Boost, Apache2-LLVM), fast_float, freetype, harfbuzz, icu, lcms, libjpeg_turbo (md and ijg), libopenjpeg, libpng, llvm-libc, simdutf, zlib, pdfium (S48). FSF list: FreeType license compatible with GPLv3, IJG compatible, zlib compatible, Boost compatible (S1). The others (abseil, harfbuzz, icu, lcms, openjpeg, libpng, simdutf, fast_float, llvm-libc) were **not individually assessed**; part of the transitive check after the AOT spike (Q10).
- Maintenance: packager `bblanchon` has 468 commits, next contributor 23; last push 2026-10-12; 50 NuGet releases in the last 12 months (S45, S49). **Flag: single maintainer (packager).** Upstream PDFium is a Google/Chromium project (organization-backed; not assessed further).
- AOT/trimming: n/a (native).
- Native assets: release assets `pdfium-linux-x64.tgz`, `pdfium-linux-arm64.tgz`, `pdfium-mac-x64.tgz`, `pdfium-mac-arm64.tgz` for chromium/8086 (S44); NuGet listing see row 6. All four RIDs Y.
- Risks: binary size (a `libpdfium.so` is 7-8 MB; `libpdfium.dylib` about 7 MB, S40, S41); dependence on one packager.
- Verdict: **adopt with fallback**: self-building PDFium is an option, not evaluated here (replacement: open).
- Sources: S1, S37, S40, S41, S44-S49.

### 8. Microsoft.Data.Sqlite

- Package/ID, repo: `Microsoft.Data.Sqlite` and `Microsoft.Data.Sqlite.Core`, source in https://github.com/dotnet/efcore (S50, S51).
- Version: 10.0.12 (2026-09-08); prerelease 11.0.0-rc.1.26425.128 (S52).
- License: MIT (nuspec S50; repo S53). Compatible (S1).
- Maintenance: Microsoft; 35 releases across servicing branches in the last 12 months; efcore last push 2026-10-12 (S53, S52).
- AOT/trimming: `IsAotCompatible=true` in `Microsoft.Data.Sqlite.Core.csproj` at `main` (S54); not checked at the 10.0.12 tag. Value: `claimed`.
- Native assets: none itself. Version 10.0.12 depends on `SQLitePCLRaw.bundle_e_sqlite3` 2.1.12 and `SQLitePCLRaw.core` 2.1.12 (S50). See row 9.
- Extension loading: `SqliteConnection.LoadExtension` exists since 3.0; native library lookup for extensions needs `PATH`/`LD_LIBRARY_PATH`/`DYLD_LIBRARY_PATH` handling (S55). Input for the sqlite-vec spike and the single-file binary.
- Verdict: **adopt**.
- Sources: S1, S50-S55.

### 9. SQLitePCLRaw bundle (`bundle_e_sqlite3`) + bundled SQLite

- Package/ID, repo: `SQLitePCLRaw.bundle_e_sqlite3` 2.1.12, depends on `SQLitePCLRaw.lib.e_sqlite3` 2.1.12 (native), `provider.*` (S56). https://github.com/ericsink/SQLitePCL.raw (S57).
- Version: 2.1.12 (2026-07-14) is what Microsoft.Data.Sqlite 10.0.12 pins. NuGet also has 2.1.13 (2026-08-13) and 3.0.5 (2026-07-27, S58). The 3.x line changes the native dependency to the packages `SQLite` 3.53.4 / `SourceGear.sqlite3` (S59, S57 releases). The repo's v2.1.12 release note says the bundle was updated to reference `lib.e_sqlite3` 3.53.3 while the 2.1.12 nuspec references `lib.e_sqlite3` 2.1.12: the two statements differ (S57, S56); the nuspec and the package contents are authoritative here.
- License: nuspec `Apache-2.0` for the bundle and `lib.e_sqlite3` 2.1.12 (S56, S60); repo README says Apache License v2 (S57). SQLite itself is public domain (S61). The 3.x `SQLite` package ships a `LICENSE.txt` saying "SQLite is Public Domain" (S59). The FSF list says public domain material is compatible with the GNU GPL and Apache 2.0 is compatible with GPLv3 (S1). Compatible.
- Bundled SQLite version: 3.53.3, from `strings` on the `lib.e_sqlite3` 2.1.12 binaries (static inspection of S62; not documented in the package metadata).
- FTS5: documented. Microsoft docs source lists `SQLitePCLRaw.bundle_e_sqlite3` as "Includes the FTS4, FTS5, JSON1, and R*Tree extensions" (S63); the upstream issue records FTS5 enabled since SQLitePCL.raw 1.1.10 (S64). Static check of the 2.1.12 binaries: the compile-option string `ENABLE_FTS5` is present in `linux-x64`, `linux-arm64`, `osx-arm64`, `osx-x64` (S62). The string `OMIT_LOAD_EXTENSION` was not found in the `linux-x64` binary (extension loading is therefore not compiled out; not exercised here).
- Maintenance: Eric Sink 1734 commits, next contributor 37; copyright holder SourceGear, LLC; last release 2026-08-13; last push 2026-08-13; 5 stable releases in the last 12 months; 39 open issues+PRs (S57, S58). Not flagged under the rule (a company is named as copyright holder), but effectively a one-person project (single maintainer, bus-factor 1; 1734 of the commits are by one contributor) that Microsoft.Data.Sqlite depends on, so a stalled maintainer would affect the whole data layer. The README says the builds on nuget.org "may not always contain the latest version of SQLite" (S65).
- AOT/trimming: no claim found in the README (S65).
- Native assets, `SQLitePCLRaw.lib.e_sqlite3` 2.1.12 (S62): `runtimes/osx-arm64/native/libe_sqlite3.dylib` Y, `runtimes/osx-x64/native/libe_sqlite3.dylib` Y, `runtimes/linux-x64/native/libe_sqlite3.so` Y, `runtimes/linux-arm64/native/libe_sqlite3.so` Y (plus many other RIDs). 3.x `SQLite` 3.53.4 package: same four RIDs Y (S59). FTS5 in the 3.x builds: unverified.
- Risks: lib size about 1.5-1.7 MB per RID (S62). Open upstream issue #682: bundled `libe_sqlite3` segfault on Linux when a system `libsqlite3` is loaded globally (S66); input for the AOT/packaging spike.
- Verdict: **adopt**.
- Sources: S1, S56-S66.

### 10. sqlite-vec

- Repo: https://github.com/asg017/sqlite-vec (S67).
- Version: v0.1.9 (2026-03-31) is the latest non-prerelease; v0.1.10-alpha.4 (2026-05-18) is the newest prerelease (S68). README: "`sqlite-vec` is a pre-v1, so expect breaking changes!" (S69).
- License: repo contains `LICENSE-APACHE` and `LICENSE-MIT` (S70); GitHub detects Apache-2.0 (S67). An explicit "either license" statement was not found: unverified. Both are GPL-3.0-compatible (S1). Compatible.
- Maintenance: last commit 2026-05-18 (147 days ago, under 6 months); 4 commits since 2026-04-12; 215 open issues+PRs; `asg017` 441 commits, next contributor 5; development sponsored by Mozilla Builders and others (named in README), no owning organization (S67, S69, S71). **Flag: single maintainer.** Stable releases in the last 12 months: v0.1.8, v0.1.9 (S68; older stable releases not listed in the first page).
- AOT/trimming: n/a (C extension loaded by SQLite).
- Distribution: GitHub release assets of v0.1.9: `sqlite-vec-0.1.9-loadable-macos-aarch64.tar.gz`, `-loadable-macos-x86_64.tar.gz`, `-loadable-linux-x86_64.tar.gz`, `-loadable-linux-aarch64.tar.gz`, static libraries for the same four, and an amalgamation (S72). No official NuGet package found in a NuGet search (S73). A third-party NuGet package `HiraokaHyperTools.sqlite-vec` 0.1.9 (nuspec license MIT, authors "kenjiuno, Alex Garcia") carries `vec0.so`/`vec0.dylib` for `linux-arm64`, `linux-x64`, `osx-arm64`, `osx-x64` (S74); it was not reviewed beyond its file list and is not a recommendation.
- Native assets: osx-arm64 Y, osx-x64 Y, linux-x64 Y, linux-arm64 Y (release assets, S72).
- Risks: pre-v1 API and on-disk format; single maintainer; binaries must be loaded as an extension (see row 8) or linked statically into the single-file binary (spike).
- Verdict: **adopt with fallback**: brute-force cosine in C# (PRD risk table 14).
- Sources: S1, S67-S74.

### 11. Microsoft.ML.OnnxRuntime

- Package/ID, repo: `Microsoft.ML.OnnxRuntime` 1.31.0 (depends on `Microsoft.ML.OnnxRuntime.Managed` 1.31.0), https://github.com/microsoft/onnxruntime (S75).
- Version: 1.31.0 (2026-10-09), monthly cadence (1.26.0 2026-05-08 to 1.31.0 2026-10-09, S76).
- License: MIT (nuspec points to `LICENSE` file in the package, which holds the MIT text; S75, S77). The package also ships `ThirdPartyNotices.txt` (344,457 bytes, S77). Compatible (S1).
- Maintenance: Microsoft; 14 stable releases in the last 12 months; last push 2026-10-12; 1774 open issues+PRs (S76, S78).
- AOT/trimming: no claim found in the project file of the managed package at `402979ac` (S79) or in the README.
- Native assets, `runtimes/<rid>/native/` in the 1.31.0 `.nupkg` (S77):
  - `osx-arm64`: `libonnxruntime.dylib` (44,830,288 bytes) Y
  - `osx-x64`: **absent**
  - `linux-x64`: `libonnxruntime.so` (29,345,384 bytes) + `libonnxruntime_providers_shared.so` Y
  - `linux-arm64`: `libonnxruntime.so` (25,266,408 bytes) + `libonnxruntime_providers_shared.so` Y
  - Also present: `win-x64`, `win-arm64`, `android`, `ios`.
- The 1.31.0 GitHub release assets contain `onnxruntime-osx-arm64-1.31.0.tgz` and no macOS x86_64 archive (S80). Upstream issue #27961 reports that `onnxruntime-node` stopped shipping macOS x86-64 binaries since 1.23.x, and PR #29502 (open) proposes restoring a macOS x86_64 packaging job (S81). These concern the npm package; the cause for the NuGet package is `unverified`. Older NuGet versions: the package entries of the NuGet catalog leaves (S138) list `runtimes/osx-x64/native/libonnxruntime.dylib` for 1.22.0, 1.22.1, 1.23.0, 1.23.1 and 1.23.2 (published 2025-05-09 to 2025-10-25), and only `osx-arm64` for every stable version from 1.24.1 (2026-02-05) through 1.31.0 (1.24.1 to 1.24.4, 1.25.0, 1.25.1, 1.26.0, 1.27.0, 1.27.1, 1.28.0, 1.29.0, 1.30.0, 1.31.0; checked in S138; the 1.31.0 listing is also S77). Versions before 1.22.0 were not checked. The asset therefore disappeared between 1.23.2 and 1.24.1, which matches the upstream issue "since 1.23.x" (S81).
- Risks: **no prebuilt `osx-x64` native in the current NuGet package**, but `osx-x64` is a PRD 9.2 target. Package size 159,626,085 bytes (S77).
- Verdict: **pending**. Deciding step: AOT/packaging spike plus a decision on osx-x64 (options to evaluate there, not decided here: (a) pin 1.23.x (1.23.2, 2025-10-25, is the last version with an osx-x64 asset per S138) as a fallback for the spike; trade-off: a superseded line (no newer 1.23 patch listed), and its AOT, maintenance and security-patch status is `unverified`; (b) self-built ONNX Runtime; (c) no semantic search on `osx-x64`).
- Sources: S1, S75-S81, S138.

### 12. Microsoft.ML.Tokenizers

- Package/ID, repo: `Microsoft.ML.Tokenizers`, https://github.com/dotnet/machinelearning (S82).
- Version: 2.0.0 stable (2025-11-11); prerelease 3.0.0-preview.26457.2 (2026-09-09) (S83).
- License: MIT (nuspec S82; repo S84). Compatible (S1).
- Maintenance: Microsoft/.NET; 2 stable releases in the last 12 months (2.0.0 and a 1.0.3 servicing release, S83); dotnet/machinelearning last push 2026-10-12 (S84). Last stable release is 11 months old: not flagged, but close to the 12-month limit.
- Tokenizer type needed: `multilingual-e5-small` uses `XLMRobertaTokenizer`; `tokenizer.json` has model type `Unigram` (250,002 vocab entries) and the repo ships `sentencepiece.bpe.model` (S85, S86).
- Documented support: `SentencePieceTokenizer` selects `SentencePieceUnigramModel` or `SentencePieceBpeModel` from the model file's `TrainerSpec.ModelType` (S87, at the 2.0.0 commit `efefa92f`). The repo's own `UnigramTests` load `sentencepiece.bpe.model` from `sentence-transformers/paraphrase-multilingual-MiniLM-L12-v2` (S88). That file has the **same SHA-256** (`cfc8146a...2865`, 5,069,051 bytes) as `onnx/sentencepiece.bpe.model` in `intfloat/multilingual-e5-small` (S85, S89). So the tokenizer type is covered by a fixture. Token-id parity against the Hugging Face tokenizer is not verified here (semantic slice).
- AOT/trimming: no `IsAotCompatible`/`IsTrimmable` in the project file at `efefa92f` (S90). Value: `no claim`.
- Native assets: none (managed). Its dependencies include `Google.Protobuf` 3.30.2 (BSD-3-Clause per nuspec, S91) and System.Text.Json; transitive check deferred (Q10).
- Verdict: **adopt**.
- Sources: S1, S82-S91.

### 13. ModelContextProtocol (official C# SDK)

- Package IDs: `ModelContextProtocol` 2.2.0 (core + hosting integration, depends on `ModelContextProtocol.Core` [2.2.0]); `ModelContextProtocol.Core` 2.2.0 (minimal dependencies); `ModelContextProtocol.AspNetCore` 2.2.0 (HTTP transport) (S92). Repo: https://github.com/modelcontextprotocol/csharp-sdk. Which of them is needed is a later decision: stdio only needs the first or second; an HTTP MCP endpoint needs the third.
- Version: 2.2.0 stable (2026-08-13); 2.0.0 stable 2026-07-28; no newer prerelease (S93).
- License: nuspec `Apache-2.0` (S92). The repo's `LICENSE` states that the project is "undergoing a licensing transition from the MIT License to the Apache License, Version 2.0"; contributions without relicensing consent remain MIT; documentation is CC-BY-4.0 (S94). GitHub reports NOASSERTION because of this. Apache-2.0 and MIT are both GPL-3.0-compatible and CC-BY-4.0 is compatible with all GPL versions (S1). Compatible.
- Maintenance: organization `modelcontextprotocol`; 9 stable releases in the last 12 months; 100+ commits in the last 6 months (API page limit); top contributors include `stephentoub` and `halter73` (S94, S95).
- AOT/trimming: `IsAotCompatible=true` in `ModelContextProtocol.Core`, `ModelContextProtocol` and `ModelContextProtocol.AspNetCore` project files at `6fa38259` (S96). Value: `claimed`. Measured by [ADR 0006](0006-mcp-sdk-aot.md): `ModelContextProtocol.Core` 2.2.0 publishes with 0 AOT/trim warnings and passes the stdio checks on `osx-arm64`.
- Native assets: none.
- Risks: major version 2.x was released 2026-07-28; check API stability when the MCP slice starts.
- Verdict: **adopt**.
- Sources: S1, S92-S96.

### 14. RazorSlices

- Package/ID, repo: `RazorSlices`, https://github.com/DamianEdwards/RazorSlices (S97).
- Version: 0.12.1 (2026-09-28); 8 stable releases in the last 12 months; pre-1.0 (S98).
- License: MIT (nuspec S97; repo S99). Compatible (S1).
- Maintenance: last push 2026-09-28; 19 commits in the last 6 months; 15 open issues+PRs; owner `DamianEdwards` 98 commits, no organization account (S99, S100). **Flag: single maintainer.**
- AOT/trimming: README: "Compatible with trimming and native AOT" and "Full support for trimming and native AOT when used in conjunction with ASP.NET Core Minimal APIs" (S101); `IsAotCompatible=true` at `552937e5` (S102). Value: `claimed`.
- Native assets: none.
- Risks: pre-1.0 API, single maintainer. The PRD's web frontend depends on it (PRD Q1).
- Verdict: **adopt with fallback**: Svelte (PRD Q1; adds a Node.js build toolchain).
- **Update 2026-10-17:** not used. The project owner chose Svelte as the frontend (PRD Q1), so the fallback became the plan. The facts above stay as checked.
- Sources: S1, S97-S102.

### 15. htmx

- Repo: https://github.com/bigskysoftware/htmx (S103).
- Version: v4.0.0 (2026-08-28) is the latest v4 stable; v4.0.0-beta6 is flagged prerelease (S104). The 2.x line is still maintained: v2.0.10 (2026-09-06), v2.0.9 (2026-04-20), v2.0.8 (published 2026-09-06; the GitHub release object was created 2025-10-25, so the publish date is much later than the creation date) (S104). The PRD does not pin a major version, so the choice between 2.x and 4.x is open (decision for the first web slice; not made here).
- License: `LICENSE` holds "Zero-Clause BSD" (0BSD) text (S105). GitHub reports NOASSERTION. The FSF list says the Zero-clause BSD license "can operate as a free software license compatible with the GNU GPL" and recommends keeping the notices (S1). Compatible.
- Maintenance: organization `bigskysoftware`; 98 commits in the last 6 months; last push 2026-10-02; 4 stable releases in the last 12 months (v4.0.0, v2.0.10, v2.0.9, v2.0.8). GitHub also lists v4.0.0-beta3 (2026-05-08), beta4 (2026-05-22) and beta5 (2026-06-26) without the prerelease flag; these are betas by name and are not counted (S103, S104, S106).
- AOT/trimming: n/a (JS asset, embedded).
- Native assets: none.
- Verdict: **adopt**.
- **Update 2026-10-17:** not used (PRD Q1: Svelte).
- Sources: S1, S103-S106.

### 16. Alpine.js (optional)

- Repo: https://github.com/alpinejs/alpine (S107).
- Version: v3.17.4 (2026-09-21); 21 stable releases in the last 12 months (S108).
- License: MIT (S109). Compatible (S1).
- Maintenance: last push 2026-10-05; 64 commits in the last 6 months; main contributor `calebporzio` 1317 commits, others 124/81/74 (S107, S110). Not flagged.
- AOT/trimming: n/a. Native: none.
- Verdict: **adopt**. Note: optional; the PRD allows "vanilla JS" instead.
- **Update 2026-10-17:** not used (PRD Q1: Svelte).
- Sources: S1, S107-S110.

### 17. PDF.js

- Repo: https://github.com/mozilla/pdf.js (S111).
- Version: v6.4.299 (2026-10-03); 12 stable releases in the last 12 months (S112).
- License: **Apache-2.0** (repo `LICENSE` file, S113). The PRD claim "PDF.js (Apache 2.0)" is correct. Compatible with GPLv3 (S1).
- Maintenance: Mozilla; last push 2026-10-12; 448 open issues+PRs; top contributors Snuffleupagus, timvandermeij, calixteman (S111, S114).
- AOT/trimming: n/a. Native: none.
- Risks: foliate-js vendors its own copy of PDF.js (row 18); the embedded asset must be one chosen copy.
- Verdict: **adopt**.
- Sources: S1, S111-S114.

### 18. foliate-js

- Repo: https://github.com/johnfactotum/foliate-js (S115).
- Version: **no releases and no tags** (S116). README: "This library itself is, however, *not* stable. Expect it to break and the API to change at any time." and, since there is no release, "it is recommended that you include the library as a git submodule" (S117).
- License: **MIT** (repo `LICENSE`, Copyright 2022 John Factotum, S118). The PRD claim "foliate-js (MIT)" is correct. Vendored libraries according to README: zip.js (BSD-3-Clause), fflate (MIT), PDF.js (Apache) (S117); the `vendor` directory holds `fflate.js`, `pdfjs`, `zip.js` (S119). All GPL-3.0-compatible (S1). Compatible.
- Maintenance: last commit 2026-05-01 (164 days ago, under the 6-month limit); 15 commits in the last 12 months, 1 in the last 6 months; 65 open issues+PRs; 14 contributors, `johnfactotum` 268 commits, next 12 (S115, S120, S121). **Flags: no release in 12 months (none ever); single maintainer.**
- AOT/trimming: n/a. Native: none.
- Risks: unstable API, no versioning; an upstream break cannot be detected by a version bump. PDF support in foliate-js is "highly experimental" (S117), so PDF reading should use PDF.js directly as the PRD says.
- Verdict: **adopt with fallback**: pin a commit and vendor it (the README's own recommendation). Replacement reader: open.
- Sources: S1, S115-S121.

### 19. Reqnroll + Reqnroll.xunit.v3 (test-only)

- Package/ID: `Reqnroll` 3.3.4, `Reqnroll.xunit.v3` 3.3.4 (2026-03-23) (S122). Repo: https://github.com/reqnroll/Reqnroll.
- License: BSD-3-Clause (nuspec S122; repo S123); FSF: modified BSD is GPL-compatible (S1). Not distributed.
- Maintenance: organization `reqnroll`; 7 stable releases in the last 12 months; last push 2026-08-26; top contributor `gasparnagy` 1383 commits (S123, S124).
- AOT/trimming: n/a (test project). Native: none.
- Verdict: **adopt**.
- Sources: S1, S122-S124.

### 20. xunit.v3 (test-only)

- Package/ID: `xunit.v3` 4.0.2 (2026-10-09) (S125). Repo: https://github.com/xunit/xunit.
- License: nuspec `Apache-2.0` (S125); repo `LICENSE` says "Unless otherwise noted, the source code here is covered by ... the Apache License, Version 2.0" (S126); GitHub reports NOASSERTION because of the preamble. Not distributed.
- Maintenance: 6 stable releases in the last 12 months; last push 2026-10-09; `bradwilson` 2889 commits (S125, S126, S127). One main maintainer under the .NET Foundation copyright; test-only, so not decision-relevant.
- AOT/trimming: n/a. Native: none.
- Verdict: **adopt**.
- Sources: S125-S127.

### 21. multilingual-e5-small

- Repo: https://huggingface.co/intfloat/multilingual-e5-small, revision `614241f622f53c4eeff9890bdc4f31cfecc418b3` (last modified 2026-04-02) (S128).
- License: model card metadata `license: mit` (S128, S129). This is a model license, not software; the FSF list is not applicable to it. The MIT text permits redistribution with the notice. The licenses of the training data are not stated in the card metadata checked here: `unverified`.
- ONNX: yes, in the official repo. `onnx/model.onnx` 470,268,510 bytes (about 449 MiB); `onnx/model_O4.onnx` 235,052,531; `onnx/model_qint8_avx512_vnni.onnx` 118,346,824 (the file name suggests an x86 AVX-512 VNNI target; applicability on arm64 is unverified) (S128).
- Tokenizer files: `onnx/sentencepiece.bpe.model` (5,069,051 bytes), `onnx/tokenizer.json` (17,082,730), `onnx/tokenizer_config.json`, `onnx/special_tokens_map.json`. `tokenizer_class` `XLMRobertaTokenizer`; `tokenizer.json` model type `Unigram`, Metaspace pre-tokenizer (S128, S130, S85).
- Dimension: 384 (`hidden_size`, `1_Pooling/config.json`, mean pooling); max sequence length: 512 (`sentence_bert_config.json`, `max_position_embeddings`); 12 layers (S130).
- Usage rule from the model card: input texts must start with `"query: "` or `"passage: "` (S131).
- Downloads: 10,213,009; likes: 454 (rough signal, S128).
- Runtime download vs mirroring: shelf downloads the model at runtime from the user's machine and does not ship or mirror it, so the license binds the user and shelf's distribution carries no model files. **Mirroring the model (for example in GitHub Releases for an offline install) would change this assessment**: shelf would then redistribute the files (MIT notice required) and the training-data terms would need checking.
- Verdict: **adopt**.
- Sources: S128-S131, S85.

### 22. Svelte

- Repo: https://github.com/sveltejs/svelte (S132).
- Version: 5.57.2 (2026-10-06); 110 stable releases in the last 12 months (S133).
- License: MIT (S132, S134). Compatible (S1).
- Maintenance: organization `sveltejs`; last push 2026-10-12; 1142 open issues+PRs; top contributors Rich-Harris 5250, dummdidumm 815 (S132, S135). Not flagged.
- Toolchain: PRD Q1 states that Svelte brings a Node.js build toolchain; this was not verified separately here.
- Verdict: **adopt**.
- **Update 2026-10-17:** Svelte is the frontend (PRD Q1: Svelte 5 + Vite + TypeScript, single-page application). **Not checked with this ADR's method:** Vite, TypeScript and Node.js (build-time tools), and the npm dependency tree of the UI. That check was done in [ADR 0009](0009-frontend-toolchain-licenses-and-maintenance.md) (2026-10-17).
- Sources: S1, S132-S135.

### 23. bge-m3 (deferred, informational)

- Repo: https://huggingface.co/BAAI/bge-m3, revision `5617a9f61b028005a4858fdac845db406aefb181` (last modified 2024-07-03) (S136).
- License: model card metadata `license: mit` (S136). Redistribution and training-data caveats as in row 21.
- Size: `onnx/model.onnx` 724,923 bytes plus external data `onnx/model.onnx_data` 2,266,820,608 bytes (about 2.1 GiB); `pytorch_model.bin` 2,271,145,830 bytes (S136). About 5 times the e5-small ONNX file.
- Shape: `XLMRobertaModel`, `hidden_size` 1024, `max_position_embeddings` 8194, 24 layers (S137). Embedding dimension 1024 differs from the 384 of the default model (relevant for sqlite-vec table definitions, PRD F-SEM-8).
- Verdict: **deferred, informational**. License acceptable under the policy.
- Sources: S136, S137.

## Decision

- **Adopted:** .NET 10 (1), Spectre.Console.Cli (2) and System.CommandLine (3) (license and maintenance; selection: CLI framework decision (Spectre documents `IsAotCompatible=false`, System.CommandLine `=true`)), PdfPig (5), Microsoft.Data.Sqlite (8), SQLitePCLRaw bundle_e_sqlite3 with bundled SQLite (9), Microsoft.ML.Tokenizers (12), ModelContextProtocol (13), PDF.js (17), Svelte (22; frontend since 2026-10-17), Reqnroll (19), xunit.v3 (20), multilingual-e5-small downloaded at runtime (21).
- **Adopted with fallback:**
  - VersOne.Epub (4): single maintainer; ReadyToRun if AOT fails; replacement open.
  - PDFtoImage and PDFium (6, 7): single maintainer (binding and packager); replacement open; ReadyToRun if AOT fails.
  - sqlite-vec (10): brute-force cosine in C# (PRD risk table).
  - foliate-js (18): pin a commit and vendor it; replacement open.
- **Pending:**
  - Microsoft.ML.OnnxRuntime (11): no prebuilt `osx-x64` native in 1.31.0 although `osx-x64` is a PRD 9.2 target. Deciding step: AOT/packaging spike and a project-owner decision on `osx-x64`.
- **Not used since 2026-10-17:** RazorSlices (14), htmx (15) and Alpine.js (16): PRD Q1 now chooses Svelte (22).
- **Deferred:** bge-m3 (23) is deferred and informational.
- **Rejected:** none. No candidate failed the license policy and none is abandoned.

## Consequences

**Inputs for the AOT spike**

- Documented not AOT-compatible: Spectre.Console.Cli (row 2). No documented claim: VersOne.Epub, PdfPig, Microsoft.ML.Tokenizers, Microsoft.ML.OnnxRuntime (managed part), SQLitePCLRaw (rows 4, 5, 12, 11, 9). Claimed in the project file: System.CommandLine, Microsoft.Data.Sqlite.Core, PDFtoImage, ModelContextProtocol (core, hosting, AspNetCore), RazorSlices (rows 3, 8, 6, 13, 14; RazorSlices is no longer used). All claims are unverified until the spike runs. Corrected by ADR 0005 R3: PdfPig does claim `IsAotCompatible=true` via `src/Directory.Build.targets` for net8.0+ TFMs.
- RIDs without a prebuilt native asset: `osx-x64` for ONNX Runtime (row 11). All other native components have assets for all four RIDs: SQLite (row 9), PDFium and SkiaSharp (rows 6, 7), sqlite-vec (row 10, as release assets and not as an official NuGet package).
- Packaging questions that belong in the spike: sqlite-vec as a loadable extension in a single-file AOT binary (extension lookup uses OS library paths, S55); the 3.x vs 2.1.x SQLitePCLRaw lines (row 9); SkiaSharp being pulled in by PDFtoImage (row 6); FTS5 in the 3.x SQLite builds is unverified.

**Follow-ups that need other files (listed, not done in this slice)**

- Roadmap: tick the M0 work package "License and maintenance check" once this ADR is merged.
- PRD 10.2: optional license column. No PRD license claim was wrong: PDF.js is Apache-2.0 and foliate-js is MIT.
- PRD vs roadmap inconsistency: PRD 13.1 Q1 and the risk table (chapter 14) say the AOT spike happens "in M1", while the roadmap puts it in M0. One of them should be corrected.
- PRD 9.2: the `osx-x64` target and ONNX Runtime (row 11) need a decision.
- `THIRD-PARTY-NOTICES`: MIT, BSD, Apache-2.0 and 0BSD (keep notices, S1) all require or recommend preserving notices when redistributing binaries and embedded JS. A notices file and the build step that produces it is a later task (release pipeline). The ONNX Runtime package ships a `ThirdPartyNotices.txt` and the PDFium archive a `licenses/` directory; both are inputs.
- SPDX header / `PackageLicenseExpression` for shelf itself (`GPL-3.0-or-later`): separate slice.
- Transitive dependency license check once the dependency graph exists (after the AOT spike), including the bundled PDFium third-party libraries (row 7) and `Google.Protobuf` (row 12).
- TOML parser, ULID, stemming and trigram candidates are not covered here; the TOML parser is decided together with the CLI framework.

**Re-check trigger:** re-run this check for a candidate when its major version changes, when a native package is swapped, and before the 1.0 release. Notable current moving targets: htmx 4.x (new major), ModelContextProtocol 2.x (new major, license transition from MIT to Apache-2.0), System.CommandLine 3.0 (preview), Microsoft.Data.Sqlite 11.0 (preview), SQLitePCLRaw 3.x.

## Source index

All sources retrieved read-only on 2026-10-12. `nuspec(id, version)` is `https://api.nuget.org/v3-flatcontainer/<id>/<version>/<id>.nuspec` (lower-case id); `catalog(id)` is `https://api.nuget.org/v3/registration5-gz-semver2/<id>/index.json`; `nupkg(id, version)` is `https://api.nuget.org/v3-flatcontainer/<id>/<version>/<id>.<version>.nupkg` (listed with `unzip -l`, `file`, `strings`, never executed). `gh:` means `https://api.github.com/<path>`. Hugging Face paths are `https://huggingface.co/<repo>/resolve/<revision>/<file>`.

| ID | Source |
|---|---|
| S1 | FSF license list https://www.gnu.org/licenses/license-list.html |
| S2 | (not used; the SQLite public-domain statement is S61) |
| S3 | https://raw.githubusercontent.com/dotnet/core/main/release-notes/releases-index.json |
| S4 | https://raw.githubusercontent.com/dotnet/core/main/release-notes/10.0/README.md |
| S5 | https://raw.githubusercontent.com/dotnet/core/main/release-notes/10.0/supported-os.md |
| S6 | https://raw.githubusercontent.com/dotnet/runtime/main/LICENSE.TXT |
| S7 | https://raw.githubusercontent.com/dotnet/aspnetcore/main/LICENSE.txt |
| S8 | https://github.com/dotnet/AspNetCore.Docs/blob/main/aspnetcore/fundamentals/native-aot.md (ms.date 06/02/2026) |
| S9 | nuspec(microsoft.extensions.logging, 10.0.12) |
| S10 | https://github.com/dotnet/runtime/blob/main/src/libraries/Microsoft.Extensions.Logging/src/Microsoft.Extensions.Logging.csproj |
| S11 | nuspec(spectre.console.cli, 0.57.2) |
| S12 | nuspec(spectre.console, 0.57.2) |
| S13 | nuspec(spectre.console.ansi, 0.57.2) |
| S14 | catalog(spectre.console.cli) |
| S15 | gh:repos/spectreconsole/spectre.console.cli and gh:repos/spectreconsole/spectre.console |
| S16 | gh:repos/spectreconsole/spectre.console.cli/contributors and /commits?since=2026-04-12 |
| S17 | https://github.com/spectreconsole/spectre.console.cli/blob/9c88a343b8ce622e9fda4e4334b2057d773d90c9/src/Spectre.Console.Cli/Spectre.Console.Cli.csproj |
| S18 | nuspec(system.commandline, 2.0.12) |
| S19 | catalog(system.commandline) |
| S20 | gh:repos/dotnet/command-line-api/releases |
| S21 | gh:repos/dotnet/command-line-api, /commits?since=2026-04-12 |
| S22 | https://github.com/dotnet/command-line-api/blob/main/src/System.CommandLine/System.CommandLine.csproj |
| S23 | nuspec(versone.epub, 3.3.6) |
| S24 | catalog(versone.epub) |
| S25 | gh:repos/vers-one/EpubReader, /contributors, /license |
| S26 | gh:repos/vers-one/EpubReader/commits?since=2026-04-12 |
| S27 | https://github.com/vers-one/EpubReader/blob/master/Source/VersOne.Epub/VersOne.Epub.csproj and its README (no AOT/trimming mention) |
| S28 | nuspec(pdfpig, 0.1.16) |
| S29 | catalog(pdfpig) |
| S30 | gh:repos/UglyToad/PdfPig |
| S31 | gh:repos/UglyToad/PdfPig/contributors, /commits?since=2026-04-12 |
| S32 | https://github.com/UglyToad/PdfPig/blob/a7bb35662bbbf405efddad50aedc9bcdcf515afc/src/UglyToad.PdfPig/UglyToad.PdfPig.csproj |
| S33 | nuspec(pdftoimage, 5.4.0) |
| S34 | catalog(pdftoimage); gh:repos/sungaila/PDFtoImage/releases |
| S35 | gh:repos/sungaila/PDFtoImage, /contributors, /commits?since=2026-04-12 |
| S36 | nuspec(skiasharp, 4.150.1), nuspec(skiasharp.nativeassets.macos, 4.150.1), nuspec(skiasharp.nativeassets.linux.nodependencies, 4.150.1) |
| S37 | nuspec(bblanchon.pdfium.linux, 152.0.7961 and 157.0.8086), nuspec(bblanchon.pdfium.macos, 157.0.8086) |
| S38 | https://github.com/sungaila/PDFtoImage/blob/5948628cb2cf7d070c7fd28b1b4496db39e3afcd/src/PDFtoImage/PDFtoImage.csproj |
| S39 | https://github.com/sungaila/PDFtoImage/blob/master/README.md |
| S40 | nupkg(bblanchon.pdfium.macos, 152.0.7961) file listing |
| S41 | nupkg(bblanchon.pdfium.linux, 152.0.7961) file listing |
| S42 | nupkg(skiasharp.nativeassets.macos, 4.150.1) file listing and `file` on `runtimes/osx/native/libSkiaSharp.dylib` |
| S43 | nupkg(skiasharp.nativeassets.linux.nodependencies, 4.150.1) file listing |
| S44 | https://github.com/bblanchon/pdfium-binaries/releases/tag/chromium%2F8086 (asset names via gh:repos/bblanchon/pdfium-binaries/releases/latest) |
| S45 | gh:repos/bblanchon/pdfium-binaries, /releases, /commits?since=2026-04-12; catalog(bblanchon.pdfium.linux) |
| S46 | https://pdfium.googlesource.com/pdfium/+/refs/heads/main/LICENSE |
| S47 | gh:repos/bblanchon/pdfium-binaries/license |
| S48 | https://github.com/bblanchon/pdfium-binaries/releases/download/chromium%2F8086/pdfium-linux-x64.tgz (archive listing only) |
| S49 | gh:repos/bblanchon/pdfium-binaries/contributors |
| S50 | nuspec(microsoft.data.sqlite, 10.0.12) |
| S51 | nuspec(microsoft.data.sqlite.core, 10.0.12) |
| S52 | catalog(microsoft.data.sqlite) |
| S53 | gh:repos/dotnet/efcore |
| S54 | https://github.com/dotnet/efcore/blob/main/src/Microsoft.Data.Sqlite.Core/Microsoft.Data.Sqlite.Core.csproj |
| S55 | https://github.com/dotnet/docs/blob/main/docs/standard/data/sqlite/extensions.md |
| S56 | nuspec(sqlitepclraw.bundle_e_sqlite3, 2.1.12) and nuspec(sqlitepclraw.bundle_e_sqlite3, 3.0.5) |
| S57 | https://github.com/ericsink/SQLitePCL.raw (README; gh:repos/ericsink/SQLitePCL.raw, /releases, /contributors, /commits) |
| S58 | catalog(sqlitepclraw.bundle_e_sqlite3) |
| S59 | nuspec(sqlite, 3.53.4) and nupkg(sqlite, 3.53.4) file listing incl. `LICENSE.txt` |
| S60 | nuspec(sqlitepclraw.lib.e_sqlite3, 2.1.12) |
| S61 | https://sqlite.org/copyright.html |
| S62 | nupkg(sqlitepclraw.lib.e_sqlite3, 2.1.12) file listing; `strings` on `runtimes/{linux-x64,linux-arm64,osx-arm64,osx-x64}/native/libe_sqlite3.*` |
| S63 | https://github.com/dotnet/docs/blob/main/docs/standard/data/sqlite/custom-versions.md |
| S64 | https://github.com/ericsink/SQLitePCL.raw/issues/171 |
| S65 | https://github.com/ericsink/SQLitePCL.raw/blob/main/README.md |
| S66 | https://github.com/ericsink/SQLitePCL.raw/issues/682 |
| S67 | gh:repos/asg017/sqlite-vec, /commits, /contributors |
| S68 | gh:repos/asg017/sqlite-vec/releases |
| S69 | https://github.com/asg017/sqlite-vec/blob/main/README.md |
| S70 | https://github.com/asg017/sqlite-vec/blob/main/LICENSE-APACHE and https://github.com/asg017/sqlite-vec/blob/main/LICENSE-MIT |
| S71 | gh:repos/asg017/sqlite-vec/commits?since=2026-04-12 |
| S72 | gh:repos/asg017/sqlite-vec/releases/latest (v0.1.9 asset list) |
| S73 | https://azuresearch-usnc.nuget.org/query?q=sqlite-vec&prerelease=true |
| S74 | nuspec(hiraokahypertools.sqlite-vec, 0.1.9) and nupkg file listing |
| S75 | nuspec(microsoft.ml.onnxruntime, 1.31.0) |
| S76 | catalog(microsoft.ml.onnxruntime) |
| S77 | nupkg(microsoft.ml.onnxruntime, 1.31.0) file listing, `LICENSE`, `ThirdPartyNotices.txt` |
| S78 | gh:repos/microsoft/onnxruntime |
| S79 | https://github.com/microsoft/onnxruntime/blob/402979ac1bef89dcd55add41eaa7277c407599eb/csharp/src/Microsoft.ML.OnnxRuntime/Microsoft.ML.OnnxRuntime.csproj |
| S80 | gh:repos/microsoft/onnxruntime/releases/tags/v1.31.0 (asset names) |
| S81 | https://github.com/microsoft/onnxruntime/issues/27961 and https://github.com/microsoft/onnxruntime/pull/29502 |
| S82 | nuspec(microsoft.ml.tokenizers, 2.0.0) |
| S83 | catalog(microsoft.ml.tokenizers) |
| S84 | gh:repos/dotnet/machinelearning |
| S85 | https://huggingface.co/api/models/intfloat/multilingual-e5-small?blobs=true (file list with SHA-256) and `tokenizer.json` at revision 614241f6 |
| S86 | https://huggingface.co/intfloat/multilingual-e5-small/resolve/614241f622f53c4eeff9890bdc4f31cfecc418b3/tokenizer_config.json |
| S87 | https://github.com/dotnet/machinelearning/blob/efefa92f4486a43047c5b47618885a71bf7f0967/src/Microsoft.ML.Tokenizers/Model/SentencePieceTokenizer.cs |
| S88 | https://github.com/dotnet/machinelearning/blob/efefa92f4486a43047c5b47618885a71bf7f0967/test/Microsoft.ML.Tokenizers.Tests/UnigramTests.cs |
| S89 | https://huggingface.co/api/models/sentence-transformers/paraphrase-multilingual-MiniLM-L12-v2?blobs=true |
| S90 | https://github.com/dotnet/machinelearning/blob/efefa92f4486a43047c5b47618885a71bf7f0967/src/Microsoft.ML.Tokenizers/Microsoft.ML.Tokenizers.csproj |
| S91 | nuspec(google.protobuf, 3.30.2) |
| S92 | nuspec(modelcontextprotocol, 2.2.0), nuspec(modelcontextprotocol.core, 2.2.0), nuspec(modelcontextprotocol.aspnetcore, 2.2.0) |
| S93 | catalog(modelcontextprotocol) |
| S94 | https://github.com/modelcontextprotocol/csharp-sdk/blob/main/LICENSE; gh:repos/modelcontextprotocol/csharp-sdk |
| S95 | gh:repos/modelcontextprotocol/csharp-sdk/contributors, /commits?since=2026-04-12 |
| S96 | project files at `6fa3825973949a9c4f0cd8af344e15a8db09dc35`: `src/ModelContextProtocol.Core/ModelContextProtocol.Core.csproj`, `src/ModelContextProtocol/ModelContextProtocol.csproj`, `src/ModelContextProtocol.AspNetCore/ModelContextProtocol.AspNetCore.csproj` |
| S97 | nuspec(razorslices, 0.12.1) |
| S98 | catalog(razorslices) |
| S99 | gh:repos/DamianEdwards/RazorSlices |
| S100 | gh:repos/DamianEdwards/RazorSlices/contributors, /commits?since=2026-04-12 |
| S101 | https://github.com/DamianEdwards/RazorSlices/blob/main/README.md |
| S102 | https://github.com/DamianEdwards/RazorSlices/blob/552937e5f2f21ebcdc73939be014f3d217ffbb22/src/RazorSlices/RazorSlices.csproj |
| S103 | gh:repos/bigskysoftware/htmx |
| S104 | gh:repos/bigskysoftware/htmx/releases |
| S105 | https://github.com/bigskysoftware/htmx/blob/master/LICENSE |
| S106 | gh:repos/bigskysoftware/htmx/contributors, /commits?since=2026-04-12 |
| S107 | gh:repos/alpinejs/alpine |
| S108 | gh:repos/alpinejs/alpine/releases |
| S109 | https://github.com/alpinejs/alpine/blob/main/LICENSE.md |
| S110 | gh:repos/alpinejs/alpine/contributors, /commits?since=2026-04-12 |
| S111 | gh:repos/mozilla/pdf.js |
| S112 | gh:repos/mozilla/pdf.js/releases |
| S113 | https://github.com/mozilla/pdf.js/blob/master/LICENSE |
| S114 | gh:repos/mozilla/pdf.js/contributors |
| S115 | gh:repos/johnfactotum/foliate-js |
| S116 | gh:repos/johnfactotum/foliate-js/releases and /tags (both empty) |
| S117 | https://github.com/johnfactotum/foliate-js/blob/main/README.md |
| S118 | https://github.com/johnfactotum/foliate-js/blob/main/LICENSE |
| S119 | gh:repos/johnfactotum/foliate-js/contents/vendor |
| S120 | gh:repos/johnfactotum/foliate-js/commits |
| S121 | gh:repos/johnfactotum/foliate-js/contributors |
| S122 | nuspec(reqnroll, 3.3.4), nuspec(reqnroll.xunit.v3, 3.3.4), catalog(reqnroll) |
| S123 | gh:repos/reqnroll/Reqnroll |
| S124 | gh:repos/reqnroll/Reqnroll/contributors |
| S125 | nuspec(xunit.v3, 4.0.2), catalog(xunit.v3) |
| S126 | https://github.com/xunit/xunit/blob/main/LICENSE; gh:repos/xunit/xunit |
| S127 | gh:repos/xunit/xunit/contributors |
| S128 | https://huggingface.co/api/models/intfloat/multilingual-e5-small?blobs=true |
| S129 | https://huggingface.co/intfloat/multilingual-e5-small/blob/main/README.md (YAML header `license: mit`) |
| S130 | `config.json`, `sentence_bert_config.json`, `1_Pooling/config.json`, `onnx/config.json` of https://huggingface.co/intfloat/multilingual-e5-small at revision 614241f6 |
| S131 | https://huggingface.co/intfloat/multilingual-e5-small/blob/main/README.md ("Each input text should start with ...") |
| S132 | gh:repos/sveltejs/svelte |
| S133 | gh:repos/sveltejs/svelte/releases |
| S134 | https://github.com/sveltejs/svelte/blob/main/LICENSE.md |
| S135 | gh:repos/sveltejs/svelte/contributors |
| S136 | https://huggingface.co/api/models/BAAI/bge-m3?blobs=true |
| S137 | https://huggingface.co/BAAI/bge-m3/resolve/5617a9f61b028005a4858fdac845db406aefb181/config.json |
| S138 | catalog(microsoft.ml.onnxruntime) leaf entries (`catalogEntry` pages, field `packageEntries`) for versions 1.22.0-1.31.0 (stable versions), read via `https://api.nuget.org/v3/registration5-gz-semver2/microsoft.ml.onnxruntime/index.json`; no nupkg downloaded |
