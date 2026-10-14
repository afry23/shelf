# Slice m0-toml-parser: TOML Parser Decision and Minimal Config Reader

| | |
|---|---|
| **Slice ID** | `m0-toml-parser` |
| **Branch** | `feat/m0-toml-parser` |
| **Milestone** | M0 – Setup & Spikes |
| **Roadmap** | Exit criterion "CLI framework and TOML parser are chosen" (TOML part); TOML parser line of the M0 "AOT spike" work package (osx-arm64 part only, see 3.6) |
| **PRD references** | 9.2 (config locations, Native AOT, single binary), F-GRP-6 (thresholds in `config.toml`; enabling only), 7.2 (`mcp.readOnly` as an example key) |
| **Inputs** | ADR 0001 (license policy, research method; TOML explicitly excluded there, Q5), ADR 0002 (gate format, benchmark method), `docs/slices/m0-cli-framework.md`, `experiments/cli-framework/` |

---

## 1. Scope

**In scope**

1. License and maintenance check of TOML parser candidates, using the ADR 0001 method and policy (GPL-3.0-or-later compatible; FSF list).
2. Reproducible AOT benchmark in `experiments/toml-parser/` (read path, plus a write/round-trip assessment that is recorded but not a gate).
3. `docs/adr/0003-toml-parser.md`: criteria fixed before measuring, measurements, decision, consequences.
4. Minimal integration of the winner into `src/Shelf.Core`: load one `config.toml` into a library-independent, immutable `ConfigDocument` with typed getters by dotted key; error reporting with file, line, column; resolution of the two config paths from PRD 9.2.
5. Feature files `ConfigFile.feature` and `ConfigLocations.feature`, step definitions, new unit test project `tests/Shelf.Core.Tests`.
6. Roadmap: note the TOML parser decision (link ADR 0003) in M0.

**Requirements covered:** PRD 9.2 bullet "Configuration: `~/.config/shelf/config.toml` (XDG) for global, `<root>/.shelf/config.toml` per library" (path resolution and reading one file). Milestone M0. F-GRP-6 is only enabled (the reader exists); thresholds themselves are M1 and are not tagged here.

**Out of scope**

- Any shelf config schema: key names, defaults, validation of unknown keys (Q6). Keys in the scenarios are test data, not a schema decision.
- Merging/precedence of global vs. per-library config, CLI options, environment variables (Q3).
- Writing config files, `shelf config` commands (Q2). Write capability is measured and documented only.
- Wiring config loading into `Shelf.Cli` (no command consumes config in M0).
- Library list for F-LIB-1 "multiple libraries via configuration" (Q11).
- AOT verification of the parser on `osx-x64`, `linux-x64`, `linux-arm64` (AOT spike work package, Q8). Functional (JIT) verification on all four platforms happens via the existing CI `test` job.
- Changes to `.github/workflows/build.yml`, `Directory.Build.props`, `global.json`, ADR 0001, ADR 0002.

## 2. Feature Files

Rules for the implementer:

- Write both files first. Run `dotnet test -c Release`: new scenarios must be red (undefined steps), existing ones green.
- The files are library-independent. If the chosen library cannot satisfy a row (e.g. reports a different line for a duplicate key), stop and raise it; do not edit the row silently.
- Rows marked `# Q4` / `# Q5` depend on an open question; keep them unless the owner decides otherwise.
- Gherkin table cells use `\\` for a literal backslash. Reqnroll substitutes `<placeholders>` inside doc strings of a `Scenario Outline`; verify this in step 1. If it does not, switch the content step to one row per line via a data table and tell the caller.

### 2.1 `tests/Shelf.Specs/Features/Configuration/ConfigFile.feature` (new)

