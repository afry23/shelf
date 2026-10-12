# Slice m0-skeleton: Solution Skeleton and BDD Setup

| | |
|---|---|
| **Slice ID** | `m0-skeleton` |
| **Branch** | `feat/m0-skeleton` |
| **Milestone** | M0 – Setup & Spikes |
| **Roadmap work packages** | "Solution skeleton", "BDD setup" (roadmap 3, M0) |
| **PRD references** | 10.1 (project structure), 10.2 (BDD: Reqnroll), 7.1 (exit codes, used by the template feature) |

---

## 1. Scope

**In scope**

- Solution file `shelf.slnx` at the repo root.
- Move the root `shelf.csproj` / `Program.cs` into `src/Shelf.Cli/`. The root must not contain a project file afterwards (otherwise `dotnet restore` at the root fails with MSB1011, and the root project would glob-include every `.cs` under `src/` and `tests/`).
- Minimal project set from PRD 10.1: `src/Shelf.Core`, `src/Shelf.Cli`, `tests/Shelf.Specs`.
- Central package management: `Directory.Packages.props`.
- Extend `Directory.Build.props` with shared defaults (target framework, nullable, implicit usings).
- `global.json` pinning the .NET 10 SDK.
- Reqnroll test project `tests/Shelf.Specs` on **xUnit v3** (justification in 3.4).
- A first template feature file (green), tagged `@PRD-7.1 @M0` (see open question Q1).
- `.github/workflows/build.yml`: add `dotnet test` so CI fails on red scenarios.

**Requirement covered by the template feature:** PRD 7.1, "Exit codes: 0 = success, 1 = error, 2 = usage error" (success and usage-error parts only). There is no `F-*` requirement for M0.

**Out of scope (later slices or other M0 work packages)**

- The other 10.1 projects (`Shelf.Extraction`, `Shelf.Index`, `Shelf.Semantic`, `Shelf.Organize`, `Shelf.Mcp`, `Shelf.Web`, `Shelf.Web.Ui`). Each is created by the first slice that needs it.
- `tests/Shelf.*.Tests` unit test projects (no internal logic in this slice, see 4).
- `tests/fixtures/` and the synthetic library generator (work package "Test fixtures").
- CLI framework decision (Spectre.Console.Cli vs. System.CommandLine). This slice uses a hand-written argument check in `ShelfApp` that will be replaced.
- `PublishAot`, AOT publish job, multi-platform CI matrix (work packages "CI" and "AOT spike").
- Exit code 1 (runtime error): nothing in this slice can fail at runtime.
- `--json`, `NO_COLOR`, shell completion, logging setup.
- `.editorconfig` / code style rules.

## 2. Feature Files

### `tests/Shelf.Specs/Features/Cli/ExitCodes.feature`

```gherkin
# Template feature file. Conventions (see CLAUDE.md):
# - English only.
# - Tag every feature or scenario with the PRD requirement ID and the milestone.
# - Back every scenario with concrete examples.
# - Express variations as a Scenario Outline with an Examples table.
@PRD-7.1 @M0
Feature: CLI exit codes
  As a user or a script calling shelf
  I want predictable exit codes and output streams
  So that I can tell success from usage errors

  Scenario: Printing the version succeeds
    When I run shelf with arguments "--version"
    Then the exit code is 0
    And standard output matches the pattern "^shelf \d+\.\d+\.\d+"
    And standard error is empty

  Scenario: Printing the help succeeds
    When I run shelf with arguments "--help"
    Then the exit code is 0
    And standard output starts with "Usage: shelf"
    And standard error is empty

  Scenario Outline: Usage errors exit with code 2 and write to standard error
    When I run shelf with arguments "<arguments>"
    Then the exit code is 2
    And standard output is empty
    And standard error starts with "<message>"

    Examples:
      | arguments    | message                       |
      |              | Usage: shelf                  |
      | frobnicate   | Unknown command 'frobnicate'  |
      | --frobnicate | Unknown option '--frobnicate' |
```

