# Slice m0-cli-framework: CLI Framework Decision and Integration

| | |
|---|---|
| **Slice ID** | `m0-cli-framework` |
| **Branch** | `feat/m0-cli-framework` |
| **Milestone** | M0 – Setup & Spikes |
| **Roadmap work package** | "CLI framework decision" (roadmap 3, M0). Contributes to the exit criterion "CLI framework and TOML parser are chosen" (CLI framework part only, see Q5). |
| **PRD references** | 7.1 (CLI: `--json`, exit codes 0/1/2, `NO_COLOR`, shell completion), 9.1 (cold start < 150 ms), 9.2 (Native AOT, single binary), 10.2 (CLI candidates) |
| **Inputs** | ADR 0001 rows 2 and 3, `docs/slices/m0-skeleton.md`, `docs/slices/m0-ci-matrix.md` |

---

## 1. Scope

**In scope**

1. A reproducible benchmark that compares **System.CommandLine 2.0.12** and **Spectre.Console.Cli 0.57.2** (versions from ADR 0001) as Native AOT prototypes with the same command surface, against a framework-free baseline, on local `osx-arm64`.
2. ADR `docs/adr/0002-cli-framework.md` with criteria (fixed before measuring), measurements, decision, consequences.
3. Integration of the winning framework into `src/Shelf.Cli`, replacing the hand-written parser in `ShelfApp`, while keeping `ShelfApp.Run(string[] args, TextWriter stdout, TextWriter stderr)` and its exit-code/stream contract.
4. CLI plumbing that PRD 7.1 requires of every command and that can be specified now without any real command: global `--json` option, `NO_COLOR` handling via an injected environment, exit code mapping (usage error = 2, unhandled error = 1).
5. New feature file `GlobalOptions.feature`, new step definitions, a small unit test project `tests/Shelf.Cli.Tests`.
6. Roadmap: mark the M0 work package "CLI framework decision" as done with a link to ADR 0002 (same style as the ADR 0001 line).

**Requirement covered:** PRD 7.1 bullets "Every command supports `--json`" (as a recognized global option only), "Exit codes: 0/1/2" (0 and 2 by scenario, 1 by design rule), "`NO_COLOR` is respected". Milestone M0.

**Out of scope**

- Any real shelf command (`init`, `search`, ...). They start in M1. Therefore no subcommand help scenarios and no `--json` output format.
- What `--json` does for `--version`/`--help` (Q6).
- Colored or tabular output itself (M1). This slice only guarantees that no ANSI codes are written when `NO_COLOR` is set.
- Shell completion scripts (M5). The ADR only records what each framework offers.
- TOML parser selection (recommended as a separate slice, Q5).
- Cold start timing in CI, on any of the four platforms (decision in 3.2).
- Moving `PublishAot` into a project file, `ReadyToRun` fallback, size tuning (`InvariantGlobalization`, `UseSystemResourceKeys`, `OptimizationPreference`).
- Logging setup (separate M0 work package).
- Out-of-process specs against the AOT binary (m0-ci-matrix Q5, still deferred).

## 2. Feature Files

Rules for the implementer:

- Write these files first. Run `dotnet test`: the new scenarios must be red (undefined steps / unknown `--json`) before integration starts.
- Do **not** edit the existing scenarios in `ExitCodes.feature`. If the chosen framework makes a text assertion there impossible to keep at reasonable cost, stop and raise Q4. Exit codes and stream assignment are never negotiable.
- No scenarios for the benchmark.

### 2.1 `tests/Shelf.Specs/Features/Cli/ExitCodes.feature`

Unchanged (see m0-skeleton 2). It must stay green with the new framework, including the exact texts `Usage: shelf`, `Unknown command 'frobnicate'`, `Unknown option '--frobnicate'`, unless Q4 is decided otherwise.

### 2.2 `tests/Shelf.Specs/Features/Cli/GlobalOptions.feature` (new)

