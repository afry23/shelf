# Slice m1-model-sidecar: Work Model, book.json and ULID

| | |
|---|---|
| **Slice ID** | `m1-model-sidecar` |
| **Branch** | `feat/m1-model-sidecar` (stacked on `feat/m1-config`) |
| **Milestone** | M1 – Foundation |
| **Roadmap** | M1 area "Library and data model"; note "`book.json` already uses the final schema from 10.4" |
| **PRD references** | F-LIB-4, F-LIB-5, 6.1 (metadata per work), 10.4 (`book.json`), 9.3 (readable files, no sync) |
| **Inputs** | PRD 10.4 example, `src/Shelf.Core` |

---

## 1. Scope

**In scope**

1. `Ulid` (own implementation, decided by the owner): 48 bit milliseconds + 80 random bits, 26 characters Crockford base32, case-insensitive parsing, sorts by time.
2. `WorkSidecar` and its parts (`SidecarFile`, `FileProgress`, `NotDuplicates`, `ReadStatus`): the complete schema 1 including `files[].progress` and `notDuplicates`, plus the optional metadata of PRD 6.1 (`subtitle`, `isbn10`, `series`/`seriesIndex`, `edition`, `description`, `added`, `cover`).
3. `SidecarFormat`: parse, validate, serialize. Hand-written on `JsonDocument`/`Utf8JsonWriter`: exact keys in messages (`files[2].name`), fixed key order, no reflection (Native AOT).
4. `SidecarStore`: read, and atomic write (temp file in the same folder, flush, move over). A write never changes the ID of an existing file, and never overwrites a file it cannot read.
5. Unknown fields are kept and written back (forward compatibility within a schema).
6. A `schema` newer than this version is refused (no silent damage); the migration of older schemas has no case yet (only schema 1 exists) and is added with the first breaking change.

**Requirements covered:** F-LIB-4 (a work has one or more files), F-LIB-5.

**Out of scope**

- Creating works from files, the work folder layout, hashes/sizes (stored in the index, not in the sidecar): `m1-import-scan`.
- Tag syntax (hierarchy rules): M2. Tags are non-empty strings here.
- Journal for file operations: M2. The atomic write covers the sidecar itself.

## 2. Feature Files

- `Features/Library/Ulid.feature` (`@F-LIB-5 @M1`): vectors from an independent computation, ordering, uniqueness, parsing.
- `Features/Library/Sidecar.feature` (`@F-LIB-5 @F-LIB-4 @M1`): documented example, minimal sidecar, round trip and file format, optional fields, non-ASCII, unknown fields, immutable ID, newer schema, 17 invalid-sidecar examples.

## 3. Design

- Rules checked by `Validate` (read and write): title not blank; at least one author, none blank; rating 1..5; ISBN-13 13 digits, ISBN-10 9 digits + digit/X (no check digit yet, that is `m1-extraction`); tags not blank; at least one file; file names without folder and unique; progress locator not blank; "not a duplicate" file pairs are file names.
- Decision without ADR: **at least one author** is required (PRD: title and authors are required). The importer must supply one (for example from the file name) or send the file to review instead of creating a work.
- `updatedAt`/`added` are UTC with second precision (`2026-10-12T19:04:00Z`), as in the PRD example.
- The file is written with `UnsafeRelaxedJsonEscaping` so that `Größe` and `O'Reilly` stay readable; the file is never embedded in HTML.
- AOT: no new package; `IsAotCompatible` analyzers report nothing.

## 4. Unit Tests

`UlidTests` (bounds, clock injection, ordering), `SidecarFormatTests` (all fields round trip, UTC times, JSON null, type errors, refusal to overwrite an unreadable file, missing file).

## 5. Open Questions

None.

## 6. Implementation Notes

- A step named "reading succeeds" already exists for config files; sidecar steps say "reading the sidecar …" to avoid ambiguous bindings.
- `Tags`, `Authors` and `Files` are `IReadOnlyList<T>`; record equality compares references, so tests compare the serialized text.