```gherkin
@PRD-9.2 @M0
Feature: Reading a config.toml file
  As a user of shelf
  I want shelf to read settings from a TOML config file
  So that behavior such as grouping thresholds can be configured without code changes

  Background:
    Given an empty working directory

  Scenario Outline: Values of each scalar type are read by dotted key
    Given a config file "config.toml" with content:
      """
      title = "My Library"

      [grouping]
      auto_merge = 0.9
      review = 0.5
      max_hamming = 10

      [mcp]
      readOnly = true
      """
    When shelf loads the config file "config.toml"
    Then loading succeeds
    And the <type> value at "<key>" is "<value>"

    Examples:
      | key                  | type    | value      |
      | title                | string  | My Library |
      | grouping.auto_merge  | float   | 0.9        |
      | grouping.review      | float   | 0.5        |
      | grouping.max_hamming | integer | 10         |
      | mcp.readOnly         | boolean | true       |

  Scenario Outline: TOML syntax variants yield the same value
    Given a config file "config.toml" with content:
      """
      <line 1>
      <line 2>
      """
    When shelf loads the config file "config.toml"
    Then loading succeeds
    And the <type> value at "<key>" is "<value>"

    Examples:
      | variant              | line 1                          | line 2            | key                 | type    | value         |
      | literal string       | name = 'single quoted'          |                   | name                | string  | single quoted |
      | unicode escape       | name = "caf\\u00e9"             |                   | name                | string  | café          |
      | table header         | [grouping]                      | auto_merge = 0.9  | grouping.auto_merge | float   | 0.9           |
      | dotted key           | grouping.auto_merge = 0.9       |                   | grouping.auto_merge | float   | 0.9           |
      | inline table         | grouping = { auto_merge = 0.9 } |                   | grouping.auto_merge | float   | 0.9           |
      | exponent             | [grouping]                      | auto_merge = 9e-1 | grouping.auto_merge | float   | 0.9           |
      | digit separators     | limit = 20_000                  |                   | limit               | integer | 20000         |
      | hexadecimal          | limit = 0x4E20                  |                   | limit               | integer | 20000         |
      | trailing comment     | flag = true # enabled           |                   | flag                | boolean | true          |
      | comment line         | # flag = true                   | flag = false      | flag                | boolean | false         |
      | quoted key           | "max chars" = 4000              |                   | max chars           | integer | 4000          |
      # Q5: an integer is accepted where a float is requested
      | integer as float     | threshold = 1                   |                   | threshold           | float   | 1             |

  Scenario Outline: A key that is not in the file has no value
    Given a config file "config.toml" with content:
      """
      [grouping]
      auto_merge = 0.9
      """
    When shelf loads the config file "config.toml"
    Then loading succeeds
    And there is no value at "<key>"

    Examples:
      | key                 |
      | grouping.review     |
      | search              |
      | search.limit        |

  Scenario Outline: An empty or comment-only file is a valid empty configuration
    Given a config file "config.toml" with content:
      """
      <line 1>
      """
    When shelf loads the config file "config.toml"
    Then loading succeeds
    And there is no value at "grouping.auto_merge"

    Examples:
      | line 1             |
      |                    |
      | # nothing set here |

  # Q4: a missing file is an empty configuration, not an error
  Scenario: A missing config file is an empty configuration
    When shelf loads the config file "config.toml"
    Then loading succeeds
    And there is no value at "grouping.auto_merge"

  Scenario Outline: Invalid TOML is rejected with file and line
    Given a config file "config.toml" with content:
      """
      <line 1>
      <line 2>
      <line 3>
      """
    When shelf loads the config file "config.toml"
    Then loading fails with an error at line <line>
    And the error message contains "config.toml:<line>:"

    Examples:
      | problem              | line 1          | line 2     | line 3 | line |
      | missing value        | a = 1           | b =        |        | 2    |
      | duplicate key        | [grouping]      | x = 1      | x = 2  | 3    |
      | duplicate table      | [grouping]      | [grouping] |        | 2    |
      | unterminated string  | name = "open    |            |        | 1    |
      | leading zero         | n = 01          |            |        | 1    |
      | two pairs on a line  | a = 1 b = 2     |            |        | 1    |

  Scenario Outline: Reading a value with the wrong type fails with the key and both types
    Given a config file "config.toml" with content:
      """
      [grouping]
      auto_merge = "high"
      max_hamming = 10
      enabled = true
      """
    When shelf loads the config file "config.toml"
    And shelf reads the <requested> value at "<key>"
    Then reading fails with an error containing "<key>: expected <requested>, found <actual>"

    Examples:
      | key                  | requested | actual  |
      | grouping.auto_merge  | float     | string  |
      | grouping.enabled     | integer   | boolean |
      | grouping.max_hamming | string    | integer |
      | grouping             | string    | table   |
```

### 2.2 `tests/Shelf.Specs/Features/Configuration/ConfigLocations.feature` (new)

