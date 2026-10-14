# Slice m0-logging: Logging to Standard Error

| | |
|---|---|
| **Slice ID** | `m0-logging` |
| **Branch** | `feat/m0-logging` |
| **Milestone** | M0 – Setup & Spikes |
| **Roadmap** | M0 work package "Logging: Microsoft.Extensions.Logging to stderr only" |
| **PRD references** | 10.2 (Logging: Microsoft.Extensions.Logging, output to stderr, important for MCP stdio), 12 acceptance criterion 6 (MCP stdio not disturbed by log output; only prepared here, verified in M3), 7.1 (`--json`, `NO_COLOR`, exit codes; must keep working) |
| **Inputs** | ADR 0001 row 1 (.NET 10 incl. `Microsoft.Extensions.Logging` 10.0.12, MIT, adopt), ADR 0002 (System.CommandLine), `docs/slices/m0-cli-framework.md`, `src/Shelf.Cli/ShelfApp.cs`, `src/Shelf.Cli/CliEnvironment.cs` |

---

## 1. Scope

**In scope**

1. Package `Microsoft.Extensions.Logging` 10.0.12 in `Shelf.Cli` (license already cleared in ADR 0001 row 1: MIT, adopt).
2. A small custom `ILoggerProvider` in `Shelf.Cli` that writes one line per entry to the **injected** `stderr` `TextWriter` of `ShelfApp.Run`. Nothing ever writes log output to stdout.
3. Logger factory created and disposed inside `ShelfApp.Run`; minimum level carried by `CliEnvironment` (default `Warning`, Q2). The level is settable only programmatically (specs, unit tests); no user-facing option or environment variable (Q1).
4. Three `[LoggerMessage]` source-generated events in `ShelfApp` (start, parse result, exit) so that the stdout/stderr separation is observable in scenarios.
5. Feature file `Logging.feature`, step additions in `CliSteps`, unit tests in `tests/Shelf.Cli.Tests`.
6. Roadmap: mark the M0 Logging work package done.

**Requirements covered:** PRD 10.2 row "Logging" (stderr only). Milestone M0. Acceptance criterion 12.6 is enabled (provider exists, stdout untouched), not verified (no MCP server until M3); it is not tagged.

**Out of scope**

- Any user-facing control of the log level: `--verbose`, `--quiet`, `--log-level`, `SHELF_LOG_LEVEL` or a config key (none is in PRD 7.1 or 9.2; Q1).
- Log files, timestamps, scopes, structured/JSON log output, colored log output (Q3, Q5).
- Logging from `Shelf.Core` (no use case exists yet; Core gets no package reference in this slice, Q6).
- Wiring the provider into a `Microsoft.Extensions.Hosting`/DI host (MCP server, web host): M3/M4 (Q8). The provider is written so that it can later be added with `ILoggingBuilder.AddProvider`.
- MCP protocol logging (`notifications/message`): unrelated to stderr logging, M3.
- Converting the existing user-facing messages (`Usage: ...`, `Unknown option ...`, `error: ...`) into log entries. They stay direct writes to `stderr` (Q4).
- An ADR. There is no real choice (see 3.2); the rationale lives in this plan and in a doc comment on the provider (Q7).

## 2. Feature Files

Rules for the implementer:

- Write the file first. Run `dotnet test -c Release`: new scenarios red (undefined steps or failing assertions), existing ones green.
- Every existing scenario that asserts `standard error is empty` must stay green unchanged. That is the regression check for the default level.
- Log line format used in the scenarios: `[<level>] <category>: <message>` with lower-case full level names (`trace`, `debug`, `information`, `warning`, `error`, `critical`); the category of all events is `Shelf.Cli.ShelfApp` (3.3, 3.4).

### 2.1 `tests/Shelf.Specs/Features/Cli/Logging.feature` (new)

