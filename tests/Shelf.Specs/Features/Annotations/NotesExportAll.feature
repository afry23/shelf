@F-ANN-9 @M17
Feature: Export of the highlights and notes of the whole library
  As a person who takes reading notes into other tools
  I want all highlights and notes in one go, as Markdown files or as a Readwise CSV
  So that I do not have to export book by book

  Background:
    Given an empty working directory
    And I have run shelf with arguments "init library"
    And a sample PDF "library/Alander/Event Sourcing in Practice/es.pdf" with the title "Event Sourcing in Practice", the author "Jördis Ålander" and the pages:
      | text                                            |
      | Event sourcing stores every change as an event. |
      | A projection builds a read model.               |
    And the sidecar "library/Alander/Event Sourcing in Practice/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000002", "title": "Event Sourcing in Practice", "authors": ["Jördis Ålander"], "year": 2021, "citeKey": "alander2021event", "files": [{ "name": "es.pdf" }] }
      """
    And the sidecar "library/Alander/Event Sourcing in Practice/annotations.json" is replaced by:
      """
      { "schema": 1, "annotations": [
        { "id": "01J9Z3K00000000000000000A1", "file": "es.pdf", "kind": "highlight", "locator": "page=2", "quote": { "exact": "builds a read model" }, "color": "green", "note": "Compare with a **materialized** view.", "createdAt": "2026-10-25T10:00:00Z", "updatedAt": "2026-10-25T10:00:00Z" },
        { "id": "01J9Z3K00000000000000000A2", "file": "es.pdf", "kind": "note", "note": "Ask about the workshop agenda.", "createdAt": "2026-10-25T10:05:00Z", "updatedAt": "2026-10-25T10:05:00Z" },
        { "id": "01J9Z3K00000000000000000A3", "file": "es.pdf", "kind": "bookmark", "locator": "page=1", "note": "Start here", "createdAt": "2026-10-25T10:06:00Z", "updatedAt": "2026-10-25T10:06:00Z" }
      ] }
      """
    And a sample PDF "library/Ibryam/Kubernetes Patterns/kp.pdf" with the title "Kubernetes Patterns", the author "Bilgin Ibryam" and the pages:
      | text                 |
      | The sidecar pattern. |
    And the sidecar "library/Ibryam/Kubernetes Patterns/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000001", "title": "Kubernetes Patterns", "authors": ["Bilgin Ibryam"], "files": [{ "name": "kp.pdf" }] }
      """
    And the sidecar "library/Ibryam/Kubernetes Patterns/annotations.json" is replaced by:
      """
      { "schema": 1, "annotations": [
        { "id": "01J9Z3K00000000000000000B1", "file": "kp.pdf", "kind": "highlight", "locator": "page=1", "quote": { "exact": "The sidecar pattern" }, "color": "yellow", "createdAt": "2026-10-26T09:00:00Z", "updatedAt": "2026-10-26T09:00:00Z" }
      ] }
      """
    And I have run shelf with arguments "reindex --library library"

  Scenario: All works with a cite key are written as Markdown files into a folder
    When I run shelf with arguments "notes export --all --out export --library library"
    Then the exit code is 0
    And the file "export/alander2021event.md" contains "> builds a read model"
    And the file "export/alander2021event.md" contains "Ask about the workshop agenda."
    And standard output contains "1 work"
    And standard output contains "Kubernetes Patterns"
    And standard output contains "cite key"

  Scenario: Markdown for all works needs a folder
    When I run shelf with arguments "notes export --all --library library"
    Then the exit code is 1
    And standard error contains "--out"

  Scenario: A query narrows the export to the works it finds
    When I run shelf with arguments "notes export --all --query 'author:Alander' --out export --library library"
    Then the exit code is 0
    And the file "export/alander2021event.md" exists
    And standard output does not contain "Kubernetes Patterns"

  Scenario: The Readwise CSV has one row per highlight and per note, and no bookmarks
    When I run shelf with arguments "notes export --all --format readwise-csv --library library"
    Then the exit code is 0
    And standard output contains "Highlight,Title,Author,URL,Note,Location,Location Type,Date"
    And standard output contains "builds a read model,Event Sourcing in Practice,Jördis Ålander,,Compare with a **materialized** view.,2,page,2026-10-25"
    And standard output contains "The sidecar pattern,Kubernetes Patterns,Bilgin Ibryam,,,1,page,2026-10-26"
    And standard output contains ",Event Sourcing in Practice,Jördis Ålander,,Ask about the workshop agenda."
    And standard output does not contain "Start here"

  Scenario: The CSV is written to a file and works without a cite key are included
    When I run shelf with arguments "notes export --all --format readwise-csv --out readwise.csv --library library"
    Then the exit code is 0
    And the file "readwise.csv" contains "Kubernetes Patterns"

  Scenario: Quotes with commas, quotation marks and line breaks stay one CSV field
    Given the sidecar "library/Ibryam/Kubernetes Patterns/annotations.json" is replaced by:
      """
      { "schema": 1, "annotations": [
        { "id": "01J9Z3K00000000000000000B1", "file": "kp.pdf", "kind": "highlight", "locator": "page=1", "quote": { "exact": "A \"sidecar\", or helper,\nruns next to the app" }, "color": "yellow", "createdAt": "2026-10-26T09:00:00Z", "updatedAt": "2026-10-26T09:00:00Z" }
      ] }
      """
    And I have run shelf with arguments "reindex --library library"
    When I run shelf with arguments "notes export --all --format readwise-csv --library library"
    Then the exit code is 0
    And standard output contains '"A ""sidecar"", or helper,'
    And standard output contains 'runs next to the app"'

  Scenario: The export is also available as JSON
    When I run shelf with arguments "notes export --all --out export --json --library library"
    Then the exit code is 0
    And standard output contains '"works":1'
    And standard output contains '"skipped"'

  Scenario Outline: A format that does not exist is refused and nothing is written
    When I run shelf with arguments "notes export --all --format <format> --out export --library library"
    Then the exit code is 1
    And standard error contains "markdown"
    And the file "export/alander2021event.md" does not exist

    Examples:
      | format |
      | docx   |
      | json   |

  Scenario: An ID together with --all is refused
    When I run shelf with arguments "notes export 01J9Z3K0000000000000000002 --all --out export --library library"
    Then the exit code is 1
    And standard error contains "--all"
