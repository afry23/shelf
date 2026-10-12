# ADR 0002: CLI framework

| | |
|---|---|
| **Status** | Accepted: System.CommandLine 2.0.12 |
| **Date** | 2026-10-12 |
| **Scope** | PRD 7.1 (CLI), 9.1 (cold start), 9.2 (Native AOT), 10.2 (candidates) |
| **Roadmap** | M0 work package "CLI framework decision" |
| **Inputs** | [ADR 0001](0001-dependency-licenses-and-maintenance.md) rows 2 and 3, slice plan `docs/slices/m0-cli-framework.md` |

## Context

shelf needs a CLI framework for about 12 commands with nested subcommands (`tag add|rm|list|rename|merge`), a global `--json` option, exit codes 0/1/2, `NO_COLOR`, and shell completion (M5). It must work in a Native AOT single binary (PRD 9.2) and start in under 150 ms (PRD 9.1). License and maintenance of both candidates were checked in ADR 0001 (both MIT, both `ok`).

## Options

- **A. System.CommandLine 2.0.12** (Microsoft, MIT; ADR 0001 row 3; AOT claim `IsAotCompatible=true` at `main`, not verified at the tag).
- **B. Spectre.Console.Cli 0.57.2** (MIT; ADR 0001 row 2; documents `IsAotCompatible=false`).
- **C. Keep the hand-written parser** (the framework-free baseline prototype). Documented as the fallback if no framework passes all gates.

Newer prereleases (System.CommandLine 3.0.0-rc.1) are not considered.

## Criteria (fixed before measuring)

Weights, bands and the strict reading of G1 (no suppressions) were confirmed by the project owner (slice questions Q1 and Q2) before the measurements were taken. The criteria were not committed separately before the prototypes were built, so there is no commit that proves the order; this section was written with status `Proposed` and the status changed to `Accepted` after the measurements and the decision.

### Gates (a candidate that fails a gate is out, regardless of score)

| ID | Gate |
|---|---|
| G1 | AOT publish with **zero** `IL*`/AOT warnings from the framework, with no suppression (no `NoWarn`, no `SuppressTrimAnalysisWarnings`). Decided by the project owner: a candidate that only passes with suppressed warnings is not accepted. |
| G2 | All functional checks pass on the **AOT binary**. |
| G3 | In-process invocation with caller-supplied stdout/stderr writers, no write to static `Console`/`AnsiConsole`, usage errors mappable to exit 2 on stderr. Extra glue code is allowed; its amount is recorded. |
| G4 | `--version` p95 on `osx-arm64` below 150 ms (PRD 9.1). |

If no candidate passes all gates, the decision is escalated to the project owner and nothing is integrated.

### Weighted score (1 to 5 per criterion; weights and bands confirmed by the project owner)

| Criterion | Weight | Scoring |
|---|---|---|
| Cold start overhead vs. baseline (`--version` and `greet`, median) | 30 % | <= 10 ms: 5, <= 20 ms: 4, <= 35 ms: 3, <= 50 ms: 2, more: 1 |
| PRD 7.1 fit: 1 point each for global option, NO_COLOR controllable via injected setting, version text controllable (no entry-assembly lookup), exit codes fully controllable, completion generation for bash/zsh/fish without external tool | 20 % | points = score |
| Binary size overhead vs. baseline | 15 % | <= 1 MB: 5, <= 2 MB: 4, <= 4 MB: 3, <= 8 MB: 2, more: 1 (MB = 10^6 bytes) |
| API ergonomics for about 12 commands incl. nested `tag` subcommands | 15 % | Judgement, justified: glue code in the prototype, type safety of binding, how commands are added |
| Maintenance and stability (ADR 0001 data) | 10 % | Judgement |
| Testability (glue needed for G3, determinism across locales) | 10 % | Judgement |

### Benchmark method

Prototypes in `experiments/cli-framework/` (outside `shelf.slnx`, not built by CI): `Baseline` (hand-written), `SystemCommandLine`, `SpectreCli`, all with the same command surface (`--version`, `--help`, `greet <name> [--times n]`, `tag add <name>`, global `--json`, usage errors exit 2) and publishing Native AOT for `osx-arm64`. `measure.sh` publishes with `-p:TrimmerSingleWarn=false`, counts `IL*`/`AOT` warnings, runs the functional checks on the AOT binary, records binary size and output directory contents, and measures cold start (50 runs after 5 warmups). Numbers are local `osx-arm64` only; CI timing is owned by the roadmap work package "Cold start measurement".