Notes for the implementer:

- Do not put regexes into `Examples` cells: Gherkin applies escape handling to table cells (`\|`, `\\`, `\n`), and cells are trimmed. That is why the version pattern lives in step text, not in a table.
- The empty `arguments` cell arrives as `""` and means "no arguments".

## 3. Design

### 3.1 Target file layout

```
shelf.slnx                         NEW   solution (src/ and tests/ solution folders)
global.json                        NEW
Directory.Build.props              CHANGE
Directory.Packages.props           NEW
shelf.csproj                       DELETE (git mv to src/Shelf.Cli/Shelf.Cli.csproj, then edit)
Program.cs                         DELETE (git mv to src/Shelf.Cli/Program.cs, then edit)
src/Shelf.Core/Shelf.Core.csproj   NEW   class library, no code yet
src/Shelf.Cli/Shelf.Cli.csproj     NEW   (moved) Exe, AssemblyName "shelf"
src/Shelf.Cli/Program.cs           CHANGE
src/Shelf.Cli/ShelfApp.cs          NEW
tests/Shelf.Specs/Shelf.Specs.csproj               NEW
tests/Shelf.Specs/reqnroll.json                    NEW
tests/Shelf.Specs/Features/Cli/ExitCodes.feature   NEW
tests/Shelf.Specs/Steps/CliSteps.cs                NEW
.github/workflows/build.yml        CHANGE
```

Also delete the untracked root `bin/` and `obj/` folders locally (left over from the root project; they are git-ignored).

### 3.2 Build configuration

**`global.json`**

```json
{
  "sdk": { "version": "10.0.100", "rollForward": "latestFeature" },
  "test": { "runner": "Microsoft.Testing.Platform" }
}
```

Deviation (implemented): the `test.runner` entry opts `dotnet test` into Microsoft.Testing.Platform (MTP). xunit.v3 4.x errors in VSTest mode on the .NET 10 SDK.

**`Directory.Build.props`** (keep the two existing properties, add):

- `TargetFramework` = `net10.0`
- `Nullable` = `enable`
- `ImplicitUsings` = `enable`
- `LangVersion` = `latest`

Do **not** put `IsAotCompatible` here; it would also apply to the test project, which uses reflection (Reqnroll, xUnit).

**`Directory.Packages.props`**

- `ManagePackageVersionsCentrally` = `true`
- `PackageVersion` entries (latest stable at implementation time; verify each supports `net10.0`):
  - `Reqnroll.xunit.v3` (Reqnroll 3.x)
  - `xunit.v3`
  - Not used (deviation, MTP mode): `xunit.runner.visualstudio` and `Microsoft.NET.Test.Sdk`.
- No assertion library. Use xUnit's `Assert` (avoids the FluentAssertions license issue and keeps dependencies minimal).

### 3.3 Projects

**`src/Shelf.Core`**: class library, `IsAotCompatible=true`. No source files in this slice (an empty library builds). Exists so the dependency direction `Cli -> Core` is fixed from the start.

**`src/Shelf.Cli`**:

- `OutputType=Exe`, `AssemblyName=shelf`, `RootNamespace=Shelf.Cli`, `IsAotCompatible=true`.
- `ProjectReference` to `Shelf.Core`.
- `Program.cs` (top-level statements): `return ShelfApp.Run(args, Console.Out, Console.Error);`
- `ShelfApp.cs`:

  ```csharp
  public static class ShelfApp
  {
      public static int Run(string[] args, TextWriter stdout, TextWriter stderr);
  }
  ```

  Behavior (temporary, replaced by the chosen CLI framework later; the exit codes and stream rules stay):

  | Input | Exit | stdout | stderr |
  |---|---|---|---|
  | `--version` | 0 | `shelf <version>` + newline | – |
  | `--help` | 0 | usage text, first line `Usage: shelf <command> [options]` | – |
  | no arguments | 2 | – | usage text (same as `--help`) |
  | first arg starts with `-`, unknown | 2 | – | `Unknown option '<arg>'` + newline, then usage |
  | any other first arg | 2 | – | `Unknown command '<arg>'` + newline, then usage |

  `<version>` is read from `typeof(ShelfApp).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()`, with the `+<commit>` suffix cut off. Do **not** use `Assembly.GetEntryAssembly()`: in the specs the entry assembly is the test host, and the attribute approach is AOT-safe.

  `ShelfApp` must never write to `Console` directly. The specs call it in-process and in parallel; redirecting the static `Console` would race.