```gherkin
@PRD-7.1 @M0
Feature: Global CLI options and environment
  As a user or a script calling shelf
  I want options and environment settings that apply to every command
  So that machine-readable and colorless output work the same everywhere

  Scenario: The help lists the global --json option
    When I run shelf with arguments "--help"
    Then the exit code is 0
    And standard output contains "--json"
    And standard error is empty

  Scenario Outline: --json is a known global option
    When I run shelf with arguments "<arguments>"
    Then the exit code is 2
    And standard output is empty
    And standard error starts with "<message>"
    And standard error does not contain "Unknown option '--json'"

    Examples:
      | arguments           | message                       |
      | --json              | Usage: shelf                  |
      | --json frobnicate   | Unknown command 'frobnicate'  |
      | --json --frobnicate | Unknown option '--frobnicate' |

  Scenario Outline: NO_COLOR suppresses color codes on a terminal
    Given standard output is a terminal
    And the environment variable "NO_COLOR" is set to "<value>"
    When I run shelf with arguments "<arguments>"
    Then the exit code is <exit code>
    And standard output contains no ANSI escape sequences
    And standard error contains no ANSI escape sequences

    Examples:
      | value | arguments    | exit code |
      | 1     | --help       | 0         |
      | 1     |              | 2         |
      | 1     | --frobnicate | 2         |
      | true  | --help       | 0         |
```

Notes:

- "the environment variable ... is set" and "standard output is a terminal" are **scenario-scoped inputs** passed into `ShelfApp` through `CliEnvironment` (3.4). They never touch the process environment or `Console`, because specs run in parallel in one process.
- The NO_COLOR scenario is meaningful only together with "standard output is a terminal": without it, a non-terminal writer would pass trivially. A positive counterpart ("colors appear when NO_COLOR is unset") is deliberately not written: there is no colored output yet (M1).
- Empty `arguments` cell means "no arguments" (existing step behavior).

### 2.3 New steps in `tests/Shelf.Specs/Steps/CliSteps.cs`

Cucumber expressions, as in the existing file:

| Step | Behavior |
|---|---|
| `Given the environment variable {string} is set to {string}` | Store in a per-scenario `Dictionary<string, string>`. |
| `Given standard output is a terminal` | Set a per-scenario flag `stdoutIsTerminal = true` (default `false`). |
| `Then standard output contains {string}` | Ordinal `Contains`. |
| `Then standard error does not contain {string}` | Ordinal `DoesNotContain`. |
| `Then standard output contains no ANSI escape sequences` | Assert no `'\u001b'` char. |
| `Then standard error contains no ANSI escape sequences` | Same for stderr. |

The existing `When I run shelf with arguments {string}` changes to call the 4-argument `ShelfApp.Run` with `CliEnvironment.FromVariables(name => dict.GetValueOrDefault(name), stdoutIsTerminal)`.

## 3. Design

### 3.1 Files

```
experiments/cli-framework/                       NEW  benchmark, not in shelf.slnx (3.2)
  Directory.Build.props                          NEW  stops inheritance from repo root
  Directory.Packages.props                       NEW  own CPM versions
  .gitignore                                     NEW  bin/, obj/, out/, results/
  measure.sh                                     NEW  publish + checks + timing
  Baseline/Baseline.csproj, Program.cs           NEW  hand-written parser, same surface
  SystemCommandLine/SystemCommandLine.csproj, Program.cs   NEW
  SpectreCli/SpectreCli.csproj, Program.cs       NEW
docs/adr/0002-cli-framework.md                   NEW
docs/roadmap.md                                  CHANGE  mark work package done (link ADR 0002)
Directory.Packages.props                         CHANGE  add winner's PackageVersion (+ dependency pins if needed)
src/Shelf.Cli/Shelf.Cli.csproj                   CHANGE  PackageReference, InternalsVisibleTo, SatelliteResourceLanguages (3.6)
src/Shelf.Cli/Program.cs                         CHANGE  pass CliEnvironment from process
src/Shelf.Cli/ShelfApp.cs                        CHANGE  framework-based command tree
src/Shelf.Cli/CliEnvironment.cs                  NEW
src/Shelf.Cli/ExitCodes.cs                       NEW  constants Success=0, Error=1, Usage=2
src/Shelf.Cli/VersionInfo.cs                     NEW  extracted from ShelfApp.GetVersion
src/Shelf.Cli/GlobalOptions.cs                   NEW  --json definition (framework-specific shape)
tests/Shelf.Specs/Features/Cli/GlobalOptions.feature     NEW
tests/Shelf.Specs/Steps/CliSteps.cs              CHANGE
tests/Shelf.Cli.Tests/Shelf.Cli.Tests.csproj     NEW  xunit.v3, OutputType Exe, MTP (like Shelf.Specs)
tests/Shelf.Cli.Tests/CliEnvironmentTests.cs     NEW
tests/Shelf.Cli.Tests/VersionInfoTests.cs        NEW
shelf.slnx                                       CHANGE  add tests/Shelf.Cli.Tests
```

