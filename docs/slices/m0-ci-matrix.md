# Slice m0-ci-matrix: CI Build/Test Matrix and AOT Publish Job

| | |
|---|---|
| **Slice ID** | `m0-ci-matrix` |
| **Branch** | `ci/m0-matrix` |
| **Milestone** | M0 – Setup & Spikes |
| **Roadmap work package** | "CI" (roadmap 3, M0) |
| **PRD references** | 9.2 (four target platforms, Native AOT as the goal) |

---

## 1. Scope

**In scope**

- Rewrite `.github/workflows/build.yml`. Nothing else in the repo changes.
- Build and test job (`dotnet restore`, `build -c Release`, `test --no-build -c Release`) as a matrix over `linux-x64`, `linux-arm64`, `osx-arm64`, `osx-x64`. Each RID runs on a native GitHub-hosted runner of that architecture.
- AOT publish job: `dotnet publish src/Shelf.Cli` with `PublishAot=true` for each RID, followed by a smoke check of the published binary.
- An aggregator job that gives one stable check name (see 3.5).
- Workflow-level settings: least-privilege `permissions`, `concurrency`, action major versions, env defaults.

**Requirement covered:** PRD 9.2, first two bullets, only as far as CI is concerned. CI proves that the solution builds, all scenarios pass, and an AOT binary of the current CLI is produced and runs on all four platforms.

**Out of scope**

- The AOT spike: native dependencies (SQLite/FTS5, sqlite-vec, ONNX Runtime, PdfPig, and the others). The CLI has **no native dependencies today**, so this job only proves that the AOT toolchain works on every runner. It does not prove that shelf's future dependency stack is AOT-compatible.
- Setting `PublishAot` in any `.csproj` or in `Directory.Build.props` (see 3.4).
- Single-file/ReadyToRun fallback builds, uploading binaries as artifacts, release pipeline, signing/notarization, Homebrew (M5).
- Performance and cold-start measurement in CI (separate M0 work package, and from M1 on).
- NuGet lock files and NuGet caching (see 3.6).
- Changing branch protection settings (GitHub UI, not in the repo; see Q2).
- New feature files or step definitions (see 2).

## 2. Feature Files

**No new feature file in this slice.** The implementer must not add one.

Justification under CLAUDE.md rule 1: rule 1 requires feature files before implementation of *behavior*. This slice adds no user-visible behavior. It changes only the CI workflow. "The product builds and runs on four platforms" is a property of the build pipeline. The pipeline checks it directly, and a Gherkin scenario cannot check it from inside a test run on a single machine.

How the slice still serves BDD:

- The existing `tests/Shelf.Specs/Features/Cli/ExitCodes.feature` (`@PRD-7.1 @M0`) now runs on all four platforms. A platform-specific red scenario blocks the merge.
- The smoke check on the AOT binary (3.3) checks the same exit-code contract against the *published native binary*: `--version` gives exit 0, and running without arguments gives exit 2. It is a shell check in the workflow, not a scenario. This is a deliberate choice. Running the Reqnroll specs out-of-process against the AOT binary would need a second execution mode in `CliSteps` (process launch, binary path via env var). That is worth deciding once real CLI commands exist (see Q5), not now.

## 3. Design

### 3.1 Files

```
.github/workflows/build.yml   CHANGE (full rewrite, same file name and workflow name "build")
```

No changes to `src/`, `tests/`, `Directory.Build.props`, `Directory.Packages.props`, or `global.json`.

### 3.2 Workflow-level settings

| Setting | Value | Reason |
|---|---|---|
| `name` | `build` (unchanged) | Keeps the workflow name stable. |
| `on.push.branches` / `on.pull_request.branches` | `[main]` (unchanged) | |
| `paths-ignore` (both triggers) | `docs/**`, `**/*.md`, `**/*.txt`, `LICENSE` (unchanged) | Keep the previous slice's decision: `**/*.feature` is **not** ignored. Do not add any entry. `.github/workflows/**` must not be ignored. |
| `on.workflow_dispatch` | add, no inputs | Allows a manual re-run on a branch, for example after a runner image change. |
| `permissions` | `contents: read` at workflow level, nothing per job | Least privilege. No job needs to write. |
| `concurrency.group` | `${{ github.workflow }}-${{ github.event_name == 'pull_request' && github.ref || github.sha }}` | One run per PR. Non-PR runs get a group per commit SHA, so GitHub never drops a pending run on `main` and every merge commit gets a result. |
| `concurrency.cancel-in-progress` | `${{ github.event_name == 'pull_request' }}` | Cancel superseded PR runs. Never cancel runs on `main`, so that every merge commit gets a full result. |
| `env` | `DOTNET_NOLOGO: true`, `DOTNET_CLI_TELEMETRY_OPTOUT: true`, `DOTNET_SKIP_FIRST_TIME_EXPERIENCE: true` | Quieter logs, no telemetry. |
| `defaults.run.shell` | `bash` | All runners are Linux/macOS. Bash on Actions runs with `-eo pipefail`. |