```gherkin
@PRD-9.2 @M0
Feature: Locations of the config files
  As a user of shelf
  I want shelf to look for its config files in well-known places
  So that I know where to put global and per-library settings

  Background:
    Given the home directory is "/home/ada"

  Scenario: Without XDG_CONFIG_HOME the global config lives in ~/.config
    When shelf resolves the global config file path
    Then the path is "/home/ada/.config/shelf/config.toml"

  Scenario Outline: XDG_CONFIG_HOME is honored only when it is an absolute path
    Given the environment variable "XDG_CONFIG_HOME" is set to "<value>"
    When shelf resolves the global config file path
    Then the path is "<path>"

    Examples:
      | value        | path                                |
      | /xdg         | /xdg/shelf/config.toml              |
      | /xdg/        | /xdg/shelf/config.toml              |
      |              | /home/ada/.config/shelf/config.toml |
      | relative/dir | /home/ada/.config/shelf/config.toml |

  Scenario Outline: The per-library config lives in the library's .shelf folder
    When shelf resolves the library config file path for the root "<root>"
    Then the path is "<path>"

    Examples:
      | root            | path                                 |
      | /books          | /books/.shelf/config.toml            |
      | /books/         | /books/.shelf/config.toml            |
      | /Users/ada/Ebooks | /Users/ada/Ebooks/.shelf/config.toml |
```

Notes: paths are Unix-only (all four targets are Unix). XDG rule: empty or relative `XDG_CONFIG_HOME` is ignored (XDG Base Directory spec). macOS uses `~/.config` as PRD 9.2 states (Q7).

### 2.3 Step definitions

Shared state across `CliSteps` and the new classes goes through a context-injected POCO `ScenarioEnvironment` (`Dictionary<string,string> Variables`), because the step `the environment variable {string} is set to {string}` already exists in `CliSteps` and must not be bound twice. Refactor `CliSteps` to take it via constructor; behavior unchanged.

New file `tests/Shelf.Specs/Steps/ConfigSteps.cs` (Cucumber expressions, ordinal comparisons, `CultureInfo.InvariantCulture` for numbers):

| Step | Behavior |
|---|---|
| `Given an empty working directory` | Create a unique temp directory (`Path.Combine(Path.GetTempPath(), "shelf-specs", Guid.NewGuid().ToString("N"))`); delete it in `[AfterScenario]`. |
| `Given a config file {string} with content:` (doc string) | Write the text as UTF-8 without BOM, `\n` line endings. |
| `When shelf loads the config file {string}` | `ConfigFile.Load(path)`; store document or caught `ConfigException`. |
| `When shelf reads the {word} value at {string}` | Call the getter for `{word}` (`string`, `integer`, `float`, `boolean`); store caught `ConfigException`. |
| `Then loading succeeds` | No exception stored. |
| `Then loading fails with an error at line {int}` | `ConfigException.Line == expected`. |
| `Then the error message contains {string}` | Ordinal `Contains` on `Message`. |
| `Then reading fails with an error containing {string}` | Exception from the read step; ordinal `Contains`. |
| `Then the {word} value at {string} is {string}` | Getter result equals parsed expected value (`long.Parse`, `double.Parse` invariant, `bool.Parse`, string exact). |
| `Then there is no value at {string}` | `document.Contains(key) == false`. |

New file `tests/Shelf.Specs/Steps/ConfigLocationSteps.cs`:

| Step | Behavior |
|---|---|
| `Given the home directory is {string}` | Store. |
| `When shelf resolves the global config file path` | `ConfigPaths.Global(name => env.Variables.GetValueOrDefault(name), home)`. |
| `When shelf resolves the library config file path for the root {string}` | `ConfigPaths.Library(root)`. |
| `Then the path is {string}` | Ordinal equality. |

No step touches the process environment or `HOME` (specs run in parallel).

## 3. Design

### 3.1 Files