```gherkin
@PRD-10.2 @M0
Feature: Diagnostic logging goes to standard error only
  As a user, a script or an MCP client reading shelf's standard output
  I want diagnostic log output to appear on standard error only
  So that command output and protocol messages on standard output stay clean

  Scenario Outline: Log lines on standard error follow the log level
    Given the log level is "<level>"
    When I run shelf with arguments "--version"
    Then the exit code is 0
    And standard output matches the pattern "^shelf \d+\.\d+\.\d+"
    And standard error contains <trace> log lines at level "trace"
    And standard error contains <debug> log lines at level "debug"

    Examples:
      | level       | trace | debug |
      | Trace       | 1     | 2     |
      | Debug       | 0     | 2     |
      | Information | 0     | 0     |
      | Warning     | 0     | 0     |
      | None        | 0     | 0     |

  Scenario Outline: Exact log lines for a successful run
    Given the log level is "Trace"
    When I run shelf with arguments "--version"
    Then standard error contains the line "<line>"

    Examples:
      | line                                                                    |
      | [trace] Shelf.Cli.ShelfApp: Parsed command line: 1 token(s), 0 error(s) |
      | [debug] Shelf.Cli.ShelfApp: Exiting with code 0                         |

  Scenario Outline: JSON output stays a single valid JSON object while logging
    Given the log level is "Trace"
    When I run shelf with arguments "<arguments>"
    Then the exit code is 0
    And standard output is a JSON object with the string property "version" matching "^\d+\.\d+\.\d+"
    And standard error contains 2 log lines at level "debug"

    Examples:
      | arguments        |
      | --version --json |
      | --json --version |

  Scenario Outline: Usage errors keep standard output empty while logging
    Given the log level is "Trace"
    When I run shelf with arguments "<arguments>"
    Then the exit code is 2
    And standard output is empty
    And standard error contains "<message>"
    And standard error contains the line "[trace] Shelf.Cli.ShelfApp: Parsed command line: <tokens> token(s), <errors> error(s)"
    And standard error contains the line "[debug] Shelf.Cli.ShelfApp: Exiting with code 2"

    Examples:
      | arguments         | message                       | tokens | errors |
      |                   | Usage: shelf                  | 0      | 0      |
      | --frobnicate      | Unknown option '--frobnicate' | 1      | 1      |
      | frobnicate --json | Unknown command 'frobnicate'  | 2      | 1      |

  Scenario Outline: Help text on standard output contains no log lines
    Given the log level is "Trace"
    When I run shelf with arguments "<arguments>"
    Then the exit code is 0
    And standard output contains "Usage:"
    And standard output does not contain "Shelf.Cli.ShelfApp"
    And standard error contains the line "[debug] Shelf.Cli.ShelfApp: Exiting with code 0"

    Examples:
      | arguments     |
      | --help        |
      | --help --json |

  Scenario Outline: Without a log level setting no log lines are written
    When I run shelf with arguments "<arguments>"
    Then the exit code is <exit code>
    And standard error does not contain "Shelf.Cli.ShelfApp"

    Examples:
      | arguments        | exit code |
      | --version        | 0         |
      | --version --json | 0         |
      | --help           | 0         |
      | --frobnicate     | 2         |

  Scenario: Log lines contain no color codes even where color is allowed
    Given standard output is a terminal
    And the log level is "Trace"
    When I run shelf with arguments "--version"
    Then the exit code is 0
    And standard error contains 2 log lines at level "debug"
    And standard error contains no ANSI escape sequences
```

Notes:

- Token and error counts in the usage-error rows are the expected values of `parseResult.Tokens.Count` and `parseResult.Errors.Count` in System.CommandLine 2.0.12; they are not verified. Check them in step 1 with a quick debug run. If a count differs, report it to the caller and fix the row; do not change the event.
- The `debug` count of 2 is the start event plus the exit event (3.4).
- `VersionAction` prints `shelf <version>`; the pattern is anchored at the start.

### 2.2 Step definitions (`tests/Shelf.Specs/Steps/CliSteps.cs`, change)

New field `LogLevel? _logLevel`. `WhenIRunShelfWithArguments` applies it: `if (_logLevel is { } level) cliEnvironment = cliEnvironment.WithMinimumLogLevel(level);`.

