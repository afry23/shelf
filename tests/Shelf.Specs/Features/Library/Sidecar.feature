@F-LIB-5 @F-LIB-4 @M1
Feature: The book.json sidecar is the source of truth of a work
  As a user whose library lives in plain folders
  I want each work's metadata, tags, progress and "not a duplicate" marks stored in a readable file next to the books
  So that the library stays usable without shelf and the index can always be rebuilt

  Background:
    Given an empty working directory

  Scenario: A sidecar in the documented schema is read completely
    Given a sidecar "book.json" with content:
      """
      {
        "schema": 1,
        "id": "01J9Z3K0000000000000000000",
        "title": "Designing Data-Intensive Applications",
        "authors": ["Martin Kleppmann"],
        "language": "en",
        "year": 2017,
        "isbn13": "9781449373320",
        "publisher": "O'Reilly",
        "tags": ["architektur/verteilte-systeme", "datenbanken"],
        "rating": 5,
        "readStatus": "read",
        "files": [
          {
            "name": "Designing Data-Intensive Applications.epub",
            "progress": { "locator": "epubcfi(/6/14!/4/2/10)", "updatedAt": "2026-10-12T19:04:00Z" }
          },
          { "name": "Designing Data-Intensive Applications.pdf" }
        ],
        "notDuplicates": {
          "works": ["01J8XQ20000000000000000000"],
          "files": [["a.epub", "a (1).epub"]]
        }
      }
      """
    When shelf reads the sidecar "book.json"
    Then reading the sidecar succeeds
    And the sidecar title is "Designing Data-Intensive Applications"
    And the sidecar authors are "Martin Kleppmann"
    And the sidecar has 2 files
    And the progress of file 1 is "epubcfi(/6/14!/4/2/10)" at "2026-10-12T19:04:00Z"
    And file 2 has no progress
    And the sidecar tags are "architektur/verteilte-systeme, datenbanken"
    And the sidecar read status is "read"
    And the sidecar marks 1 work and 1 file pair as not duplicates

  Scenario: Only title, authors and files are needed for a minimal sidecar
    Given a sidecar "book.json" with content:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000000", "title": "Flatland", "authors": ["Edwin A. Abbott"], "files": [{ "name": "flatland.epub" }] }
      """
    When shelf reads the sidecar "book.json"
    Then reading the sidecar succeeds
    And the sidecar read status is "unread"
    And the sidecar has no tags

  Scenario: Writing and reading back gives the same sidecar in a readable file
    Given a new sidecar for the work "Flatland" by "Edwin A. Abbott" with the file "flatland.epub"
    When shelf writes the sidecar to "book.json"
    And shelf reads the sidecar "book.json"
    Then the sidecar read back equals the written one
    And the file "book.json" is indented UTF-8 text with LF line endings and a final newline
    And the keys of "book.json" start with "schema, id, title, authors"
    And no temporary files remain in the working directory

  Scenario: Non-ASCII text stays readable in the file
    Given a new sidecar for the work "Größenordnungen verteilter Systeme" by "Jörg O'Reilly" with the file "größen.epub"
    When shelf writes the sidecar to "book.json"
    Then the file "book.json" contains "Größenordnungen verteilter Systeme"
    And the file "book.json" contains "Jörg O'Reilly"
    And the file "book.json" does not contain "\u"

  Scenario: Optional fields that are not set are not written
    Given a new sidecar for the work "Flatland" by "Edwin A. Abbott" with the file "flatland.epub"
    When shelf writes the sidecar to "book.json"
    Then the file "book.json" does not contain "subtitle"
    And the file "book.json" does not contain "progress"
    And the file "book.json" does not contain "null"

  Scenario: Fields written by a newer version of the same schema survive a rewrite
    Given a sidecar "book.json" with content:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000000", "title": "Flatland", "authors": ["E. Abbott"], "files": [{ "name": "f.epub" }], "futureField": { "a": [1, 2] } }
      """
    When shelf reads the sidecar "book.json"
    And shelf changes the title to "Flatland: A Romance"
    And shelf writes the sidecar to "book.json"
    Then the file "book.json" contains "futureField"
    And the file "book.json" contains "Flatland: A Romance"

  Scenario: The ID of a work never changes
    Given a new sidecar for the work "Flatland" by "Edwin A. Abbott" with the file "flatland.epub"
    And shelf has written the sidecar to "book.json"
    And a new sidecar for the work "Flatland" by "Edwin A. Abbott" with the file "flatland.epub"
    When shelf writes the sidecar to "book.json"
    Then writing fails with an error containing "id must not change"

  Scenario: A schema from the future is refused instead of being damaged
    Given a sidecar "book.json" with content:
      """
      { "schema": 2, "id": "01J9Z3K0000000000000000000", "title": "T", "authors": ["A"], "files": [{ "name": "f.epub" }] }
      """
    When shelf reads the sidecar "book.json"
    Then reading the sidecar fails with an error containing "schema 2 is newer than this version of shelf supports (1)"

  Scenario Outline: Invalid sidecars are rejected with the file and the problem
    Given a sidecar "book.json" with content:
      """
      <content>
      """
    When shelf reads the sidecar "book.json"
    Then reading the sidecar fails with an error containing "<message>"

    Examples:
      | content                                                                                                             | message                                    |
      | not json                                                                                                            | book.json: not valid JSON                  |
      | []                                                                                                                  | book.json: not valid JSON                  |
      | { "id": "01J9Z3K0000000000000000000", "title": "T", "authors": ["A"], "files": [{ "name": "f.epub" }] }              | book.json: schema: must be 1 or greater    |
      | { "schema": 0, "id": "01J9Z3K0000000000000000000", "title": "T", "authors": ["A"], "files": [{ "name": "f.epub" }] } | book.json: schema: must be 1 or greater    |
      | { "schema": 1, "title": "T", "authors": ["A"], "files": [{ "name": "f.epub" }] }                                     | book.json: id: not a valid ULID            |
      | { "schema": 1, "id": "nope", "title": "T", "authors": ["A"], "files": [{ "name": "f.epub" }] }                       | book.json: id: not a valid ULID            |
      | { "schema": 1, "id": "01J9Z3K0000000000000000000", "title": " ", "authors": ["A"], "files": [{ "name": "f.epub" }] } | book.json: title: must not be empty        |
      | { "schema": 1, "id": "01J9Z3K0000000000000000000", "title": "T", "authors": [], "files": [{ "name": "f.epub" }] }   | book.json: authors: at least one author    |
      | { "schema": 1, "id": "01J9Z3K0000000000000000000", "title": "T", "authors": ["A"], "files": [] }                     | book.json: files: at least one file        |
      | { "schema": 1, "id": "01J9Z3K0000000000000000000", "title": "T", "authors": ["A"], "files": [{ "name": "a/b.epub" }] } | book.json: files[1].name: must be a file name without a folder |
      | { "schema": 1, "id": "01J9Z3K0000000000000000000", "title": "T", "authors": ["A"], "files": [{ "name": "f.epub" }, { "name": "f.epub" }] } | book.json: files[2].name: duplicate file name 'f.epub' |
      | { "schema": 1, "id": "01J9Z3K0000000000000000000", "title": "T", "authors": ["A"], "files": [{ "name": "f.epub" }], "rating": 6 } | book.json: rating: must be between 1 and 5 |
      | { "schema": 1, "id": "01J9Z3K0000000000000000000", "title": "T", "authors": ["A"], "files": [{ "name": "f.epub" }], "isbn13": "123" } | book.json: isbn13: must be 13 digits |
      | { "schema": 1, "id": "01J9Z3K0000000000000000000", "title": "T", "authors": ["A"], "files": [{ "name": "f.epub" }], "readStatus": "done" } | book.json: readStatus: must be one of unread, reading, read |
      | { "schema": 1, "id": "01J9Z3K0000000000000000000", "title": "T", "authors": ["A"], "files": [{ "name": "f.epub", "progress": { "locator": "page=1", "updatedAt": "yesterday" } }] } | book.json: files[1].progress.updatedAt: must be a UTC time like 2026-10-12T19:04:00Z |
      | { "schema": 1, "id": "01J9Z3K0000000000000000000", "title": "T", "authors": ["A"], "files": [{ "name": "f.epub" }], "notDuplicates": { "files": [["a.epub"]] } } | book.json: notDuplicates.files[1]: must be a pair of file names |