```
experiments/toml-parser/                            NEW  benchmark, not in shelf.slnx (3.3)
  Directory.Build.props, Directory.Packages.props   NEW  copy pattern of experiments/cli-framework
  .gitignore                                        NEW  bin/, obj/, out/, results/
  measure.sh                                        NEW
  sample/config.toml                                NEW  ~1 KB, with comments (timing + round-trip input)
  corpus/valid/*.toml, corpus/invalid/*.toml        NEW  hand-written (3.3)
  Baseline/        Program.cs, Baseline.csproj      NEW  reads file, no parser (size/time baseline)
  Tomlyn/ Tomlet/ CsToml/ Tommy/                    NEW  one prototype per candidate passing G1-G3 on paper
docs/adr/0003-toml-parser.md                        NEW
docs/roadmap.md                                     CHANGE  M0: TOML parser decided, link ADR 0003
Directory.Packages.props                            CHANGE  add winner's PackageVersion
src/Shelf.Core/Shelf.Core.csproj                    CHANGE  PackageReference, InternalsVisibleTo Shelf.Core.Tests
src/Shelf.Core/Configuration/ConfigDocument.cs      NEW
src/Shelf.Core/Configuration/ConfigValueKind.cs     NEW
src/Shelf.Core/Configuration/ConfigException.cs     NEW
src/Shelf.Core/Configuration/ConfigFile.cs          NEW
src/Shelf.Core/Configuration/ConfigPaths.cs         NEW
src/Shelf.Core/Configuration/TomlConfigParser.cs    NEW  internal; the only file that references the library
tests/Shelf.Specs/Shelf.Specs.csproj                CHANGE  explicit ProjectReference to Shelf.Core
tests/Shelf.Specs/Features/Configuration/*.feature  NEW  (2.1, 2.2)
tests/Shelf.Specs/Steps/ScenarioEnvironment.cs      NEW
tests/Shelf.Specs/Steps/CliSteps.cs                 CHANGE  use ScenarioEnvironment
tests/Shelf.Specs/Steps/ConfigSteps.cs              NEW
tests/Shelf.Specs/Steps/ConfigLocationSteps.cs      NEW
tests/Shelf.Core.Tests/Shelf.Core.Tests.csproj      NEW  xunit.v3, OutputType Exe (like Shelf.Cli.Tests)
tests/Shelf.Core.Tests/Configuration/*Tests.cs      NEW  (section 4)
shelf.slnx                                          CHANGE  add tests/Shelf.Core.Tests
```

### 3.2 Candidates (facts to verify; nothing below is verified)

Research each with the ADR 0001 method (nuspec, repo `LICENSE`, release list, commits last 6 months, contributors, project file at the release commit for `IsAotCompatible`/`IsTrimmable`, `.nupkg` listing for TFMs and native assets; read-only, nothing downloaded is executed). Record sources as S-numbers in ADR 0003 (own index). Also run a NuGet search for `toml`, screen the top 10 by downloads, and list every screened-out package with a one-line reason.

| Option | Package / repo | API shelf would use (read) | Write path to assess | To verify |
|---|---|---|---|---|
| A | `Tomlyn` (xoofx/Tomlyn) | Untyped table model or full-fidelity syntax tree (`Toml.Parse`); a source-generated serializer if the current version has one | Syntax tree keeps trivia/comments: edit + `ToString()` | License (expected BSD-2-Clause), whether the current major ships an AOT-safe/source-generated mode, whether the untyped model path is free of `RequiresUnreferencedCode` |
| B | `Tomlet` (SamboyCoding/Tomlet) | `TomlParser` → `TomlDocument` (untyped) | `TomlDocument` serialization; comments attached to values | License (expected MIT), AOT claim, reflection in the untyped path |
| C | `CsToml` (prozolic/CsToml) | Untyped `TomlDocument` or source-generated `[TomlSerializedObject]` types | Serializer; comment preservation | License (expected MIT), generator requirements, AOT claim |
| D | `Tommy` (dezhidki/Tommy) | `TOML.Parse(TextReader)` → `TomlTable` | `TomlTable.WriteTo`; comments partly kept | License (expected MIT), maintenance (likely flagged) |
| E | `Nett` | screening only | – | Expected out on maintenance (archived/unlisted?) |
| F | Hand-written TOML 1.0 subset parser in `Shelf.Core` | – | – | Documented fallback only, not prototyped unless all options fail (then Q, stop) |

Use the latest stable version of each. Prereleases are not considered (same rule as ADR 0002).

### 3.3 Decision criteria (write into ADR 0003 with status `Proposed` and commit before measuring)

**Gates** (fail = out):