| Step | Behavior |
|---|---|
| `Given the log level is {string}` | `Enum.Parse<LogLevel>(value, ignoreCase: false)`; store. |
| `Then standard error contains {string}` | Ordinal `Contains`. |
| `Then standard output does not contain {string}` | Ordinal `DoesNotContain`. |
| `Then standard error contains the line {string}` | Split stderr on `'\n'`, trim a trailing `'\r'`, ordinal equality with any line. |
| `Then standard error contains {int} log line(s) at level {string}` | Count lines starting with `"[" + level + "] "` (ordinal); assert equality. |

Already existing and reused: `standard output is a terminal`, `standard error contains no ANSI escape sequences`, `standard output matches the pattern {string}`, `standard output is empty`, `standard output contains {string}`, `standard error does not contain {string}`, the JSON object step. `LogLevel` (from `Microsoft.Extensions.Logging.Abstractions`) should flow transitively to `Shelf.Specs` through the `Shelf.Cli` project reference; add a package reference only if the build fails.

## 3. Design

### 3.1 Files

```
Directory.Packages.props                         CHANGE  PackageVersion Microsoft.Extensions.Logging 10.0.12
src/Shelf.Cli/Shelf.Cli.csproj                   CHANGE  PackageReference Microsoft.Extensions.Logging
src/Shelf.Cli/CliEnvironment.cs                  CHANGE  MinimumLogLevel + WithMinimumLogLevel
src/Shelf.Cli/ShelfApp.cs                        CHANGE  create/dispose factory, log events, single exit path
src/Shelf.Cli/Logging/StderrLoggerProvider.cs    NEW     public
src/Shelf.Cli/Logging/StderrLogger.cs            NEW     internal
src/Shelf.Cli/Logging/LogLineFormatter.cs        NEW     internal static, pure
src/Shelf.Cli/Logging/ShelfAppLog.cs             NEW     internal static partial, [LoggerMessage] events
tests/Shelf.Specs/Features/Cli/Logging.feature   NEW     (2.1)
tests/Shelf.Specs/Steps/CliSteps.cs              CHANGE  (2.2)
tests/Shelf.Cli.Tests/Logging/*Tests.cs          NEW     (section 4)
tests/Shelf.Cli.Tests/CliEnvironmentTests.cs     CHANGE  log level default and WithMinimumLogLevel
docs/roadmap.md                                  CHANGE  M0 Logging row: Done note
```

`Program.cs` is not changed (no user-facing level source, Q1). `Shelf.Core` is not changed (Q6).

### 3.2 Provider choice (no ADR)

| Option | Verdict |
|---|---|
| `Microsoft.Extensions.Logging.Console` | Rejected. Writes to `System.Console` itself, so it cannot target the injected `stderr` writer that `ShelfApp` and the specs rely on (`ShelfApp` "never touches the static console"). Writes asynchronously from a background queue thread: lines can reorder relative to direct `stderr` writes and need flushing on exit. Pulls in `Microsoft.Extensions.Options`/configuration-based option binding and formatter infrastructure (binary size and cold start for nothing used). Emits ANSI colors by default. Its default `LogToStandardErrorThreshold` sends entries to stdout unless configured, which is exactly the MCP risk. |
| Custom `ILoggerProvider` over a `TextWriter` (chosen) | About 100 lines, synchronous, deterministic order, injectable, no reflection. |
| No `Microsoft.Extensions.Logging` at all (own interface) | Rejected: PRD 10.2 names Microsoft.Extensions.Logging, and the MCP SDK and ASP.NET Core log through `ILogger`, so shelf's sink must be an `ILoggerProvider`. |

### 3.3 Types (namespace `Shelf.Cli.Logging`, except `CliEnvironment`)