No change to `.github/workflows/build.yml`, `Directory.Build.props`, `global.json`.

### 3.2 Benchmark

**Location: `experiments/cli-framework/`, committed, outside `shelf.slnx`.** Justification: the ADR must cite reproducible commands, so the sources and the script have to stay in the repo. A scratch directory outside the repo would make the ADR numbers unverifiable. Isolation:

- Root `dotnet build`/`test`/`format` and CI use `shelf.slnx`, which does not list the prototypes, so CI never builds them.
- `experiments/cli-framework/Directory.Build.props` does **not** import the root file. It sets `TargetFramework=net10.0`, `Nullable=enable`, `ImplicitUsings=enable`, `LangVersion=latest`, `TreatWarningsAsErrors=false` (warnings must be collected and counted, not stop the publish at the first one), `PublishAot=true`, `IsAotCompatible=true`. No `InvariantGlobalization` or size options, so that results match `Shelf.Cli`'s settings.
- Own `Directory.Packages.props` with `System.CommandLine` 2.0.12 and `Spectre.Console.Cli` 0.57.2.
- Distinct `AssemblyName`s: `proto-baseline`, `proto-scl`, `proto-spectre`.

**Command surface (identical in all three prototypes):**

| Invocation | Exit | stdout | stderr |
|---|---|---|---|
| `--version` | 0 | `proto 1.0.0` | empty |
| `--help` | 0 | help text listing `greet`, `tag`, `--json` | empty |
| `greet World --times 2` | 0 | `Hello, World` twice | empty |
| `--json greet World` and `greet World --json` | 0 | `{"greeting":"Hello, World"}` (string literal, no System.Text.Json) | empty |
| `tag add fiction` | 0 | `added fiction` | empty |
| (no args) | 2 | empty | usage |
| `greet` (missing argument) | 2 | empty | error |
| `greet World --times abc` | 2 | empty | error |
| `frobnicate` | 2 | empty | error |
| `--help` with `NO_COLOR=1` under a pseudo-terminal (`script -q /dev/null <cmd>`) | 0 | no ESC byte | |

`tag add` exists to judge nested subcommands (PRD has `tag add|rm|list|rename|merge`). Each prototype writes only to the `TextWriter`s it receives from `Main` (mirrors `ShelfApp`), so the in-process testability criterion is exercised.

**`measure.sh`** (bash 3.2 compatible, `set -euo pipefail`), steps per prototype:

1. `rm -rf bin obj out/<p>`, then `dotnet publish <p> -c Release -r osx-arm64 -p:TrimmerSingleWarn=false -o out/<p> 2>&1 | tee results/<p>.publish.log`. `TrimmerSingleWarn=false` makes per-call-site warnings visible instead of one IL2104 per assembly. Do not add `NoWarn` or `SuppressTrimAnalysisWarnings`.
2. Warnings: count `warning IL[0-9]+` and `warning AOT` lines in the log, grouped by code. Copy distinct lines into `results/<p>.warnings.txt`.
3. Functional checks: run every row of the table above, compare exit code and output. Any mismatch marks the prototype **functionally failed under AOT** (prints which row).
4. Size: `stat -f%z out/<p>/<binary>` (binary only; `.dSYM` excluded). Also list the output directory to detect extra files (culture folders, `.dll`).
5. Cold start: `hyperfine -N --warmup 5 --runs 50 --export-json results/<p>.json` for `--version`, `greet World --times 2`, `--help`. `-N` (no intermediate shell) is required at this time scale. Record median, p95 (from the JSON `times` array), min, max.
6. First run: one run immediately after publish, before warmup, reported as information only.