| ID | Gate |
|---|---|
| G1 | License GPL-3.0-compatible per ADR 0001 policy and FSF list. |
| G2 | Not abandoned: repository not archived, package not deprecated/unlisted on NuGet. (Other maintenance signals are flags and go into the score, as in ADR 0001.) |
| G3 | The read path shelf uses is reflection-free: untyped DOM or source-generated types; no API annotated `RequiresUnreferencedCode`/`RequiresDynamicCode` on that path. |
| G4 | AOT publish (`osx-arm64`, `-p:TrimmerSingleWarn=false`) with zero `IL*`/AOT warnings, no `NoWarn`, no `SuppressTrimAnalysisWarnings`. |
| G5 | All functional checks pass on the AOT binary: every row of `ConfigFile.feature` (values, absent keys, errors with the expected line). |
| G6 | Claims TOML 1.0.0 support. |

**Weighted score** (1 to 5; weights and bands are a proposal, Q1):

| Criterion | Weight | Scoring |
|---|---|---|
| Conformance on the hand-written corpus (valid accepted with correct values, invalid rejected) | 25 % | 100 %: 5, ≥ 95 %: 4, ≥ 90 %: 3, ≥ 80 %: 2, less: 1 |
| Error reporting (line and column present and correct on the invalid corpus; message usable for a user) | 15 % | Judgement, justified |
| Binary size overhead vs. Baseline | 15 % | ≤ 0.5 MB: 5, ≤ 1 MB: 4, ≤ 2 MB: 3, ≤ 4 MB: 2, more: 1 (MB = 10^6 bytes) |
| Load time overhead vs. Baseline (`get sample/config.toml grouping.auto_merge`, median) | 10 % | ≤ 1 ms: 5, ≤ 3 ms: 4, ≤ 10 ms: 3, ≤ 30 ms: 2, more: 1 |
| Maintenance (ADR 0001 signals and flags) | 15 % | Judgement |
| API fit (lines in the adapter mapping to `ConfigDocument`, type coverage incl. datetimes, no leaking types) | 10 % | Judgement |
| Write capability (round-trip, 3.4) | 10 % | 1 point each: unchanged round-trip byte-identical; comments kept after edit; key order kept; formatting/whitespace kept; new key insertable into an existing table; max 5 |

If no candidate passes all gates: stop, escalate (Q-list), integrate nothing.

### 3.4 Benchmark `experiments/toml-parser/`

Same isolation as `experiments/cli-framework/` (ADR 0002): outside `shelf.slnx`, own props that do not import the root, `TreatWarningsAsErrors=false`, `PublishAot=true`, `IsAotCompatible=true`, `InvariantGlobalization` **not** set. Assembly names `proto-toml-baseline`, `proto-tomlyn`, `proto-tomlet`, `proto-cstoml`, `proto-tommy`.

Each prototype is a tiny CLI writing only to the `TextWriter`s it gets in `Main`:

| Invocation | Exit | stdout |
|---|---|---|
| `get <file> <dotted.key>` | 0 / 1 | `<kind>:<value>` (`float` printed with `"R"` invariant), or `absent`; errors as `<file>:<line>:<col>: <message>` on stderr with exit 1 |
| `corpus <dir>` | 0 | one line per file: `pass`/`fail` + name; summary `passed X/Y` |
| `roundtrip <in> <out>` | 0 / 1 | Writes (1) unchanged re-serialization to `<out>.same.toml`, (2) after setting `grouping.auto_merge = 0.85` and adding `grouping.new_key = "x"` to `<out>.edit.toml`. Prints `unsupported` if the library has no write path. |

The `get` checks replicate every row of `ConfigFile.feature` as files under `checks/` (G5).

**Corpus** (hand-written from the TOML 1.0.0 spec, about 40 files, to avoid copying third-party test files): valid: all string forms incl. multi-line and escapes, integers (hex/oct/bin, separators, sign), floats (exponent, `inf`, `nan`), booleans, the four datetime kinds, arrays (mixed types allowed in 1.0), inline tables, arrays of tables, dotted keys, quoted keys, CRLF line endings. Invalid: the six rows of 2.1 plus redefining a table via dotted keys, inline table extension, integer overflow (`9223372036854775808`), invalid escape `\q`, control character in a string, bare key with a space, trailing comma in an inline table. Each valid file has an expected value list for 1-3 keys.