```csharp
public sealed class CliEnvironment
{
    // existing members unchanged
    public LogLevel MinimumLogLevel { get; }                   // Empty and FromVariables: LogLevel.Warning (Q2)
    public CliEnvironment WithMinimumLogLevel(LogLevel level); // new instance; ArgumentOutOfRangeException if not a defined value
}

public sealed class StderrLoggerProvider(TextWriter writer) : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName);          // cached per category (ConcurrentDictionary)
    public void Dispose();                                     // does not dispose the writer
}

internal sealed class StderrLogger(string category, TextWriter writer, Lock gate) : ILogger
{
    public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;  // level filtering is the factory's job
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;  // scopes unsupported
    public void Log<TState>(LogLevel, EventId, TState, Exception?, Func<TState, Exception?, string>);
}

internal static class LogLineFormatter
{
    public static string Format(LogLevel level, string category, string message, Exception? exception);
}

internal static partial class ShelfAppLog
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Debug, Message = "Starting shelf {Version}")]
    public static partial void Starting(ILogger logger, string version);

    [LoggerMessage(EventId = 2, Level = LogLevel.Trace, Message = "Parsed command line: {TokenCount} token(s), {ErrorCount} error(s)")]
    public static partial void Parsed(ILogger logger, int tokenCount, int errorCount);

    [LoggerMessage(EventId = 3, Level = LogLevel.Debug, Message = "Exiting with code {ExitCode}")]
    public static partial void Exiting(ILogger logger, int exitCode);
}
```

Rules:

- **Line format** (`LogLineFormatter`): `[<level>] <category>: <message>`; level names exactly `trace`, `debug`, `information`, `warning`, `error`, `critical` (a `switch`, not `ToString().ToLowerInvariant()`). If an exception is present, append `Environment.NewLine` + `exception.ToString()`. No timestamp, no event ID, no color (Q3, Q5). Empty message and no exception: nothing is written.
- **Writing** (`StderrLogger.Log`): check `IsEnabled`; build the complete text first, then one `writer.WriteLine(text)` under the provider's single `Lock`, so concurrent loggers (later: MCP, web) never interleave partial lines. Catch `IOException` and `ObjectDisposedException` from the writer and drop the entry (a closed stderr must not crash shelf or an MCP session). No `Flush`: `Console.Error` autoflushes; tests use `StringWriter`.
- **Factory** in `ShelfApp.Run`: `using var provider = new StderrLoggerProvider(stderr); using var factory = new LoggerFactory([provider], new LoggerFilterOptions { MinLevel = environment.MinimumLogLevel });` then `var logger = factory.CreateLogger("Shelf.Cli.ShelfApp");`. Use the constructor, not `LoggerFactory.Create(builder => ...)`, to avoid building a DI container on every CLI start (cold start, PRD 9.1).
- **ShelfApp flow:** argument null checks stay first. Then: create the factory; `Starting(VersionInfo.Get())`; the existing body moves into a private `RunCore(...)` that returns the exit code and logs `Parsed(parseResult.Tokens.Count, parseResult.Errors.Count)` right after `root.Parse(args)`; `Exiting(code)` after `RunCore` returns on every path (usage error, success, caught exception). The `catch` stays as is (`error: <message>` on stderr, exit 1).
- Update the comment on `environment` in `ShelfApp.Run` ("reserved for M1 color output"): the environment now also carries the log level.
- Category is the literal `"Shelf.Cli.ShelfApp"` (`ShelfApp` is a static class, so `ILogger<ShelfApp>` is not possible).
- Doc comment on `StderrLoggerProvider`: why not the console provider (3.2), and that it is the only log sink shelf may register (stdout is reserved for command output and the MCP stdio protocol).

### 3.4 Events emitted per run

| Run | Trace | Debug |
|---|---|---|
| Any invocation | `Parsed command line: N token(s), M error(s)` | `Starting shelf <version>`, `Exiting with code <code>` |

This is what 2.1 counts (1 trace, 2 debug).

### 3.5 AOT, trimming and native-dependency risks