## Measurements

Reproduce: `experiments/cli-framework/measure.sh` (publishes each prototype with `dotnet publish <p> -c Release -r osx-arm64 -p:TrimmerSingleWarn=false`, no suppressions). The prototypes stay in the repository until M0 closes and are then deleted (see Consequences).

**Environment:** 2026-10-12, macOS 26.6.2 arm64, Apple M4, 24 GiB RAM, .NET SDK 10.0.401, System.CommandLine 2.0.12, Spectre.Console.Cli 0.57.2 (transitively Spectre.Console). Machine on **battery power** (not AC) with other user sessions logged in; absolute numbers are therefore indicative, and the differences between prototypes are what counts. `hyperfine` was not installed and no global tools may be installed, so the documented fallback was used: a zsh loop with `$EPOCHREALTIME`, 5 warmup runs, 50 measured runs, stdout to `/dev/null`, process start to exit (includes `fork`/`exec`).

### Results (osx-arm64, Native AOT)

| | Baseline (hand-written) | System.CommandLine 2.0.12 | Spectre.Console.Cli 0.57.2 |
|---|---|---|---|
| AOT/trim warnings (distinct lines, G1) | 0 | **0** | **26** (IL2026 x4, IL2067 x4, IL2070 x3, IL2072 x7, IL2075 x4, IL3000 x1, IL3050 x3) |
| Functional checks on the AOT binary (G2) | 10/10 binary checks pass; pty `NO_COLOR` check ok (217 bytes captured) | 10/10 binary checks pass; pty `NO_COLOR` check ok (297 bytes captured) | **0 of 10 binary checks pass**: every invocation aborts (exit 134) with `CommandRuntimeException: Could not get settings type for command of type 'GreetCommand'` during `AddCommand<T>`; pty check fails (exit 250, no output) |
| Binary size | 1,236,096 B | 3,061,672 B | 9,350,032 B |
| Size overhead vs. baseline | - | +1,825,576 B (1.83 MB) | +8,113,936 B (8.11 MB) |
| Output directory | binary + `.dSYM` | binary + `.dSYM` (no culture folders, no `.dll`) | binary + `.dSYM` |
| First run after publish (info) | 3.39 ms | 4.05 ms | n/a (crashes) |
| `--version` median / p95 / min / max (ms) | 3.15 / 3.45 / 2.98 / 3.59 | 3.52 / 3.67 / 3.37 / 3.80 | n/a (crashes) |
| `greet World --times 2` median / p95 / min / max (ms) | 3.32 / 3.56 / 3.11 / 3.72 | 3.76 / 4.21 / 3.52 / 4.25 | n/a (crashes) |
| `--help` median / p95 / min / max (ms) | 3.24 / 3.73 / 2.97 / 4.15 | 3.70 / 4.44 / 3.47 / 4.54 | n/a (crashes) |
| Overhead vs. baseline (`--version`, `greet`) | - | +0.37 ms, +0.44 ms | n/a |
| Lines in prototype `Program.cs` | 97 | 71 | 131 (incl. a hand-written `ITypeRegistrar`/`ITypeResolver`) |

Raw logs and per-run JSON are kept locally in `experiments/cli-framework/results/` (git-ignored); `results/measure.out` is the unedited output of `./measure.sh 2>&1 | tee results/measure.out` (run 2026-10-12T19:27Z, machine on battery power, not on AC) and contains every per-check line; the distinct warning lines are in `results/SpectreCli.warnings.txt`. Timing numbers vary from run to run (an earlier run gave 3.12 ms / 3.52 ms `--version` median for baseline / System.CommandLine); the differences are well below the band edges, so scores do not change. Sizes and warning counts are deterministic.

### Observations

