# Slice m1-spec-infra: Spec Infrastructure for M1

| | |
|---|---|
| **Slice ID** | `m1-spec-infra` |
| **Branch** | `feat/m1-spec-infra` |
| **Milestone** | M1 – Foundation |
| **Roadmap** | M1 plan, slice 1; "Carried over from M0": logging level control |
| **PRD references** | 10.2 (Logging), 7.1 (CLI: exit codes, `--json`, `NO_COLOR`) |
| **Inputs** | [m0-logging](m0-logging.md) (Q1: no user-facing level control in M0), ADR 0002, `src/Shelf.Cli/CliEnvironment.cs`, `tests/Shelf.Specs/Steps/CliSteps.cs` |

---

## 1. Scope

**In scope**

1. `SHELF_LOG_LEVEL` sets the minimum log level (a `LogLevel` name, case-insensitive; numbers are not accepted). An empty value counts as unset. An invalid value is reported once as `warning: ignoring invalid SHELF_LOG_LEVEL '<value>'` on standard error, the default (`Warning`) stays, the exit code is unaffected.
2. Argument splitting in the step "I run shelf with arguments": double quotes group text with spaces, so later slices can pass paths and queries with spaces.

**Requirements covered:** PRD 10.2 (log level control). Milestone M1.

**Out of scope (moved to the first slice that has a command)**

- Framework parse-error message (missing argument, invalid value) before the usage line (ADR 0002 follow-up): needs a real command with arguments.
- A scenario for an unhandled command error (`error: <message>`, exit 1): needs a command that can fail.
- Library root / working directory in `CliEnvironment`: first needed by `init` (slice `m1-init`).
- Color output and stronger `NO_COLOR` scenarios: first command with colored output.

## 2. Feature Files

`tests/Shelf.Specs/Features/Cli/LogLevel.feature` (`@PRD-10.2 @M1`): level selection by name (case-insensitive), empty value keeps the default, invalid value is reported and ignored.

The argument splitter is test infrastructure, not product behavior; it is covered by `ArgumentSplitterTests` in `Shelf.Specs`.

## 3. Design

- `CliEnvironment.FromVariables` reads `SHELF_LOG_LEVEL`; `IgnoredLogLevel` carries a rejected value; `ShelfApp.Run` writes the warning before the logger starts.
- `WithMinimumLogLevel` keeps `IgnoredLogLevel`.
- `ArgumentSplitter` (internal, `tests/Shelf.Specs/Steps`): whitespace separates, `"` toggles grouping, `""` is an empty argument, no escape character, an unterminated quote throws.
- No AOT risk: no new package, no reflection (`Enum.GetNames<LogLevel>()` is the generic overload).

## 4. Unit Tests

- `CliEnvironmentTests`: names, case, rejected values (`verbose`, `3`, `warn`).
- `ArgumentSplitterTests`: plain, repeated spaces, quoted text, empty argument, quote inside a token, unterminated quote.

## 5. Open Questions

None.

## 6. Implementation Notes

- An invalid `SHELF_LOG_LEVEL` is a warning, not a usage error (exit code 2 is for the command line); decided by the implementer, not an owner question.