**`tests/Shelf.Specs`**:

- `OutputType=Exe` (required by xUnit v3), `IsPackable=false`.
- `PackageReference`s (no versions): `Reqnroll.xunit.v3`, `xunit.v3` (no `xunit.runner.visualstudio` or `Microsoft.NET.Test.Sdk`; see 3.4).
- `ProjectReference` to `src/Shelf.Cli`.
- `reqnroll.json`: `{ "$schema": "https://schemas.reqnroll.net/reqnroll-config-latest.json", "language": { "feature": "en" } }`.
- `Steps/CliSteps.cs`: `[Binding]` class, one instance per scenario (Reqnroll default). Holds `int exitCode`, `StringWriter stdout`, `StringWriter stderr`.
  - `When I run shelf with arguments "(.*)"`: split on `' '` with `RemoveEmptyEntries`, call `ShelfApp.Run`. No quoting support needed yet.
  - `Then the exit code is (\d+)`
  - `Then standard output matches the pattern "(.*)"` (`Regex.IsMatch`)
  - `Then standard output starts with "(.*)"` / `Then standard error starts with "(.*)"` (ordinal)
  - `Then standard output is empty` / `Then standard error is empty`
  - Prefer Cucumber expressions or regex consistently; pick one style for the whole project and use it here as the template.

**`shelf.slnx`**: create with `dotnet new sln --format slnx -n shelf`, then `dotnet sln add` the three projects (solution folders `src` and `tests`).

### 3.4 Test runner choice: xUnit v3

- Reqnroll 3 ships a first-party `Reqnroll.xunit.v3` plugin. xUnit v3 is the current xUnit line and runs on .NET 10.
- TUnit: Reqnroll integration is not a first-party, mature plugin. NUnit/MSTest would work but bring no advantage here.
- Deviation: run via Microsoft.Testing.Platform, enabled in `global.json` (`"test": {"runner": "Microsoft.Testing.Platform"}`). Reason: xunit.v3 4.x errors in VSTest mode on the .NET 10 SDK. `Microsoft.NET.Test.Sdk` and `xunit.runner.visualstudio` are therefore not used. Plain `dotnet test` still works on the `.slnx`.
- Verified: an unbound step fails the run under MTP (exit code 2); no extra Reqnroll config needed.
- Reqnroll-generated `*.feature.cs` files are git-ignored and regenerated on build.
- Fallback if `Reqnroll.xunit.v3` fails on .NET 10: `Reqnroll.NUnit`. Document the switch in the slice PR.

### 3.5 CI (`.github/workflows/build.yml`)

Append after the build step:

```yaml
      - run: dotnet test --no-build -c Release
```

`dotnet test` returns non-zero on any failed scenario, which fails the job. Undefined steps also fail under Reqnroll's default `missingOrPendingStepsOutcome` (Pending is reported as failure in xUnit); verify this once by temporarily adding an unbound step locally.

The `paths-ignore: '**/*.feature'` entry conflicts with BDD: a PR that only changes feature files would skip CI, although feature files are compiled into tests. See Q2.

### 3.6 AOT and native-dependency risks