- **No native dependencies.** All packages are managed.
- **Package graph:** `Microsoft.Extensions.Logging` 10.0.12 pulls `Logging.Abstractions`, `DependencyInjection(.Abstractions)`, `Options`, `Primitives` (verify with `dotnet list package --include-transitive`). All MIT, Microsoft, covered by ADR 0001 row 1; record the transitive list in the PR. DI is referenced but not exercised by the chosen constructor; ILC trims unused code.
- **`LoggerFactory` internals:** the rule selector reads `ProviderAliasAttribute` via reflection on the provider type. Expected to be annotated in dotnet/runtime (unverified). Gate: zero `IL*`/AOT warnings on publish. If any warning appears, stop and report; do not add `NoWarn` or suppressions.
- **`ActivityTrackingOptions`** default `None`: no `System.Diagnostics.Activity` work.
- **Source generator:** the `[LoggerMessage]` generator ships as an analyzer in `Microsoft.Extensions.Logging.Abstractions`; it must flow transitively into `Shelf.Cli` (verify the generated file under `obj/`). Generator diagnostics (`SYSLIB1xxx`) are errors under `TreatWarningsAsErrors`.
- **No template-based extension calls** (`logger.LogDebug("...{X}", x)`): they parse templates and box at runtime. All events go through the generator.
- **ADR 0001 caveat:** row 1 records no documented `IsAotCompatible` claim for `Microsoft.Extensions.Logging`. The publish in step 7 is the first real verification on osx-arm64; the CI `aot` job covers the other three platforms because `Shelf.Cli` reaches the code.
- **Binary size / cold start:** record `shelf` binary size and `shelf --version` wall time (osx-arm64, 10 runs, median) before and after. Expected small growth; no gate, but report it (PRD 9.1: < 150 ms).

## 4. Unit Tests (`tests/Shelf.Cli.Tests/Logging/`)