**`measure.sh`** (bash 3.2, `set -euo pipefail`; reuse the structure of `experiments/cli-framework/measure.sh`): per prototype publish with `-c Release -r osx-arm64 -p:TrimmerSingleWarn=false`, count warnings by code, run checks, corpus, round-trip, size (`stat -f%z`, binary only, list output dir), timing (hyperfine `-N` or the zsh `$EPOCHREALTIME` fallback; 5 warmups, 50 runs). Record environment as in ADR 0002 (incl. power source). Run once more with `LANG=de_DE.UTF-8 LC_ALL=de_DE.UTF-8`: `float` output must still be `0.9`.

### 3.5 Integration into `Shelf.Core` (library-independent contract)

Namespace `Shelf.Core.Configuration`.

```csharp
public enum ConfigValueKind { String, Integer, Float, Boolean, DateTime, Array, Table }

public sealed class ConfigDocument
{
    public static ConfigDocument Empty { get; }
    public string SourceName { get; }                    // path as given to Load
    public bool Contains(string key);
    public string? GetString(string key);                // null = absent
    public long? GetInteger(string key);
    public double? GetFloat(string key);                 // Integer widened to double (Q5)
    public bool? GetBoolean(string key);
}

public sealed class ConfigException : Exception
{
    public string SourceName { get; }
    public int? Line { get; }       // 1-based
    public int? Column { get; }     // 1-based
    public string? Key { get; }
}

public static class ConfigFile
{
    public static ConfigDocument Load(string path);                       // missing file → Empty with SourceName (Q4)
    public static ConfigDocument Parse(string text, string sourceName);
}

public static class ConfigPaths
{
    public static string Global(Func<string, string?> getVariable, string homeDirectory);
    public static string Library(string libraryRoot);
}
```

Rules:

- `TomlConfigParser` (internal) is the only code that references the library. It maps the library DOM into shelf's own immutable tree (`FrozenDictionary<string, Node>`; values as `string`, `long`, `double`, `bool`, TOML datetimes kept as their source text with kind `DateTime`, arrays as `ImmutableArray`). No library type is public or stored.
- No interface/DI abstraction yet; a second implementation does not exist (YAGNI). The adapter file is the swap point.
- Key lookup: split on `.`; each segment is a table key. A key whose segment contains a `.` (quoted key) is not addressable; documented limitation. Descending into a non-table → absent.
- Type mismatch → `ConfigException` with message `<source>: <key>: expected <kind>, found <kind>` (kinds lower-case, as in 2.1). Absent → `null`.
- Parse error message: `<source>:<line>:<column>: <library message>`; if the library gives no column, `<source>:<line>: <message>`. `Line` is mandatory for parse errors (G5).
- `Load`: read bytes, decode with `new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true)`; invalid UTF-8 → `ConfigException` without line. Missing file (`FileNotFoundException`/`DirectoryNotFoundException`) → `Empty`; any other I/O error → `ConfigException` wrapping it.
- Numbers never pass through culture-sensitive formatting/parsing.
- `ConfigPaths.Global`: `XDG_CONFIG_HOME` if non-empty and `Path.IsPathRooted`, else `<home>/.config`; then `shelf/config.toml`. Trailing separators normalized (`Path.TrimEndingDirectorySeparator`). `Library`: `<root>/.shelf/config.toml`, same normalization. Pure functions; `Program.cs` is not changed in this slice.

### 3.6 AOT and native-dependency risks

- **No native dependencies expected** (all candidates are managed). Confirm with the `.nupkg` listing; note any `runtimes/` folder in the ADR.
- **Reflection in the package:** all candidates except possibly CsToml contain reflection-based object mappers. They are acceptable only if not reachable from shelf's path (G3). Static constructors or shared option objects can still pull reflection into the untyped path; G4 detects this.
- **Source generators** (CsToml, possibly Tomlyn): run at build time in `Shelf.Core`; check they work with `TreatWarningsAsErrors`, `LangVersion=latest`, and do not emit analyzer warnings.
- **Reachability gap in CI:** the CI `aot` job publishes `Shelf.Cli`, and nothing in `Shelf.Cli` calls `Shelf.Core.Configuration` in M0, so ILC never compiles or analyzes the parser there. Build-time trim analyzers (`IsAotCompatible=true` on `Shelf.Core`, warnings as errors) still fail the build on all four platforms if `Shelf.Core` calls an annotated API. Unannotated reflection inside the library is only caught by the prototype (osx-arm64) until M1 wires config into the CLI. See Q8.
- **Culture:** `double` parsing/printing must be invariant (`0.9` vs `0,9` under `de_DE`). Covered by unit tests and the locale run in step 9.
- **Trimming of the package's own culture/resources:** check the publish output for satellite folders; set `SatelliteResourceLanguages=en` on `Shelf.Core` only if any appear.
- **TFM:** a package that only ships `netstandard2.0` is acceptable but carries no `IsAotCompatible` claim; G4/G5 decide.