Fallback if `hyperfine` is not installed (prefer `brew install hyperfine`): a `zsh` loop with `zmodload zsh/datetime` and `$EPOCHREALTIME`, 5 warmup runs, 50 measured runs, stdout to `/dev/null`, median and p95 from `sort -n`. Record which tool produced the numbers.

Environment to record in the ADR: date, `sw_vers`, `uname -m`, `sysctl -n machdep.cpu.brand_string`, RAM, `dotnet --version`, package versions, power source (on AC), tool used. Close other heavy processes.

**Derived values:** framework overhead = prototype median minus baseline median (per command); size overhead = prototype size minus baseline size.

**Platform scope:** numbers come from local `osx-arm64` only. Numbers for `osx-x64`, `linux-x64`, `linux-arm64` are out of scope.

**CI timing decision: no.** The CI `aot` job is not changed in this slice. Reasons: hosted runners are shared and noisy, so ms-level numbers would be misleading without many runs; the roadmap has a separate M0 work package "Cold start measurement" that owns CI timing; leaving `build.yml` untouched keeps the smoke check stable. The ADR lists it as a follow-up for that work package.

### 3.3 Decision criteria (fixed before measuring; write them into the ADR before running `measure.sh`)

**Gates** (a candidate that fails a gate is out, regardless of score):

| ID | Gate |
|---|---|
| G1 | AOT publish with **zero** `IL*`/AOT warnings from the framework, with no suppression. (Shelf builds with `TreatWarningsAsErrors`; see Q2 for accepting suppressions.) |
| G2 | All functional checks pass on the **AOT binary** (reflection-based binding that compiles but fails at runtime is a fail). |
| G3 | In-process invocation with caller-supplied stdout/stderr writers, no write to static `Console`/`AnsiConsole`, usage errors mappable to exit 2 on stderr. Extra glue code is allowed; record how much. |
| G4 | `--version` p95 on `osx-arm64` below 150 ms (PRD 9.1). |

**Weighted score** (1 to 5 per criterion; bands are a proposal, see Q1):

| Criterion | Weight | Scoring |
|---|---|---|
| Cold start overhead vs. baseline (`--version` and `greet`, median) | 30 % | ≤ 10 ms: 5, ≤ 20 ms: 4, ≤ 35 ms: 3, ≤ 50 ms: 2, more: 1 |
| PRD 7.1 fit: global `--json` (recursive option), `NO_COLOR`, help/version, exit-code control, shell completion for bash/zsh/fish without external tool | 20 % | 1 point each for: global option, NO_COLOR controllable via injected setting, version text controllable (no entry-assembly lookup), exit codes fully controllable, completion generation |
| Binary size overhead vs. baseline | 15 % | ≤ 1 MB: 5, ≤ 2 MB: 4, ≤ 4 MB: 3, ≤ 8 MB: 2, more: 1 |
| API ergonomics for about 12 commands incl. nested `tag` subcommands | 15 % | Judgement, justified in 2-3 sentences: lines of glue code in the prototype, type safety of option binding, how commands are added |
| Maintenance and stability (ADR 0001 data: release cadence, 1.0 status, open majors) | 10 % | Judgement from ADR 0001 rows 2 and 3 |
| Testability (amount of glue needed for G3, determinism of output across locales) | 10 % | Judgement |

The implementer applies these to the measured data and decides. If no candidate passes all gates, stop and raise Q3 (do not pick one). The ADR also lists option C "keep the hand-written parser" (the baseline), so that this fallback is documented.

### 3.4 Integration into `Shelf.Cli` (framework-agnostic contract)

**`ShelfApp`**

```csharp
public static class ShelfApp
{
    public static int Run(string[] args, TextWriter stdout, TextWriter stderr);              // unchanged; uses CliEnvironment.Empty
    public static int Run(string[] args, TextWriter stdout, TextWriter stderr, CliEnvironment environment);
}
```

Behavior (all existing rows from m0-skeleton 3.3 stay):