Actions, pinned to **major** versions. Use the latest major at implementation time and check the release pages:

- `actions/checkout` (currently `@v4` in the file; v5 exists, so check for a newer one)
- `actions/setup-dotnet` (currently `@v4`; v5 exists, so check for a newer one)

`setup-dotnet` input: keep `dotnet-version: 10.0.x`. This installs the newest 10.0 SDK band, which satisfies `global.json` (`10.0.100`, `rollForward: latestFeature`). Do not switch to `global-json-file`, because `setup-dotnet`'s handling of `rollForward` is not verified (see Q6).

### 3.3 Jobs

All three jobs use one shared matrix definition (repeat it in both matrix jobs; YAML anchors are not worth the readability cost here):

| `rid` | `os` (runner label) | `arch` (expected `uname -m`) |
|---|---|---|
| `linux-x64` | `ubuntu-24.04` | `x86_64` |
| `linux-arm64` | `ubuntu-24.04-arm` | `aarch64` |
| `osx-arm64` | `macos-15` | `arm64` |
| `osx-x64` | `macos-15-intel` | `x86_64` |

Runner label assumptions (verify against the GitHub "GitHub-hosted runners" docs before implementing; see Q1):

- `ubuntu-24.04-arm` is a free standard arm64 runner for public repositories.
- `macos-15` is arm64 (M1).
- `macos-15-intel` is the remaining x64 macOS label. `macos-13` has been retired. GitHub has announced the end of Intel macOS runners, so this label has a limited lifetime.
- Use pinned OS versions, not `-latest`. A silent image OS upgrade should show up as a visible diff in the workflow, not as a sudden red build.

Matrix settings for both matrix jobs: `strategy.fail-fast: false`, so that one failing platform does not hide the result of the others. `timeout-minutes: 20`.

#### Job `test` (build + test)

Display name: `test (${{ matrix.rid }})`. Steps:

1. `actions/checkout`
2. **Assert runner architecture:** `uname -m` must equal `matrix.arch`. Otherwise fail with a message naming the label. This catches a runner label that does not deliver the expected CPU (for example, a mislabeled Intel/ARM macOS image).
3. `actions/setup-dotnet` (`dotnet-version: 10.0.x`)
4. `dotnet --info` (diagnostics only)
5. `dotnet restore`
6. `dotnet build --no-restore -c Release`
7. `dotnet test --no-build -c Release`

These are the same commands as today, so the test runner setup (MTP via `global.json`) stays unchanged. No `-r` on build/test: tests run framework-dependent on the runner's native architecture, which is exactly what we want.

#### Job `aot` (AOT publish + smoke check)

Display name: `aot (${{ matrix.rid }})`. Separate job, **no `needs: test`**: it runs in parallel. GitHub-hosted minutes are free for public repos, and parallel jobs give faster feedback.

Steps:

1. `actions/checkout`
2. Assert runner architecture (same as in `test`).
3. **Linux only** (`if: runner.os == 'Linux'`): ensure the AOT prerequisites `clang` and `zlib1g-dev` are present (`sudo apt-get update && sudo apt-get install -y clang zlib1g-dev`; this does nothing if they are already installed). Reason: the arm64 Ubuntu image is a different image from the x64 one, and its preinstalled toolset must not be assumed. macOS: Xcode command line tools are on the image, so no step is needed.
4. `actions/setup-dotnet` (`dotnet-version: 10.0.x`)
5. Publish:

   ```
   dotnet publish src/Shelf.Cli/Shelf.Cli.csproj -c Release -r ${{ matrix.rid }} -p:PublishAot=true -o "$RUNNER_TEMP/aot"
   ```

   - Pass the **project path**, never the solution. Publishing the solution would pull `Shelf.Specs` into the graph with a RID and `PublishAot`. That fails or produces nonsense, and xUnit/Reqnroll are not AOT-capable.
   - The output goes to `$RUNNER_TEMP`, not into the repo tree.
   - `-p:PublishAot=true` becomes a global property and also reaches `Shelf.Core`. That is harmless for a class library, which already has `IsAotCompatible=true`.
