# Slice m1-init: `shelf init` and CLI Wiring

| | |
|---|---|
| **Slice ID** | `m1-init` |
| **Branch** | `feat/m1-init` (stacked on `feat/m1-model-sidecar`) |
| **Milestone** | M1 – Foundation |
| **Roadmap** | M1 CLI command `init`; follow-ups carried from [m1-spec-infra](m1-spec-infra.md) and [m1-config](m1-config.md) |
| **PRD references** | F-LIB-1, 7.1 (exit codes, `--json`), 9.2 (config locations), 10.1 (`.shelf` layout) |
| **Inputs** | ADR 0002 (parse-error follow-up), `src/Shelf.Cli/ShelfApp.cs`, `src/Shelf.Core/Libraries` |

---

## 1. Scope

**In scope**

1. `shelf init <root> [--json]`: creates the folder if needed, then `.shelf/config.toml` (commented template, changes no setting) and `.shelf/review.json` (`{"schema": 1, "entries": []}`). Existing books in the folder are not touched. Refused: the folder is already a library, lies inside another library, or is a file. If a write fails, the `.shelf` folder created by the call is removed.
2. `CliEnvironment` knows the working directory, the home directory and process variables (`GetVariable`); `Program` passes the real ones, specs pass a scratch folder.
3. `SettingsLoader`: global config (and, for later commands, the library config) → `ShelfSettings`; unknown keys are printed as `warning: <source>: unknown key '<key>'` on standard error. An invalid config stops `init` before anything is created (`error: …`, exit 1).
4. ADR 0002 follow-up: framework parse errors (missing argument) are printed before the usage line; below a command a stray word is `Unexpected argument 'b'`, at the top level `Unknown command 'x'`. A value starting with `-` is never taken as the folder (`Unknown option '--x'`).
5. The deferred scenarios of m1-spec-infra: `error: <message>` with exit 1.

**Requirements covered:** F-LIB-1, PRD 7.1.

**Out of scope**

- `index.db`: created by `init` in slice `m1-index-db` (needs SQLite).
- The `--library` option: first command that works on an existing library (`import`/`scan`). `SettingsLoader` and `LibraryLocator` are ready for it.
- Registering the library in the global config (`[[libraries]]`): there is no config writer; the user edits the file.
- `.shelf/journal`, `.shelf/trash`, `.shelf/models`: created by the features that use them (M2, M3).
- Color output: still no colored output exists.

## 2. Feature Files

`Features/Cli/Init.feature` (`@F-LIB-1 @PRD-7.1 @M1`): structure, existing books untouched, path with spaces, JSON, refusals (already a library, nested, file), usage errors outline, global config warning and error.

`ExitCodes.feature`: the help line is now `shelf [command] [options]` (a command exists).

## 3. Design

- `LibraryInitializer` (Core) does the file system work; `InitCommand` (Cli) only parses, calls and prints.
- `review.json` already exists in schema 1 with an empty `entries` list; slice `m1-grouping` defines the entry format. Nothing has been released, so no migration is needed.
- `JsonOutput.Object` writes small JSON objects without reflection (Native AOT).
- Decision without ADR: libraries cannot be nested (a parent search for `.shelf` would otherwise be ambiguous).

## 4. Unit Tests

`LibraryInitializerTests`, `ConfigTemplateTests` (template sets nothing; uncommented it equals the defaults), `CliEnvironmentTests` (directories, variables), `ParseErrorMessagesTests`.

## 5. Open Questions

None.

## 6. Implementation Notes

- Reqnroll does not unescape `\"` in `{string}`; scenarios quote with single quotes when the text contains double quotes.
- System.CommandLine takes an unknown `--option` as the value of a missing argument, hence the validator on `root`.
