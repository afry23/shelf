@F-LIB-6 @PRD-12.3 @M1
Feature: The index is rebuilt completely from the sidecars
  As a user whose library lives in plain folders
  I want shelf reindex to rebuild the database from the files and book.json sidecars
  So that I never lose metadata or tags when the index is deleted, damaged or outdated

  Background:
    Given an empty working directory
    And I have run shelf with arguments "init library"
    And a work folder "library/Kleppmann, Martin/Designing Data-Intensive Applications (2017)" with the files "ddia.epub, ddia.pdf" and the sidecar:
      """
      {
        "schema": 1,
        "id": "01J9Z3K0000000000000000001",
        "title": "Designing Data-Intensive Applications",
        "authors": ["Martin Kleppmann"],
        "language": "en",
        "year": 2017,
        "isbn13": "9781449373320",
        "tags": ["architektur/verteilte-systeme", "datenbanken"],
        "rating": 5,
        "readStatus": "read",
        "files": [
          { "name": "ddia.epub", "progress": { "locator": "epubcfi(/6/14!/4/2/10)", "updatedAt": "2026-10-12T19:04:00Z" } },
          { "name": "ddia.pdf" }
        ],
        "notDuplicates": { "works": ["01J8XQ20000000000000000002"], "files": [["ddia.epub", "ddia.pdf"]] }
      }
      """
    And a work folder "library/Abbott, Edwin A./Flatland" with the files "flatland.epub" and the sidecar:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000002", "title": "Flatland", "authors": ["Edwin A. Abbott"], "tags": ["datenbanken", "klassiker"], "files": [{ "name": "flatland.epub" }] }
      """

  Scenario: Reindex indexes every work and file of the library
    When I run shelf with arguments "reindex --library library"
    Then the exit code is 0
    And standard output contains "Indexed 2 works with 3 files"
    And standard error is empty

  Scenario Outline: The index holds the metadata of the sidecars
    When I run shelf with arguments "reindex --library library"
    Then the table "<table>" of the index has <rows> rows

    Examples:
      | table             | rows |
      | works             | 2    |
      | files             | 3    |
      | tags              | 3    |
      | work_tags         | 4    |
      | reading_progress  | 1    |
      | dupe_ignores      | 2    |
      | works_fts         | 2    |

  Scenario: The index holds the comparable title and the sort key of the author
    When I run shelf with arguments "reindex --library library"
    Then the work "01J9Z3K0000000000000000001" in the index has the normalized title "designing data intensive applications" and the author sort key "Kleppmann, Martin"
    And the work "01J9Z3K0000000000000000002" in the index has the normalized title "flatland" and the author sort key "Abbott, Edwin A."

  Scenario: The JSON output counts works, files and problems
    When I run shelf with arguments "reindex --library library --json"
    Then the exit code is 0
    And standard output is a JSON object with the integer properties "works=2, files=3, problems=0"

  Scenario: The library is found from the working directory
    Given I am in the folder "library/Abbott, Edwin A."
    When I run shelf with arguments "reindex"
    Then the exit code is 0
    And standard output contains "Indexed 2 works with 3 files"

  Scenario: Deleting the index loses nothing (acceptance criterion 3)
    Given I have run shelf with arguments "reindex --library library"
    And I delete the file "library/.shelf/index.db"
    When I run shelf with arguments "reindex --library library"
    Then the exit code is 0
    And the work "01J9Z3K0000000000000000001" in the index has the title "Designing Data-Intensive Applications"
    And the work "01J9Z3K0000000000000000001" in the index has the tags "architektur/verteilte-systeme, datenbanken"
    And the work "01J9Z3K0000000000000000001" in the index has the rating 5 and the status "read"
    And the work "01J9Z3K0000000000000000002" in the index has the tags "datenbanken, klassiker"
    And the file "ddia.epub" in the index has the progress "epubcfi(/6/14!/4/2/10)"

  Scenario: Reindexing again gives the same index
    Given I have run shelf with arguments "reindex --library library"
    When I run shelf with arguments "reindex --library library"
    Then the table "works" of the index has 2 rows
    And the table "files" of the index has 3 rows
    And the table "tags" of the index has 3 rows

  Scenario: Changes to a sidecar show up after reindexing
    Given I have run shelf with arguments "reindex --library library"
    And the sidecar "library/Abbott, Edwin A./Flatland/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000002", "title": "Flatland: A Romance of Many Dimensions", "authors": ["Edwin A. Abbott"], "files": [{ "name": "flatland.epub" }] }
      """
    When I run shelf with arguments "reindex --library library"
    Then the work "01J9Z3K0000000000000000002" in the index has the title "Flatland: A Romance of Many Dimensions"
    And the full-text index finds the work "01J9Z3K0000000000000000002" for the query "romance"
    And the table "tags" of the index has 2 rows

  Scenario: The index database uses WAL and leaves no WAL file behind
    When I run shelf with arguments "reindex --library library"
    Then the index database is in WAL mode
    And the file "library/.shelf/index.db-wal" does not exist

  Scenario: Reindex rebuilds an index with another schema version
    Given I have run shelf with arguments "reindex --library library"
    And the index has the schema version 99
    When I run shelf with arguments "reindex --library library"
    Then the exit code is 0
    And the table "works" of the index has 2 rows

  Scenario Outline: A broken sidecar is reported and the rest is still indexed
    Given a work folder "library/Broken/<folder>" with the files "<files>" and the sidecar:
      """
      <sidecar>
      """
    When I run shelf with arguments "reindex --library library"
    Then the exit code is 1
    And standard output contains "Indexed 2 works with 3 files, 1 problem"
    And standard error contains "warning: "
    And standard error contains "<message>"
    And the table "works" of the index has 2 rows

    Examples:
      | folder  | files   | sidecar                                                                                                                  | message                             |
      | Invalid | x.epub  | { "schema": 1, "id": "nope", "title": "T", "authors": ["A"], "files": [{ "name": "x.epub" }] }                           | id: not a valid ULID                |
      | Missing | y.epub  | { "schema": 1, "id": "01J9Z3K0000000000000000003", "title": "T", "authors": ["A"], "files": [{ "name": "gone.epub" }] } | gone.epub: file does not exist      |
      | Copy    | f.epub  | { "schema": 1, "id": "01J9Z3K0000000000000000002", "title": "Copy", "authors": ["A"], "files": [{ "name": "f.epub" }] }  | duplicate work ID 01J9Z3K0000000000000000002 |

  Scenario: A library without works has an empty index
    Given I have run shelf with arguments "init empty"
    When I run shelf with arguments "reindex --library empty"
    Then the exit code is 0
    And standard output contains "Indexed 0 works with 0 files"

  Scenario Outline: Reindex needs a library
    When I run shelf with arguments "reindex <option>"
    Then the exit code is 1
    And standard output is empty
    And standard error starts with "error: "
    And standard error contains "<message>"

    Examples:
      | option               | message                      |
      | --library nowhere    | is not a shelf library       |
      |                      | no library found             |