## 4. Unit Tests (`tests/Shelf.Core.Tests`)

xunit.v3, `OutputType=Exe`, `IsPackable=false`, `ProjectReference` to `Shelf.Core`; `Shelf.Core` gets `InternalsVisibleTo Shelf.Core.Tests`.

| Unit | Cases |
|---|---|
| `TomlConfigParser` mapping | Every TOML type maps to the right `ConfigValueKind`: basic/literal/multi-line strings, integer forms, `inf`/`-inf`/`nan` floats, booleans, offset/local datetime, local date, local time, arrays, nested tables, arrays of tables. Integer overflow `9223372036854775808` → `ConfigException` with line. CRLF input gives the same document as LF. |
| `ConfigDocument` lookup | Dotted path across header table, dotted key and inline table gives the same value; descending into a scalar → absent; empty key / `a..b` → `ArgumentException`; quoted key with a dot is not addressable (documented). |
| Type rules | Integer→float widening (`1` → `1.0`; `long.MaxValue` → nearest double); float→integer is a mismatch; mismatch message exact format for each kind pair used in 2.1. |
| Culture | Under `CultureInfo.CurrentCulture = de-DE` (set in test, restored): `0.9` is read as `0.9`; message text unchanged. |
| `ConfigFile.Load` | Invalid UTF-8 bytes → `ConfigException`, `Line == null`; UTF-8 BOM: record behavior of the winner and assert it (Q12); directory instead of file → `ConfigException`; missing parent directory → `Empty`. |
| Error format | `Line` and `Column` 1-based; message `<source>:<line>:<column>: ...`; fallback without column. |
| `ConfigPaths` | Extra cases beyond the scenarios: home with trailing slash; `XDG_CONFIG_HOME` = `/`; root `/`. |

## 5. Step Order

1. **Feature files.** Add 2.1 and 2.2, `ScenarioEnvironment`, refactor `CliSteps` to use it. `dotnet test -c Release`: new scenarios red (undefined steps), all existing green. Verify doc-string placeholder substitution.
2. **Fix criteria.** Create `docs/adr/0003-toml-parser.md` (status `Proposed`): Context (PRD 9.2, F-GRP-6, ADR 0001 Q5 exclusion), Options A-F, criteria 3.3. Commit before any measurement.
3. **Research.** License/maintenance/AOT-claim/native-asset table for A-E plus NuGet screening, ADR 0001 format and source index. Drop candidates that fail G1, G2, G6 on paper (G3 if the untyped path is documented as reflection-based). Commit.
4. **Prototypes.** Create `experiments/toml-parser/` (3.4): props, `.gitignore`, sample, checks, corpus, Baseline, one prototype per remaining candidate. Confirm root `dotnet build` does not touch them.
5. **Measure.** Write and run `measure.sh` on `osx-arm64`, including the `de_DE` run. Results stay local (git-ignored).
6. **Decide.** Fill ADR: environment, commands, results (warnings by code, checks, corpus, round-trip table, size, timing), gate table, scores, decision, consequences (version pin, write capability statement, re-check triggers, follow-ups: Q8 hand-off to the AOT spike, M1 wiring, delete `experiments/toml-parser/` when M0 closes). If every candidate fails a gate or an open question blocks: stop and ask. Else status `Accepted`.
7. **Package.** Add the winner to `Directory.Packages.props` and `Shelf.Core.csproj`.
8. **Unit test project.** Create `tests/Shelf.Core.Tests`, add to `shelf.slnx`; write the tests from section 4 (red).
9. **Implementation.** Add the `Configuration` types (3.5); write `ConfigSteps.cs` and `ConfigLocationSteps.cs` alongside; add the explicit `Shelf.Core` reference to `Shelf.Specs`. Iterate until unit tests and all scenarios are green.
10. **Locale check.** `LANG=de_DE.UTF-8 LC_ALL=de_DE.UTF-8 dotnet test -c Release`: identical results.
11. **Quality gates:** `dotnet format --verify-no-changes`; `dotnet build -c Release` (no `NoWarn` for `IL*` in `src/`, `IsAotCompatible=true` kept on `Shelf.Core`); `dotnet test --no-build -c Release` all green; `dotnet publish src/Shelf.Cli/Shelf.Cli.csproj -c Release -r osx-arm64 -p:PublishAot=true -o <scratch>` with zero warnings and the CI smoke check by hand; record the `shelf` binary size before/after in the PR (expected unchanged, see 3.6).
12. **Roadmap.** In M0, note "TOML parser: **Done:** ADR 0003 (<package> <version>)" next to the AOT spike line or exit criterion, same style as the ADR 0001/0002 lines.
13. Push, open the PR; all `ci` checks green on four platforms.