6. **Smoke check** (bash, with `$BIN="$RUNNER_TEMP/aot/shelf"`):
   - `$BIN` exists and is executable.
   - `$RUNNER_TEMP/aot/shelf.dll` does **not** exist. This proves a native binary was produced, not a framework-dependent app.
   - `file "$BIN"` is printed to the log for diagnostics. Do not parse it, because the output format differs between macOS and Linux.
   - `"$BIN" --version` exits 0, and its stdout matches `^shelf [0-9]+\.[0-9]+\.[0-9]+`. Print the output.
   - `"$BIN"` with no arguments exits **2**. Capture the exit code without letting `bash -e` abort first: run it inside `if`, or use `set +e`/`set -e` around the call.
   - Print the binary size (`ls -l`), for information only. There is no size threshold in this slice.

#### Job `ci` (aggregator)

- `needs: [test, aot]`, `if: always()`, `runs-on: ubuntu-24.04`, no checkout.
- One step: fail unless `needs.test.result == 'success'` and `needs.aot.result == 'success'`.
- Purpose: one stable check name, `ci`, for branch protection that does not change when the matrix grows or shrinks (see Q2).

### 3.4 Where `PublishAot` is set: command line only

Decision: `PublishAot=true` is passed **only** on the `dotnet publish` command line in the `aot` job. It is **not** set in `Shelf.Cli.csproj`.

Reasons:

- `Shelf.Specs` references the `Shelf.Cli` Exe project. Setting `PublishAot`, and with it `SelfContained` and a `RuntimeIdentifier`, on `Shelf.Cli` in the project file risks NETSDK1150/NETSDK1151 in normal `dotnet build`/`dotnet test` of the solution (risk noted in `docs/slices/m0-skeleton.md` 3.6).
- The trim/AOT analyzers are already active at build time through `IsAotCompatible=true` on `Shelf.Core` and `Shelf.Cli`, with `TreatWarningsAsErrors`. Moving `PublishAot` into the project adds nothing for early warnings.
- Whether `PublishAot` moves into the project file, and the mitigation if it does (`ShelfApp` into a library, or `ValidateExecutableReferencesMatchSelfContained=false` in Specs), is decided by the AOT spike, not here.

### 3.5 Check names and branch protection

Today the only check is `build` (workflow `build`, job `build`). After this slice the checks are `test (linux-x64)`, `test (linux-arm64)`, `test (osx-arm64)`, `test (osx-x64)`, `aot (...)` ×4, and `ci`. **If `build` is a required status check on `main`, PRs will wait for it forever after the merge.** The fix happens in the repository settings, not in this slice (see Q2).

### 3.6 NuGet cache: not added

- Restore pulls only `xunit.v3` and `Reqnroll.xunit.v3` plus their dependencies. That takes seconds.
- The AOT job also downloads the ILCompiler runtime packs per RID, which are larger. Caching them would need a stable key. `setup-dotnet`'s built-in `cache: true` requires `packages.lock.json`, which the repo does not have.
- Decision: no cache now. Revisit when the AOT spike adds native packages (ONNX Runtime, SQLite), if restore time becomes noticeable (Q4).

### 3.7 AOT and native-dependency risks

- **No native dependencies in the CLI today.** A green `aot` job only proves that the toolchain (ILCompiler plus the platform linker) works on each runner. It must not be cited as evidence that shelf's dependency stack is AOT-compatible. That is the AOT spike's job.
- **Linux linker prerequisites** (`clang`, `zlib1g-dev`): handled by the install step. The risk is highest on `ubuntu-24.04-arm`.
- **macOS:** the AOT linker uses Xcode's toolchain. A runner image update that changes Xcode can break linking. Pinning `macos-15` / `macos-15-intel` limits surprise changes.
- **osx-x64 runner lifetime:** Intel macOS runners are being retired by GitHub. When `macos-15-intel` goes away, `osx-x64` can only be **cross-compiled** on arm64 macOS (`-r osx-x64` on `macos-15`; supported by .NET for macOS) and run under Rosetta, which may not be available on hosted runners. Tests would then no longer run natively on x64. See Q1.
- **ILC warnings:** expected to fail the publish under `TreatWarningsAsErrors`, because ILC inherits it. Verify once locally (step 2). If ILC warnings do not fail the build, report this in the PR instead of adding flags silently.
- **Global `-p:PublishAot=true` on referenced projects:** harmless today, because `Shelf.Core` is a pure managed library. Future native-asset packages referenced from libraries may need RID-specific restore. The AOT spike decides.

