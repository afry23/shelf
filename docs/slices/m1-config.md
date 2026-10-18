# Slice m1-config: Layered Settings and Library Lookup

| | |
|---|---|
| **Slice ID** | `m1-config` |
| **Branch** | `feat/m1-config` (stacked on `feat/m1-spec-infra`) |
| **Milestone** | M1 – Foundation |
| **Roadmap** | M1 area "Configuration"; ADR 0003 open questions Q3, Q6, Q11 (key names, precedence, unknown keys, several libraries) |
| **PRD references** | F-LIB-1, F-GRP-6, 9.2 |
| **Inputs** | [ADR 0003](../adr/0003-toml-parser.md), `src/Shelf.Core/Configuration/*`, PRD 6.3 threshold tables |

---

## 1. Scope

**In scope**

1. `ShelfSettings.Resolve(global, library)`: built-in defaults, overridden by the global config, overridden by the per-library config.
2. Keys: `grouping.auto_merge` (0.9), `grouping.review` (0.5), `grouping.max_hamming` (10); global only: `default_library`, `[[libraries]]` with `name` and `path`.
3. Validation: thresholds in 0..1, `review <= auto_merge` (on the merged values), `max_hamming` an integer in 0..64, library names unique and non-empty, paths absolute, `default_library` names a configured library. Errors are `ConfigException`s naming file and key.
4. Unknown keys (also global-only keys in a library config) produce a warning `<source>: unknown key '<key>'`; they are never an error.
5. `LibraryLocator.Resolve`: `--library` option (configured name, else path relative to the working directory) > working directory or nearest parent with a `.shelf` folder > `default_library`. A chosen folder without `.shelf` is an error that says so.
6. `ConfigDocument.LeafKeys()` and `GetTables()` (arrays of tables, 1-based element keys in errors).

**Requirements covered:** F-LIB-1, F-GRP-6 (configurable thresholds; calibration of the start values is slice `m1-calibration`), PRD 9.2.

**Out of scope**

- Reading the files from disk and printing the warnings: done by the CLI wiring in the first command slice (`m1-init`); `Shelf.Core` stays free of process state.
- The `--library` option itself and the `init` template config: `m1-init`.
- Writing config files.

## 2. Feature Files

- `Features/Configuration/Settings.feature` (`@F-GRP-6 @PRD-9.2 @M1`): defaults, precedence, invalid values, unknown keys.
- `Features/Library/LibraryConfiguration.feature` (`@F-LIB-1 @PRD-9.2 @M1`): libraries and default, invalid entries.
- `Features/Library/LibraryResolution.feature` (`@F-LIB-1 @PRD-9.2 @M1`): option / working directory / default order, failures.

Steps: `tests/Shelf.Specs/Steps/SettingsSteps.cs` (configs are parsed in memory, directories are a set of paths).

## 3. Design

- Pure functions: the file system is a `Func<string, bool>` parameter of the locator.
- No AOT risk: no new package, no reflection.
- Decisions without ADR (no real alternative with a cost): precedence order, warning instead of error for unknown keys (forward compatibility with newer config files), a named library wins over a relative path of the same spelling.

## 4. Unit Tests

`ConfigDocumentTablesTests` (leaf keys, arrays of tables, element keys), `LibraryLocatorTests` (root library, nearest library, name vs. path, default without marker, hint message).

## 5. Open Questions

None.

## 6. Implementation Notes

- Gherkin step arguments cannot contain `"`; TOML values in example tables use literal strings (`'high'`).
- The "weak" unknown-key rule also covers `libraries` in a library config: libraries are a property of the machine, not of a library.