| Input | Exit | stdout | stderr |
|---|---|---|---|
| `--version` | 0 | `shelf <VersionInfo>` | – |
| `--help` | 0 | help, includes `--json` | – |
| no arguments, or only global options (`--json`) | 2 | – | usage, first line starts with `Usage: shelf` |
| unknown option | 2 | – | `Unknown option '<arg>'`, then usage |
| unknown command | 2 | – | `Unknown command '<arg>'`, then usage |
| unhandled exception in a command | 1 | – | one-line message (no stack trace; Q8) |

Rules:

- Never write to `Console`, `AnsiConsole` statics, or read `Environment` inside `ShelfApp`. Only `Program.cs` reads the process.
- Set the application name explicitly to `shelf`. Both frameworks may derive it from the process path otherwise, which is the test host in specs.
- Version comes from `VersionInfo` (assembly attribute of `ShelfApp`'s assembly), never from the framework's built-in entry-assembly lookup (wrong in specs: entry assembly is the test host).
- Parse errors are rendered by shelf's own code into the texts above (the frameworks' native error texts differ). This also gives exit 2 explicitly; do not rely on framework defaults (to verify in the prototype: System.CommandLine's parse error action returns 1, Spectre's `CommandApp.Run` returns -1 on errors).
- `--json`: one global/recursive option definition in `GlobalOptions.cs`. It has no effect yet (Q6), but it must parse anywhere and show in help.

**`CliEnvironment`** (public, sealed, immutable)

- Properties: `bool NoColor`, `bool StdoutIsTerminal`.
- `static CliEnvironment Empty` → `NoColor=false`, `StdoutIsTerminal=false`.
- `static CliEnvironment FromVariables(Func<string, string?> getVariable, bool stdoutIsTerminal)`: `NoColor` is true when `NO_COLOR` is present **and non-empty** (no-color.org rule; any value, including `0` or `false`).
- `bool UseColor => StdoutIsTerminal && !NoColor`. The framework's console/color setting is driven by this, not by the framework's own environment detection.

**`Program.cs`**

```csharp
return ShelfApp.Run(args, Console.Out, Console.Error,
    CliEnvironment.FromVariables(Environment.GetEnvironmentVariable, !Console.IsOutputRedirected));
```

**Framework-specific notes (apply the one that wins):**

- System.CommandLine 2.0.12: `RootCommand` with explicit name; `Option<bool>("--json") { Recursive = true }`; `root.Parse(args)`; on `parseResult.Errors.Count > 0` render own message and return 2; otherwise `parseResult.Invoke(new InvocationConfiguration { Output = stdout, Error = stderr, ... })` and map exceptions to 1. Replace the built-in version option's action so it prints `shelf <VersionInfo>`. Check whether help output can start with `Usage: shelf`; if not, Q4.
- Spectre.Console.Cli 0.57.2: `CommandApp` with `SetApplicationName("shelf")`, `SetApplicationVersion(VersionInfo.Get())`, `PropagateExceptions()`; console created via `AnsiConsole.Create(new AnsiConsoleSettings { Out = new AnsiConsoleOutput(stdout), Ansi = UseColor ? Detect/Yes : No, ColorSystem = ... })`; catch parse exceptions and render to stderr with exit 2; `--json` on a shared base `CommandSettings`. Verify that Spectre does not read `NO_COLOR` from the process environment in a way that overrides the injected setting. Verify that errors and help can be routed to stderr/stdout as required (Spectre writes everything to its one console by default).

### 3.5 Packages

- Root `Directory.Packages.props`: add only the winner, at the ADR 0001 version (`System.CommandLine` 2.0.12 or `Spectre.Console.Cli` 0.57.2). Do not use System.CommandLine 3.0.0-rc. If a newer stable patch exists at implementation time, use the ADR 0001 version anyway and note the newer one in ADR 0002 (license/maintenance were checked for the ADR 0001 version).
- Transitive packages (Spectre.Console, Spectre.Console.Ansi) are not pinned unless restore requires it.

### 3.6 AOT and native-dependency risks