## 4. Unit Tests

None. The slice contains no C# code. The smoke check in the `aot` job and the existing scenarios running on all four platforms are the verification.

## 5. Step Order

1. **Verify assumptions.** Check the GitHub docs for the runner labels (`ubuntu-24.04`, `ubuntu-24.04-arm`, `macos-15`, `macos-15-intel`): they must exist, be free for public repos, and have the expected architecture. Check the latest major versions of `actions/checkout` and `actions/setup-dotnet`. If a label differs, adjust the matrix table and note it in the PR.
2. **Local AOT dry run** (dev Mac, `osx-arm64`): run `dotnet publish src/Shelf.Cli/Shelf.Cli.csproj -c Release -r osx-arm64 -p:PublishAot=true -o <scratch dir>`. Check: no NETSDK1150/1151, zero warnings, no `shelf.dll` in the output, `--version` exits 0 with `shelf X.Y.Z`, and no arguments exits 2. Then run `dotnet build -c Release && dotnet test --no-build -c Release` at the root and confirm they are still green and unaffected.
3. Rewrite `build.yml`, part 1: workflow-level settings (3.2). Leave `paths-ignore` unchanged.
4. Add the `test` matrix job (3.3).
5. Add the `aot` matrix job, including the Linux prerequisite step and the smoke check (3.3).
6. Add the `ci` aggregator job (3.3).
7. Optional local lint: `actionlint` if available. Do not add it to the repo.
8. Push `ci/m0-matrix` and open the PR. All nine checks must be green. In the logs, check that the architecture assertion printed the expected `uname -m` for each runner, and that `file` shows the right architecture for each AOT binary.
9. Negative check (throwaway commit, not merged): make the smoke check expect exit 1 for no arguments, and confirm that all four `aot` jobs and `ci` go red. Revert.
10. In the PR description, list the new check names and point to Q2 so that the repository owner can update branch protection before or right after the merge.

## 6. Open Questions

- **Q1: Runner labels and osx-x64 lifetime.** The plan assumes `ubuntu-24.04-arm` (free for public repos), `macos-15` (arm64), and `macos-15-intel` (x64). These are not verified in this plan. When Intel macOS runners are retired, what is the policy for `osx-x64`: cross-compile on arm64 and skip native tests, drop native `osx-x64` testing, or drop the target? PRD 9.2 lists `osx-x64` as a target.
- **Q2: Required status checks.** Is `build` currently a required check on `main`? If so, it must be replaced. Should the required check become the single aggregator `ci` (recommended, stable across matrix changes) or the individual `test (...)` / `aot (...)` checks? Related, existing issue: with `paths-ignore`, a docs-only PR never produces a required check and waits forever. Is that acceptable, or should the workflow always run and skip work instead?
- **Q3: AOT on all four platforms vs. linux-x64 only.** This plan runs AOT on all four. Reasons: hosted minutes are free for public repos, each RID builds natively without cross-compiling, the cost is small without native dependencies, and the M0 exit criterion requires the AOT spike on all four platforms, so the harness should be proven there first. If macOS queue times turn out to be long, should `aot` on macOS run only on `push` to `main` and not on every PR?
- **Q4: NuGet lock files / caching.** Should the repo adopt `packages.lock.json` (`RestorePackagesWithLockFile`) for reproducible restores and `setup-dotnet` caching? This is deferred here; it is a repo-wide change outside this slice.
- **Q5: Specs against the published binary.** Should the Reqnroll suite later get an out-of-process mode that runs `ExitCodes.feature` (and future CLI features) against the AOT binary in the `aot` job? That would replace the hand-written smoke check with scenarios. Decide when the CLI framework is chosen.
- **Q6: SDK version source in CI.** `dotnet-version: 10.0.x` plus `global.json` `latestFeature` means CI picks up new SDK feature bands automatically. Is that wanted, or should CI pin exactly (via `global-json-file` and a stricter `rollForward`)?
- **Q7: Artifact upload.** Should the `aot` job upload the binaries (short retention) for manual testing on other machines, or should that wait for the M5 release pipeline? Not included in this plan.