- No native dependencies in this slice.
- `IsAotCompatible=true` on `Shelf.Core` and `Shelf.Cli` turns on trim/AOT analyzers now; with `TreatWarningsAsErrors` any reflection use fails the build early. `ShelfApp` uses only an assembly attribute lookup, which is AOT-safe.
- `PublishAot` is **not** set in this slice. Risk for the AOT slice: a test project referencing an Exe project that becomes self-contained can fail with NETSDK1150/NETSDK1151. Mitigations then: move `ShelfApp` into a library project, or set `ValidateExecutableReferencesMatchSelfContained=false` in the specs project.
- Reqnroll and xUnit use reflection, but only in the test project, which is never AOT-published.
- `TreatWarningsAsErrors` also applies to Reqnroll's generated code-behind. If generated files raise warnings (for example nullable warnings), suppress the specific warning IDs in `Shelf.Specs.csproj` via `NoWarn`. Do not turn off `TreatWarningsAsErrors`.

## 4. Unit Tests

None in this slice. `ShelfApp` is a temporary argument check, fully covered by the scenarios, and will be replaced by the CLI framework. No `tests/Shelf.*.Tests` project is created yet.

## 5. Step Order

1. Delete the local root `bin/` and `obj/`. `git mv shelf.csproj src/Shelf.Cli/Shelf.Cli.csproj` and `git mv Program.cs src/Shelf.Cli/Program.cs`.
2. Add `global.json`, extend `Directory.Build.props`, add `Directory.Packages.props`. Strip properties now inherited from `Directory.Build.props` out of `Shelf.Cli.csproj`; set `AssemblyName`, `RootNamespace`, `IsAotCompatible`.
3. Create `src/Shelf.Core`, add a `ProjectReference` from `Shelf.Cli`.
4. Create `shelf.slnx` and add both projects. `dotnet build -c Release` is green (`Program.cs` still prints "Hello, World!").
5. Create `tests/Shelf.Specs` (csproj, `reqnroll.json`), add it to the solution.
6. **Write `Features/Cli/ExitCodes.feature`** exactly as in section 2. `dotnet test`: scenarios fail (undefined steps). This is the expected red state.
7. Add `ShelfApp.cs` and update `Program.cs`, together with `Steps/CliSteps.cs`. Iterate until `dotnet test -c Release` is fully green.
8. Check that an unbound step makes `dotnet test` fail (temporarily add one; do not commit it).
9. Run `dotnet run --project src/Shelf.Cli -- --version`, then run without arguments and check `echo $?` is `2`.
10. Update `.github/workflows/build.yml` with the `dotnet test` step (and the `paths-ignore` change if Q2 is decided).
11. Final check from a clean clone: `dotnet restore && dotnet build --no-restore -c Release && dotnet test --no-build -c Release`.

## 6. Open Questions

- **Q1: Tag for M0 work without an `F-*` ID.** CLAUDE.md requires a PRD requirement ID, but the PRD has no `F-*` ID for M0 or for the CLI exit codes. The plan uses a section tag, `@PRD-7.1`. Should that be the convention for non-`F-*` requirements (for example `@PRD-9.3` for data safety), or should the PRD assign IDs to the 7.x/9.x requirements?
- **Q2: `paths-ignore: '**/*.feature'` in `build.yml`.** This skips CI for feature-file-only changes, so changed scenarios are not verified. The recommendation is to remove `**/*.feature` from both `paths-ignore` lists. Confirm before changing it. Related: if build is a required status check, any ignored path leaves PRs waiting for a check that never runs (existing issue, not caused by this slice).
- **Q3: Message texts in the template feature.** `Unknown command '...'`, `Unknown option '...'`, and `Usage: shelf` are invented for this slice. The chosen CLI framework may print different texts. Is it OK to change these scenarios' message assertions when the framework is adopted? Exit codes and streams must stay.
- **Q4: Version source.** Where should the version shown by `--version` come from (MSBuild `Version` in `Directory.Build.props`, git tags via a tool such as MinVer, or CI `-p:Version`)? This slice leaves the SDK default (`1.0.0`) and the scenario only checks the `shelf X.Y.Z` shape.
- **Q5: Empty projects.** Roadmap M0 says "projects as in 10.1". This plan creates only `Shelf.Core` and `Shelf.Cli` and leaves the other projects to the slices that need them. Confirm, or create all 10.1 projects as empty shells now.