- No native dependencies. Both candidates are managed.
- **Spectre.Console.Cli** documents `IsAotCompatible=false` and `IsTrimmable=false` (ADR 0001 row 2). Expect trim/AOT warnings from reflection-based settings binding and type converters; with `IsAotCompatible=true` on `Shelf.Cli`, analyzer warnings on annotated APIs fail the build. Runtime binding failures under AOT are possible even with zero warnings, hence gate G2. If Spectre is chosen with suppressions, PRD 9.2's fallback (ReadyToRun) becomes likely for the whole binary.
- **System.CommandLine**: `IsAotCompatible=true` is claimed at `main`, not verified at the 2.0.12 tag (ADR 0001 row 3). The benchmark verifies it.
- **Localized resources:** framework messages may follow `CurrentUICulture` (for example on a German system), which would make help/error texts and specs locale-dependent, and satellite resource folders could appear next to the AOT binary. Check the publish output for culture subfolders. Set `<SatelliteResourceLanguages>en</SatelliteResourceLanguages>` in `Shelf.Cli.csproj` if any appear or if messages are localized. Own error texts (3.4) reduce the exposure.
- **ILC warnings and `TreatWarningsAsErrors`:** the m0-ci-matrix slice expected ILC to inherit warnings-as-errors. Verify with the winner's publish; never add `NoWarn` for `IL*` codes in `src/`.
- **NETSDK1150/1151:** `PublishAot` stays command-line only (m0-ci-matrix 3.4). `Shelf.Cli.Tests` references the Exe project like `Shelf.Specs` does; same constraint.

## 4. Unit Tests

Project `tests/Shelf.Cli.Tests` (xunit.v3 via MTP, `OutputType=Exe`, `IsPackable=false`, `ProjectReference` to `Shelf.Cli`; `Shelf.Cli` gets `InternalsVisibleTo Shelf.Cli.Tests`).

| Unit | Cases (`[Theory]` + `[InlineData]`) |
|---|---|
| `CliEnvironment.FromVariables` | `NO_COLOR` unset → `NoColor=false`; `""` → false; `"1"`, `"0"`, `"false"`, `"yes"` → true. `UseColor` truth table over `NoColor` × `StdoutIsTerminal` (only `false/true` → true). |
| `VersionInfo.Format(string? informationalVersion)` (internal, pure) | `"1.2.3+abc123"` → `"1.2.3"`; `"1.2.3"` → `"1.2.3"`; `"1.2.3-beta.1+sha"` → `"1.2.3-beta.1"`; `null` → `"0.0.0"`; `""` → `"0.0.0"`. |
| Parse-error translation (only if implemented as a separate function) | Framework error for unknown token `--x` → `Unknown option '--x'`; for `x` → `Unknown command 'x'`; missing command → usage. |

Everything user-visible stays in the scenarios.

## 5. Step Order

1. **Feature files.** Add `GlobalOptions.feature` (2.2). Run `dotnet test -c Release`: new scenarios red (undefined steps), existing green.
2. **Fix criteria.** Create `docs/adr/0002-cli-framework.md` with status `Proposed`, Context, Options (A System.CommandLine 2.0.12, B Spectre.Console.Cli 0.57.2, C keep hand-written parser), and the criteria from 3.3. Commit before measuring, so the criteria provably precede the data.
3. **Prototypes.** Create `experiments/cli-framework/` (3.2): props files, `.gitignore`, Baseline, then SystemCommandLine, then SpectreCli. Confirm `dotnet build shelf.slnx` at root does not touch them.
4. **Measure.** Write and run `measure.sh` on `osx-arm64`. Keep publish logs and results locally (git-ignored).
5. **Decide.** Fill the ADR: environment, exact commands, results table (size, size overhead, median/p95/min/max per command, overhead, warning counts by code, functional check result), gate results, scores, decision, consequences (including follow-ups: CI cold start timing for the "Cold start measurement" work package, shell completion approach for M5, re-check trigger for System.CommandLine 3.0 / Spectre 1.0). If a gate fails for every candidate or an open question blocks, stop and ask. Otherwise set status `Accepted`.
6. **Package.** Add the winner to `Directory.Packages.props` and `Shelf.Cli.csproj`.
7. **Unit test project.** Create `tests/Shelf.Cli.Tests`, add to `shelf.slnx`. Write `CliEnvironmentTests`, `VersionInfoTests` (red), then add `CliEnvironment.cs`, `VersionInfo.cs`, `ExitCodes.cs` (green).
8. **Integration.** Rewrite `ShelfApp.cs` on the framework, add `GlobalOptions.cs`, update `Program.cs`. Extend `CliSteps.cs` with the steps from 2.3 alongside. Iterate until all scenarios (old and new) are green.
9. **Locale check.** Run `LANG=de_DE.UTF-8 LC_ALL=de_DE.UTF-8 dotnet test -c Release` once; texts must be identical. Apply `SatelliteResourceLanguages` (3.6) if needed.
10. **Quality gates** (all must pass):
    - `dotnet format --verify-no-changes`
    - `dotnet build -c Release` (warnings as errors; no `NoWarn` for `IL*` in `src/`; `IsAotCompatible=true` still on `Shelf.Cli`)
    - `dotnet test --no-build -c Release`: all green
    - `dotnet publish src/Shelf.Cli/Shelf.Cli.csproj -c Release -r osx-arm64 -p:PublishAot=true -o <scratch>`: zero warnings; then the CI smoke check by hand: binary exists and is executable, no `shelf.dll` and no culture subfolders in the output, `--version` exits 0 and matches `^shelf [0-9]+\.[0-9]+\.[0-9]+`, no arguments exits 2. Run `NO_COLOR=1 script -q /dev/null <bin> --help` and confirm no ESC byte. Record the binary size and a `hyperfine -N` run of `--version` in the PR description.