## 6. Open Questions

- **Q1: Gates, weights and bands** (3.3) are a proposal, not from the PRD. Confirm before step 5. In particular G2 (archived/deprecated = out) is stricter than ADR 0001, where maintenance only flags.
- **Q2: Write support.** Undecided by the owner. PRD 7.1 lists no `shelf config` command, and nothing in the PRD writes `config.toml`. Recommendation: reading is required now; write capability (round-trip, comment preservation) is measured and recorded in ADR 0003 with a 10 % weight but not integrated. Decide on writing when a consumer appears (e.g. `shelf config set`, or `shelf init` creating a commented template). If comment-preserving edits become a hard requirement and the winner cannot do them, the options are a targeted line edit or re-opening ADR 0003.
- **Q3: Precedence.** PRD 9.2 names two files but not how they combine (library overrides global per key? whole tables?) nor the order relative to CLI options and environment variables. Out of scope here; needed in M1.
- **Q4: Missing file.** The plan treats a missing file as an empty configuration (scenario marked `# Q4`). The PRD does not say. Confirm; also whether an unreadable file (permissions) is an error (plan: yes).
- **Q5: Integer where a float is expected** (`auto_merge = 1`). TOML distinguishes the types. Plan: widen integer to float, reject float where an integer is expected (scenario row marked `# Q5`). Confirm.
- **Q6: Key naming and schema.** PRD 7.2 writes `mcp.readOnly` (camelCase); TOML convention is snake_case; F-GRP-6 names no keys. Also: are unknown keys ignored, warned about, or errors? Decide in M1 together with the schema.
- **Q7: macOS location.** PRD 9.2 says `~/.config/shelf/config.toml` (XDG) on all platforms; the plan honors `XDG_CONFIG_HOME` (absolute only) and does not use `~/Library/Application Support` on macOS. Confirm. Behavior when `HOME` is unset or empty is not defined (plan: `Program` supplies the home directory; not specified here).
- **Q8: AOT coverage beyond osx-arm64.** CI does not exercise the parser in an AOT binary until M1 wires config into the CLI (3.6). Options: (a) accept and hand the four-platform AOT check to the AOT spike work package (recommended; matches the roadmap, which lists the TOML parser in that spike); (b) root `Shelf.Core` in `Shelf.Cli` via `TrimmerRootAssembly` (costs binary size as Core grows); (c) a separate AOT smoke project published in CI (changes `build.yml`). Confirm.
- **Q9: TOML version.** Target is TOML 1.0.0 (G6). Is TOML 1.1 support (e.g. newlines in inline tables) wanted, or should 1.1-only syntax be rejected for portability? Plan: record support in the ADR, no requirement.
- **Q10: Keep `experiments/toml-parser/`** until M0 closes, then delete with an ADR note pointing to the last commit (same as ADR 0002)? Recommended.
- **Q11: Library list (F-LIB-1).** "Multiple libraries via configuration" implies a list of roots, probably in the global file, plus a way to pick one. Not defined; M1.
- **Q12: UTF-8 BOM.** TOML 1.0 requires UTF-8 but does not mention a BOM. Accept (strip) or reject? Plan: record the winner's behavior and assert it in a unit test; no scenario.
