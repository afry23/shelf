# Slice m0-license-adr: License and Maintenance Check of Dependency Candidates (ADR)

| | |
|---|---|
| **Slice ID** | `m0-license-adr` |
| **Branch** | `docs/m0-license-adr` |
| **Milestone** | M0 – Setup & Spikes |
| **Roadmap work package** | "License and maintenance check": "All candidates from 10.2; result documented as an ADR" (roadmap 3, M0) |
| **PRD references** | 10.2 (technology candidates), plus the candidates named in 6.7 / Q3 (embedding model), Q1 (Svelte fallback), 14 (risk table), 9.2 (four target RIDs) |

---

## 1. Scope

**In scope**

- One new file: `docs/adr/0001-dependency-licenses-and-maintenance.md`. Nothing else in the repo changes.
- For every candidate in section 1.1: license, compatibility with shelf's license, maintenance status, documented AOT/trimming claim, native assets per target RID, verdict.
- Research only. Every fact carries a source URL and a retrieval date.

**shelf's own license:** `LICENSE` contains the text of the **GNU GPL v3** (29 June 2007). The repo does not say whether this means "GPL-3.0-only" or "GPL-3.0-or-later": there is no SPDX header and no `PackageLicenseExpression`. The ADR writes `GPL-3.0 (only/or-later undetermined, see Q2)`.

**Out of scope**

- Choosing between alternatives where a separate M0 work package decides. The ADR states whether **each** option is license- and maintenance-acceptable, nothing more.
  - CLI framework (Spectre.Console.Cli vs. System.CommandLine): decided in the "CLI framework decision" work package.
  - Actual AOT compatibility: decided by the "AOT spike". The ADR records only what the project **documents** about AOT/trimming. It must not write "AOT-compatible" as a verified fact.
- Picking candidates the PRD does not name (TOML parser, ULID library, stemming library, trigram implementation). See Q5.
- Adding packages to `Directory.Packages.props` or any `.csproj`.
- Editing `docs/prd.md`, `docs/roadmap.md`, `CLAUDE.md`, or `LICENSE`. Needed follow-ups go into the ADR's "Consequences" section and the PR description (see Q6).
- A `THIRD-PARTY-NOTICES` file or attribution mechanism for releases (see Q9).
- Legal advice. The ADR is an engineering assessment based on the FSF license list. It is not a legal opinion, and it says so.

### 1.1 Candidate list (the implementer works from this list)

**A. PRD 10.2 table, quoted verbatim.** All rows are mandatory.

| Area | Candidate |
|---|---|
| Runtime | .NET 10 (LTS) |
| CLI | Spectre.Console.Cli or System.CommandLine |
| EPUB | VersOne.Epub |
| PDF text/metadata | UglyToad.PdfPig |
| PDF cover rendering | PDFium binding (e.g. PDFtoImage) |
| Database | Microsoft.Data.Sqlite + FTS5 |
| Vector search | sqlite-vec (native extension, shipped per platform) |
| Embeddings | Microsoft.ML.OnnxRuntime + tokenizer (Microsoft.ML.Tokenizers) |
| MCP | Official ModelContextProtocol C# SDK |
| Web backend | ASP.NET Core Minimal APIs |
| Web frontend | htmx + RazorSlices (decision Q1); small interactions via vanilla JS or Alpine.js |
| PDF reader | PDF.js (Apache 2.0) |
| EPUB reader | foliate-js (MIT) |
| Logging | Microsoft.Extensions.Logging, output to stderr (important for MCP stdio) |
| BDD | Reqnroll (Gherkin feature files in English; process rules in `CLAUDE.md`) |

**B. Expanded into individually assessed rows.** One ADR row per item:

| # | Row | What exactly to check | Kind |
|---|---|---|---|
| 1 | .NET 10 runtime + SDK (incl. ASP.NET Core shared framework, Microsoft.Extensions.Logging, Native AOT ILCompiler) | License of dotnet/runtime and dotnet/aspnetcore; LTS end-of-support date from the Microsoft lifecycle page | Runtime |
| 2 | Spectre.Console.Cli | NuGet package; also `Spectre.Console` if it is a dependency | NuGet |
| 3 | System.CommandLine | NuGet package; stable vs. prerelease status | NuGet |
| 4 | VersOne.Epub | NuGet package | NuGet |
| 5 | UglyToad.PdfPig | NuGet package (and its sub-packages if they are pulled in separately) | NuGet |
| 6 | PDFtoImage | NuGet package **and** the PDFium native binary packages it depends on (record their package IDs and source project) | NuGet + native |
| 7 | PDFium (the library itself) | Upstream license of PDFium and of the prebuilt-binary project used by row 6 | Native |
| 8 | Microsoft.Data.Sqlite | NuGet package | NuGet |
| 9 | SQLitePCLRaw bundle used by Microsoft.Data.Sqlite (record the exact bundle ID, e.g. `bundle_e_sqlite3`) + the bundled SQLite | License of SQLitePCLRaw and SQLite; whether the bundled build has **FTS5** enabled (documented source required); native RIDs shipped | NuGet + native |
| 10 | sqlite-vec | Upstream license; release status (version, pre-1.0?); how prebuilt binaries are distributed (GitHub release assets, NuGet package, other); which of the 4 RIDs have prebuilt binaries | Native |
| 11 | Microsoft.ML.OnnxRuntime | NuGet package; native RIDs shipped (check **each** of the 4 RIDs explicitly, do not assume `osx-x64`) | NuGet + native |
| 12 | Microsoft.ML.Tokenizers | NuGet package; whether it documents support for the tokenizer type used by multilingual-e5-small (record the tokenizer type from the model's own files) | NuGet |
| 13 | ModelContextProtocol (official C# SDK) | NuGet package ID(s) actually needed (core and/or ASP.NET Core variant); stable vs. prerelease | NuGet |
| 14 | RazorSlices | NuGet package | NuGet |
| 15 | htmx | Upstream license and release | JS asset |
| 16 | Alpine.js | Upstream license and release (optional; "or vanilla JS") | JS asset |
| 17 | PDF.js | Upstream license; verify the PRD's "Apache 2.0" claim | JS asset |
| 18 | foliate-js | Upstream license; verify the PRD's "MIT" claim; whether it publishes releases at all | JS asset |
| 19 | Reqnroll + `Reqnroll.xunit.v3` (in use, 3.3.4) | Test-only, not distributed | NuGet (test) |
| 20 | xunit.v3 (in use, 4.0.2) | Test-only, not distributed | NuGet (test) |

**C. Candidates named outside 10.2.** Also mandatory, in a separate ADR table:

| # | Row | Source in PRD | What to check |
|---|---|---|---|
| 21 | multilingual-e5-small (Hugging Face `intfloat/multilingual-e5-small`) | F-SEM-1, Q3 | Model license (from the model card / repo metadata), whether an ONNX export is published in the official repo (path, file size), tokenizer files present, embedding dimension, max sequence length |
| 22 | Svelte | Q1 (fallback for RazorSlices) | License, maintenance. Note that it brings a Node.js build toolchain (PRD Q1 avoids this) |
| 23 | bge-m3 (`BAAI/bge-m3`) | Q3, roadmap backlog | Model license and size only. Marked "deferred, informational" |

For rows 19–20 (test-only) and 23 (deferred), a short assessment is enough: license and last release. Compatibility with distribution is "n/a (not distributed)" for 19–20.

## 2. Feature Files

**No feature file is added in this slice.** The implementer must not add one, and must not add step definitions.

Justification under CLAUDE.md rule 1: rule 1 requires feature files before implementing **behavior**. This slice adds an ADR and no code, so there is no product behavior to specify. Tagging rule 4 (`@F-…`, `@M…`) does not apply because no scenario is created.

**Quality gate for this slice:**

1. The diff contains exactly one added file, `docs/adr/0001-dependency-licenses-and-maintenance.md` (`git diff --stat main...HEAD`).
2. Run locally at the repo root, all unchanged and green: `dotnet format --verify-no-changes`, `dotnet build -c Release`, `dotnet test --no-build -c Release`.
3. Every row in the ADR summary table has a verdict, and every factual cell has either a source reference or the word `unverified`.
4. Note: the CI workflow ignores `docs/**` and `**/*.md` (`paths-ignore`), so this PR triggers **no** CI run. If a status check is required on `main`, the PR will wait for it (see Q8). Do not change the workflow in this slice.

## 3. Design

### 3.1 Files

```
docs/adr/0001-dependency-licenses-and-maintenance.md   NEW
```

`docs/adr/` does not exist yet, and there is no `CONTEXT.md`. This slice creates the folder with this ADR as number `0001`. Naming convention for later ADRs: `docs/adr/NNNN-kebab-case-title.md`, four-digit sequence. Do not add an index/README in this slice (Q7).

### 3.2 ADR structure

Use this outline (a lightweight Nygard/MADR style). Headings in this order:

```
# ADR 0001: Dependency licenses and maintenance status

| Status | Accepted | (or "Proposed" if Q1/Q2 are still open at merge time) |
| Date   | <date of merge-ready version> |
| Scope  | PRD 10.2 candidates + embedding models + Svelte fallback |

## Context
  - Why: PRD 10.2 requires the check before the project starts.
  - shelf's license: GPL-3.0 (only/or-later per Q2).
  - Distribution model that drives the license question: one self-contained
    single-file binary per RID (PRD 9.2), statically linking/bundling managed
    and native code; JS assets embedded in the binary; model downloaded at
    runtime into .shelf/models/ (not shipped in the binary).
  - Not legal advice; compatibility judged by the FSF license list.

## Criteria
  (copy of section 3.3 of this plan, as applied)

## Summary
  One table, rows in the order of section 1.1 (rows 1–20, then 21–23):
  | # | Candidate | Version (date) | License (SPDX) | GPL-3.0 compatible | Maintenance | AOT/trim claim | Native RIDs (osx-arm64/osx-x64/linux-x64/linux-arm64) | Verdict |

## Candidate notes
  One H3 per row. Fixed bullet list:
  - Package/ID, repo URL
  - Latest stable version + release date; prerelease noted separately
  - License: SPDX, link to license file; compatibility reasoning (1 line)
  - Maintenance: last release, release cadence over the last 12 months,
    last commit to default branch, open issues / open PRs, number of
    active maintainers or backing organization, NuGet downloads or
    GitHub stars (rough signal only)
  - AOT/trimming: quoted documented claim with link, or "no documented
    claim"; for NuGet packages, whether the package declares
    IsAotCompatible / IsTrimmable (check the nuspec or the
    project file at the release tag) if determinable, otherwise "unverified"
  - Native assets: per RID, present/absent/unverified, with the
    package path (e.g. runtimes/linux-arm64/native/...)
  - Risks
  - Verdict + fallback (see 3.4)
  - Sources: numbered list, each "URL (retrieved 2026-10-12)"

## Decision
  Bullet list: which candidates are adopted, which with fallback,
  which rejected, which pending (and on what: AOT spike, CLI decision,
  user decision Qx).

## Consequences
  - Inputs for the AOT spike (e.g. RIDs without native assets, packages
    without an AOT claim).
  - Follow-ups that need other files (PRD/roadmap corrections, notices
    file). Listed, not done.
  - Re-check trigger: re-run the check for a candidate when its major
    version changes or before the 1.0 release.
```

### 3.3 Evaluation criteria

| Criterion | How to determine | Source required |
|---|---|---|
| Version | Latest **stable** version on NuGet / GitHub releases / Hugging Face; note the newest prerelease separately if it is what the PRD needs | NuGet package page or GitHub release |
| License | SPDX ID from the package metadata **and** the license file in the repo at the release tag. If they differ, record both and flag it | License file URL |
| GPL-3.0 compatibility | `yes` if the FSF license list (https://www.gnu.org/licenses/license-list.html) marks the license as GPLv3-compatible; `no` if it marks it incompatible; `unclear` otherwise (goes to Q1). Note that Apache-2.0 is compatible with GPLv3 per that list; check this against the list rather than relying on this plan | FSF list |
| Maintenance | Signals: last release, releases in the last 12 months, last commit on the default branch, open-issue count, maintainer count/organization, downloads/stars. Proposed flag rule (confirm via Q4): **"maintenance risk"** if there is no release in 12 months **or** no commit in 6 months **or** a single maintainer without an organization behind them. A flag does not reject a candidate by itself | GitHub repo / NuGet |
| AOT/trimming | Documented claim only (README, docs, release notes, `IsAotCompatible` in project file at tag). Values: `claimed`, `claimed with limits` (quote them), `no claim`, `documented incompatible` | Doc URL |
| Native assets per RID | For NuGet packages with native code: list the `runtimes/<rid>/native/` entries for `osx-arm64`, `osx-x64`, `linux-x64`, `linux-arm64`. Method: the package's file listing on nuget.info / NuGet Package Explorer, or download the `.nupkg` into the scratchpad and run `unzip -l` (no extraction into the repo, no execution of anything inside it). For non-NuGet native libs (sqlite-vec, PDFium): the list of release assets | Package listing / release page |
| Model specifics (rows 21, 23) | Model license, ONNX file availability and size in MB, tokenizer type, embedding dimension | Hugging Face model card and file list |

### 3.4 Verdict values

| Verdict | Meaning |
|---|---|
| `adopt` | License compatible, no maintenance flag, no known gap for the 4 RIDs |
| `adopt with fallback` | Usable, but a risk exists; the fallback is named. PRD fallbacks to use where they apply: ReadyToRun instead of Native AOT (9.2, risk table); Svelte instead of RazorSlices (Q1); brute-force cosine in C# instead of sqlite-vec (risk table) |
| `pending` | Cannot be decided here; name the deciding step (AOT spike, CLI framework decision, user decision Qx) |
| `reject` | License incompatible or project abandoned; name a replacement only if the PRD names one, otherwise "replacement: open" |

The CLI framework rows (2, 3) get `adopt`/`reject` for license and maintenance only, plus the note "selection: CLI framework decision work package".

### 3.5 Research method and honesty rules (mandatory)

- Every fact comes from an authoritative source: the NuGet package page or nuspec, the project's GitHub repo (license file, releases, README/docs), the Hugging Face model card/file list, the FSF license list, the Microsoft .NET lifecycle page.
- Every fact carries a source URL and the retrieval date. Today is **2026-10-12**; use the actual date of retrieval if it differs.
- Anything not verifiable from such a source is written as `unverified` (with a short reason, e.g. "release page not reachable"). Never fill in from memory. This includes the PRD's own claims ("PDF.js (Apache 2.0)", "foliate-js (MIT)"), which must be verified like everything else.
- If the implementer has no web access (no WebFetch/WebSearch or equivalent), **stop** after creating the skeleton and report this. Do not produce an ADR filled from memory.
- Downloaded files (`.nupkg`, archives) go into the session scratchpad, in a new empty directory per download. List their contents; do not execute or build anything from them.
- Download counts and stars are rough signals only and never the sole reason for a verdict.

### 3.6 AOT and native-dependency risks (to be surfaced in the ADR, not resolved)

Native components that must be shipped per RID, each a distribution and AOT risk: SQLite via SQLitePCLRaw (row 9), sqlite-vec (row 10), ONNX Runtime (row 11), PDFium (rows 6–7). For each, the ADR must state which of the four RIDs have prebuilt binaries. A missing RID is a finding for the AOT spike, not a reason for this slice to search for workarounds.

Managed packages where the AOT claim matters most (PRD risk table): ModelContextProtocol, RazorSlices, Microsoft.ML.OnnxRuntime (managed part), Microsoft.ML.Tokenizers, VersOne.Epub, UglyToad.PdfPig, Spectre.Console.Cli, System.CommandLine.

## 4. Unit Tests

None. The slice contains no code.

## 5. Step Order

1. Confirm the branch `docs/m0-license-adr` is checked out and clean. Confirm web access is available (3.5). If not, stop and report.
2. Create `docs/adr/0001-dependency-licenses-and-maintenance.md` with the skeleton from 3.2: Context, Criteria, an empty Summary table with all 23 rows from 1.1, one empty H3 per row, Decision, Consequences.
3. Fill the runtime row (1): license, LTS end-of-support date.
4. Fill the managed NuGet rows (2–5, 8, 12–14): version, license, maintenance, AOT claim.
5. Fill the native rows (6, 7, 9, 10, 11): additionally the per-RID native asset listing (3.3). Check FTS5 for row 9 from a documented source.
6. Fill the JS asset rows (15–18) and the Svelte row (22).
7. Fill the model rows (21, 23), including the tokenizer type for row 21, then cross-check it against row 12's documented tokenizer support.
8. Fill test-only rows (19, 20) briefly.
9. Assign verdicts (3.4). Every `pending` and every `unclear` compatibility references the deciding step or an open question.
10. Write Decision and Consequences. Under Consequences, list follow-ups that touch other files (see Q6, Q9) without making them.
11. Set the ADR status: `Accepted` if Q1 and Q2 are answered by the user before merge, otherwise `Proposed`.
12. Run the quality gate (section 2). Push, open a PR. In the PR description, list the unanswered open questions from section 6 that the ADR depends on.

## 6. Open Questions

- **Q1: Dependency license policy.** Which licenses are acceptable for dependencies of a GPL-3.0 project? Proposed default: anything the FSF list marks GPLv3-compatible (permissive licenses, LGPL, MPL-2.0, GPL-3.0). Needs a user decision for: copyleft that is GPLv3-compatible but adds obligations (e.g. AGPL-3.0); non-OSI or usage-restricted licenses (e.g. CC-BY-NC or RAIL-style model licenses); public domain / "blessing" (SQLite). Until answered, such cases get compatibility `unclear` and verdict `pending`.
- **Q2: GPL-3.0-only or GPL-3.0-or-later?** `LICENSE` holds the GPLv3 text but nothing states the variant. This affects compatibility with some licenses (e.g. those compatible only with specific GPL versions). Should a `PackageLicenseExpression` / SPDX header be added later (separate slice)?
- **Q3: Does the embedding model license matter?** The model is downloaded at runtime and not shipped in the binary (PRD 9.2), so the model license binds the user, not shelf's distribution. Does that change if shelf mirrors the model (e.g. in GitHub Releases for offline install via `shelf model install`)? Which model licenses are acceptable as the default model?
- **Q4: Maintenance thresholds.** Confirm or adjust the proposed "maintenance risk" rule (no release in 12 months, or no commit in 6 months, or a single maintainer without an organization).
- **Q5: Candidates not named in the PRD.** The roadmap requires a TOML parser decision in M0, and the PRD needs a ULID generator (Q4), DE/EN stemming (F-SRC-6), and trigram fuzzy search (F-SRC-4), but names no library for any of them. Should these be checked in this ADR (then someone must name candidates), in a later ADR, or as part of the work package that chooses them? This plan excludes them.
- **Q6: Follow-ups to other docs.** After the ADR: should the roadmap M0 work package be ticked, and should PRD 10.2 get a license column or corrections (e.g. if a PRD license claim turns out wrong, or a candidate is rejected)? Separate slice or part of the next M0 slice? Also, PRD 13.1 Q1 and the risk table say the AOT spike happens "in M1", while the roadmap puts it in M0. That inconsistency should be fixed in the PRD (not in this slice).
- **Q7: ADR conventions.** Is `docs/adr/NNNN-title.md` with a light Nygard/MADR format acceptable as the project convention? Should there be a `docs/adr/README.md` index and an ADR template?
- **Q8: Docs-only PRs and CI.** Because of `paths-ignore`, this PR produces no check run. If a check is required on `main`, the PR cannot merge without an admin override (same issue as Q2 in `docs/slices/m0-ci-matrix.md`). How should this PR be merged?
- **Q9: Attribution obligations.** Apache-2.0 (NOTICE files), BSD, and MIT require notices when redistributing binaries and embedded JS assets. When and where is a `THIRD-PARTY-NOTICES` file produced (release pipeline in M5, or earlier)?
- **Q10: Transitive dependencies.** This plan checks direct candidates plus their native bundles. Should transitive managed dependencies (e.g. of ModelContextProtocol or PdfPig) also be license-checked now, or once the actual dependency graph exists after the AOT spike?