11. **Roadmap.** Mark the "CLI framework decision" work package done with a link to ADR 0002.
12. Push, open the PR; all `ci` checks green on four platforms.

## 6. Open Questions

- **Q1: Scoring bands and weights.** The bands in 3.3 (for example "≤ 10 ms overhead = 5 points") are a proposal, not from the PRD. Confirm or adjust before step 4.
- **Q2: Suppressing framework trim/AOT warnings.** Gate G1 forbids it. Should a candidate that only passes with targeted suppressions (and passes G2 at runtime) be allowed? Recommendation: no, because shelf's policy is warnings as errors and a suppressed warning hides runtime breakage after library upgrades.
- **Q3: No candidate passes all gates** (for example both above 150 ms p95, or both with warnings). Options: option C (hand-written parser), accept the least-bad candidate with ReadyToRun fallback, or relax a gate. Owner decides; the implementer stops.
- **Q4: Message texts.** `ExitCodes.feature` asserts `Usage: shelf` as the first characters of `--help` stdout and specific error texts (m0-skeleton Q3). Both frameworks print different native help (System.CommandLine starts with `Description:`/`Usage:`, Spectre with `USAGE:`; to verify). Recommendation: keep the error texts (own rendering is needed anyway for exit 2), and allow the help assertion to become `standard output contains "Usage:"` if a framework-native help cannot start with `Usage: shelf` without rewriting the help renderer. Needs owner approval before editing that scenario.
- **Q5: TOML parser.** The roadmap exit criterion groups it with this decision, but the work package line names only the CLI framework. Recommendation: a separate slice `m0-toml-parser` with its own ADR 0003, because it needs its own license/maintenance check (ADR 0001 excluded it, Q5 there), its own AOT check, and its first consumer is M1 configuration. Confirm.
- **Q6: `--json` on `--version`/`--help`.** PRD 7.1 says every command supports `--json`, but does not define JSON for version or help (for example `{"version":"1.0.0"}`). This slice only makes `--json` a recognized global option. Decide now or in M1?
- **Q7: Keep `experiments/cli-framework/` after the decision?** Recommendation: keep it until M0 closes (the ADR references it), then delete it with a note in the ADR pointing to the commit.
- **Q8: Exit code 1 output.** PRD 7.1 defines exit 1 but not the error format: one-line message only, or stack trace with a `--verbose`/debug switch? The plan uses a one-line message on stderr; not covered by a scenario until a command can fail (M1).
- **Q9: Short aliases.** The PRD does not define `-h`, `-v`, `-?`. Frameworks add some by default (System.CommandLine: `-h`, `-?`; Spectre: `-h`, `-v`). Keep the framework defaults, or restrict to the long forms? No scenario asserts them in this slice.
- **Q10: CI cold start timing.** Decided "no" for this slice (3.2), handed to the "Cold start measurement" work package. Confirm.