| Unit | Cases |
|---|---|
| `LogLineFormatter` | Every `LogLevel` except `None` maps to its name (Theory, 6 rows); `[warning] Cat: msg` exact; an exception appends newline + `exception.ToString()`; a message with an embedded newline is passed through unchanged; empty message without exception gives an empty result (or `null`; implementer's choice, documented). |
| `StderrLogger` | `IsEnabled(None)` false, all others true; `BeginScope` returns `null`; the formatter receives state and exception; a writer throwing `IOException` lets no exception escape; same for `ObjectDisposedException`. |
| Concurrency | 8 threads x 500 entries through two loggers (categories `A`, `B`) of one provider into a `StringWriter`: 4000 lines, each matching `^\[debug\] (A|B): \d+$` (no interleaving). |
| `StderrLoggerProvider` | Same category returns the same instance; `Dispose` does not dispose the writer (writing to the `StringWriter` afterwards still works). |
| Factory filtering | `LoggerFactory` + provider with `MinLevel = Information`: `Debug` entry not written, `Information` written (guards the wiring used in `ShelfApp`). |
| `CliEnvironment` | `Empty.MinimumLogLevel == Warning`; `FromVariables(...).MinimumLogLevel == Warning`; `WithMinimumLogLevel(Debug)` returns a new instance with `Debug` and the same `NoColor`/`StdoutIsTerminal`; `(LogLevel)42` throws `ArgumentOutOfRangeException`. |
| stdout isolation | `ShelfApp.Run(["--version"], ...)` and `["--version", "--json"]` with `Trace`: stdout is identical to the run with `Warning`. |

## 5. Step Order

1. **Feature file.** Add `Logging.feature` (2.1). `dotnet test -c Release`: new scenarios red, existing green. Check the token/error counts of the usage-error rows against System.CommandLine 2.0.12.
2. **Package.** Add `Microsoft.Extensions.Logging` 10.0.12 to `Directory.Packages.props` and `Shelf.Cli.csproj`. `dotnet build -c Release` clean. Record the transitive package list.
3. **Unit tests red.** Write the tests from section 4 against compile-only stubs.
4. **Formatter, logger, provider.** Implement `LogLineFormatter`, `StderrLogger`, `StderrLoggerProvider` until their unit tests are green.
5. **CliEnvironment.** Add `MinimumLogLevel` / `WithMinimumLogLevel`; tests green.
6. **ShelfApp wiring.** Add `ShelfAppLog`, the factory, `RunCore`, the three events; write the step definitions from 2.2 alongside. Iterate until all unit tests and all scenarios are green, including every existing `standard error is empty` scenario.
7. **AOT check.** Measure the baseline binary size and `--version` time on `main` first. Then `dotnet publish src/Shelf.Cli/Shelf.Cli.csproj -c Release -r osx-arm64 -p:PublishAot=true -p:TrimmerSingleWarn=false -o <scratch>`: zero warnings. Run the binary: `shelf --version` prints the version on stdout and nothing on stderr (`2>&1 >/dev/null` is empty); `shelf --frobnicate` prints only the usage message on stderr and nothing on stdout. Record size and time after.
8. **Quality gates.** `dotnet format --verify-no-changes`; `dotnet build -c Release`; `dotnet test --no-build -c Release` all green; no `NoWarn` or suppressions added.
9. **Roadmap.** M0 table, Logging row: append "**Done:** custom stderr `ILoggerProvider` on Microsoft.Extensions.Logging 10.0.12 ([slice plan](slices/m0-logging.md)); no user-facing level control yet", same style as the ADR 0001/0002 rows.
10. Push, open the PR (size/time numbers and the transitive package list in the description); all `ci` checks green on four platforms.

## 6. Open Questions

- **Q1: User-facing log level control.** PRD 7.1 lists only `--json` and `NO_COLOR`; it defines no `--verbose`, `--quiet`, `--log-level`, `SHELF_*` variable or config key. This slice exposes the level only programmatically, so in a real run nothing below `Warning` is visible. Options: (a) `SHELF_LOG_LEVEL` environment variable (no change to the command grammar; works for `shelf mcp` started by Claude Desktop, whose server config sets env vars); (b) global `--verbose`/`--quiet` (or a `-v` count); (c) a `config.toml` key (needs the precedence rules, m0-toml-parser Q3); (d) a combination. Recommendation: (a) in M1, plus (b) if CLI users ask. Needs an owner decision and a PRD 7.1 amendment before implementation.
- **Q2: Default level.** Plan: `Warning` (silent on success; existing scenarios require an empty stderr). Confirm. Should `shelf mcp` / `shelf serve` use a different default (e.g. `Information`) in M3/M4?
- **Q3: Line format.** Plan: `[<level>] <category>: <message>`, no timestamp, no event ID, exception stack trace on following lines. Long-running modes (`serve`, `mcp`) may want timestamps (Claude Desktop writes MCP server stderr to its log file). Decide when those modes arrive; the format lives in one function.
- **Q4: User messages vs. log entries.** Plan: `Usage: ...`, `Unknown option ...` and `error: <message>` remain direct stderr writes, shown regardless of the level. Should the unhandled-exception path additionally log the full exception at `Debug` (useful for bug reports)? Not added here: no M0 command can throw, so no scenario could cover it.
- **Q5: Color.** Plan: log lines are never colored, even on a terminal without `NO_COLOR`. Confirm, or decide with the M1 color work.
- **Q6: Logging in `Shelf.Core` and other libraries.** Plan: libraries depend only on `Microsoft.Extensions.Logging.Abstractions` and receive `ILogger<T>`/`ILoggerFactory` from the CLI host, starting with the first M1 use case. Confirm, so M1 does not add a second logging setup.
- **Q7: ADR.** Plan: no ADR 0004, because the console provider is ruled out by hard requirements (injected writer, stderr only), not by a trade-off; the reasoning is in 3.2 and in the provider's doc comment. Confirm, or request a short ADR 0004.
- **Q8: Generic host integration (M3/M4).** The MCP SDK and ASP.NET Core configure logging through `ILoggingBuilder`, and default host builders add the console provider (which writes to stdout by default). M3 must call `ClearProviders()`, add `StderrLoggerProvider` over `Console.Error`, and verify acceptance criterion 12.6 with a stdio protocol test. Hand-off only, no action now.

## 7. Implementation Notes

- Owner decisions: Q1 level is internal only (`CliEnvironment.MinimumLogLevel` / `WithMinimumLogLevel`); a later slice decides how users set it (SHELF_LOG_LEVEL is the recommendation). Q2 default `Warning`. Q3-Q8 as planned.
- Token/error counts in the usage-error rows were verified against System.CommandLine 2.0.12 (scenarios green): 0/0, 1/1, 2/1.
- osx-arm64 AOT publish: zero warnings. Binary 3,061,848 -> 3,161,496 bytes (+99,648). `--version` median of 20 runs: 3.2 ms -> 3.6 ms.
- Roadmap M0 Logging row marked Done.