- **Spectre.Console.Cli** is out for two independent reasons: 26 warnings from inside the framework (reflection-based settings binding, `TypeDescriptor.GetConverter`, `MakeGenericType`, and `RequiresDynamicCode` on the `CommandApp` constructor itself), and a hard runtime failure under AOT (the settings type cannot be discovered once the generic base types are trimmed). This matches its documented `IsAotCompatible=false` (ADR 0001 row 2). Making it work would require suppression and rooting settings types, which G1 forbids.
- **System.CommandLine 2.0.12** publishes with zero warnings and all functional checks pass on the AOT binary, so its `IsAotCompatible` claim holds at 2.0.12 (open point of ADR 0001 row 3 is closed). The package ships a `net8.0` asset that is used on net10.0 without warnings.
- **Native help does not start with `Usage: shelf`.** The 2.0.12 help text starts with `Description:` (if the command has a description) followed by `Usage:` and the usage on the next line. `HelpBuilder`, `HelpContext` and `HelpAction.Builder` appear in the XML documentation but are internal in the 2.0.12 assembly, so the layout cannot be customized without replacing the help action, that is, writing an own help renderer. Decision Q4 of the slice: the `ExitCodes.feature` help scenario was relaxed from `starts with "Usage: shelf"` to `contains "Usage:"` plus `contains "shelf [options]"`. Error and usage texts on standard error (`Usage: shelf <command> [options]`, `Unknown option '...'`, `Unknown command '...'`) are rendered by shelf and unchanged.
- **Application name.** `RootCommand` takes its name from the process path (in specs: the test host) and the name is read-only. `ShelfApp` therefore uses a plain `Command("shelf")` as the root and adds `HelpOption` itself. This was found during integration, not in the prototype (the prototype's help shows `proto-scl`).
- **`VersionOption` does not run when combined with other tokens** (`--version --json` fell through to the root action). `ShelfApp` therefore defines `--version` as an ordinary option with an own action. This also makes the version text independent of the entry assembly.
- **Locale.** The package contains satellite resources for ten languages. Under `LANG=de_DE.UTF-8` the help rendered `Nutzung` instead of `Usage` in the test projects until `SatelliteResourceLanguages=en` was also set on the test projects (a project reference flows the package's satellite assemblies into the referencing project's output). With it set in `Shelf.Cli`, `Shelf.Specs` and `Shelf.Cli.Tests`, the German run is identical to the English run (37/37) and the AOT publish has no culture folders.
- **`NO_COLOR` check on a pseudo-terminal.** A first version of the check used `script`, which could not allocate a pty in this environment (`tcgetattr/ioctl: Operation not supported on socket`) and so passed vacuously; that was found in review and fixed. The check now uses `experiments/cli-framework/pty-run.py` (Python `os.openpty`), which allocates a real pty here, and the script reports `NOT RUN` instead of `ok` if no pty is available. Recorded result: with `NO_COLOR=1` and stdout on a real pty, `--help` of the baseline and of the System.CommandLine prototype contains only the runtime's own `ESC[?1h ESC=` (keypad mode) and no other ESC byte. Those two sequences are written by the .NET runtime itself, even for the baseline without any framework; they are not color codes and are ignored by the check. So the claim that System.CommandLine's help emits no ANSI color codes in 2.0.12 is evidenced for the AOT prototype on this machine; it was not checked for the integrated `shelf` binary, whose NO_COLOR scenarios are intentionally weak until M1 introduces color (`CliEnvironment.UseColor`). To re-run on any machine, run `experiments/cli-framework/measure.sh` in a real terminal.
- **Shell completion.** System.CommandLine 2.0.12 provides completion data (`[suggest]` directive, `GetCompletions`) for use with the external `dotnet-suggest` tool (the plain `Command("shelf")` root used by `ShelfApp` has no directives configured, so `[suggest]` does not work there; relevant for M5), but no script generation for bash/zsh/fish. Spectre.Console.Cli: no completion generator known (not verified, candidate is out). Completion scripts are an M5 topic and will need to be generated by shelf from the command model or via `GetCompletions`.

## Gate results

| Gate | System.CommandLine | Spectre.Console.Cli | Baseline (option C) |
|---|---|---|---|
| G1 zero AOT/trim warnings | pass (0) | **fail (26)** | pass (0) |
| G2 functional on AOT binary | pass (10/10 binary checks; pty NO_COLOR check ok) | **fail** (0/10 binary checks; pty check fails) | pass (10/10; pty check ok) |
| G3 in-process, own writers, exit 2 mappable | pass (`InvocationConfiguration.Output/Error`; parse errors rendered by shelf) | not evaluated | pass |
| G4 `--version` p95 < 150 ms | pass (3.67 ms, AOT prototype) | not measurable | pass |

## Scores (candidates that pass all gates)

| Criterion (weight) | System.CommandLine | Basis |
|---|---|---|
| Cold start overhead (30 %) | 5 | +0.37 / +0.44 ms |
| PRD 7.1 fit (20 %) | 4 | Recursive global option, version text controllable, exit codes controllable, `NO_COLOR` handled by shelf via injected `CliEnvironment` (framework has no color logic); no completion script generation (0 points) |
| Binary size overhead (15 %) | 4 | +1.83 MB |
| API ergonomics (15 %) | 4 | Smallest prototype (71 lines vs. 97 hand-written); typed options and arguments, subcommands by `Add`, no reflection or registrar; deducted for internal help builder and the root-name/`VersionOption` surprises |
| Maintenance (10 %) | 4 | Microsoft, MIT, 2.0 GA (ADR 0001 row 3); 3.0.0-rc.1 exists, so a major bump is to be expected |
| Testability (10 %) | 5 | Output/error writers injected, no static console access, parse and invoke are separate steps |
| **Weighted total** | **4.4 / 5** | 0.30 x 5 + 0.20 x 4 + 0.15 x 4 + 0.15 x 4 + 0.10 x 4 + 0.10 x 5 |

Spectre.Console.Cli is not scored: it fails G1 and G2. Indicative only: size overhead 8.11 MB would be band 1.

## Decision

**Option A: System.CommandLine 2.0.12.** It is the only framework candidate that passes all four gates, with zero AOT warnings and no suppression, and a total score of 4.4. Option B fails G1 (26 warnings) and G2 (aborts at startup under AOT). Option C (hand-written parser) passes the gates but means writing and maintaining help, subcommand routing, option binding and later completion for about 12 commands; the measured framework cost (about 0.4 ms, +1.8 MB) does not justify that.

Details decided in this slice:

- **`--version --json`** prints a single JSON object on standard output, `{"version":"X.Y.Z"}` (same version as the plain output, without `+commit`), exit 0. **`--help` is not affected by `--json`**: help stays human-readable text, exit 0. PRD 7.1 does not define this case; this is a decision made in this slice, not a PRD requirement, and it is covered by scenarios in `GlobalOptions.feature`.
- **Help text** is the framework-native help (see Observations); the help scenario asserts both `contains "Usage:"` and `contains "shelf [options]"`.
- **Exit codes:** parse errors and a missing command exit 2 with own texts on stderr; an unhandled exception in a command exits 1 with the one-line message `error: <message>` on stderr (no stack trace; not yet covered by a scenario because no command can fail).
- **Short aliases:** the framework defaults are kept. System.CommandLine 2.0.12 provides `-h` and `-?` for `--help`; it provides no short alias for `--version` (no `-v`).
- **Package version:** the ADR 0001 version 2.0.12 is pinned in `Directory.Packages.props`. System.CommandLine 3.0.0-rc.1 was not considered (prerelease).

## Consequences

- `src/Shelf.Cli` references System.CommandLine 2.0.12. `<SatelliteResourceLanguages>en</SatelliteResourceLanguages>` is set in `Shelf.Cli`, `Shelf.Specs` and `Shelf.Cli.Tests`; new test projects that reference `Shelf.Cli` need it too.
- Commands added in M1 use `Command`/`Option<T>`/`Argument<T>` with `SetAction`. Per-command help is native System.CommandLine help (also not starting with `Usage: shelf`). If a custom help layout is ever required, it needs an own `HelpAction` or a newer framework version.
- The AOT binary on osx-arm64 is 3.06 MB with the framework; the `--version` median is about 3.5 ms. CI cold-start timing on all four platforms is left to the roadmap work package "Cold start measurement". Numbers here are osx-arm64 only.
- **Re-check triggers:** System.CommandLine 3.0 stable (API changes; internal help builder may become public), Spectre.Console.Cli 1.0 if it claims AOT support.
- **Shell completion** (M5): generate scripts in shelf, using `Command.GetCompletions` or the command model; no framework feature is available for bash/zsh/fish.
- **Follow-up M1:** when real commands exist, print the framework's parse error message (e.g. missing argument, invalid value) before the usage line, with a scenario.
- **Follow-ups:** TOML parser selection is a separate slice (ADR 0003). `experiments/cli-framework/` is kept until M0 closes and is to be deleted afterwards, with the ADR pointing to the last commit that contains it.
