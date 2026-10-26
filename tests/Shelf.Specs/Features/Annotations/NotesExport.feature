@F-ANN-7 @M6
Feature: Export of a work as one Markdown file
  As a person who writes about books
  I want the metadata, highlights and notes of a work in one Markdown file named after its cite key
  So that I can take my reading notes into other tools

  Background:
    Given an empty working directory
    And I have run shelf with arguments "init library"
    And a sample PDF "library/Alander/Event Sourcing in Practice/es.pdf" with the title "Event Sourcing in Practice", the author "Jördis Ålander" and the pages:
      | text                                            |
      | Event sourcing stores every change as an event. |
      | A projection builds a read model.               |
    And the sidecar "library/Alander/Event Sourcing in Practice/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000002", "title": "Event Sourcing in Practice", "subtitle": "Patterns: a guide", "authors": ["Jördis Ålander"], "language": "en", "year": 2021, "publisher": "Example Press", "tags": ["architecture/ddd"], "citeKey": "alander2021event", "files": [{ "name": "es.pdf" }] }
      """
    And the sidecar "library/Alander/Event Sourcing in Practice/annotations.json" is replaced by:
      """
      { "schema": 1, "annotations": [
        { "id": "01J9Z3K00000000000000000A1", "file": "es.pdf", "kind": "highlight", "locator": "page=2", "quote": { "exact": "builds a read model" }, "color": "green", "note": "Compare with a **materialized** view.", "createdAt": "2026-10-25T10:00:00Z", "updatedAt": "2026-10-25T10:00:00Z" },
        { "id": "01J9Z3K00000000000000000A2", "file": "es.pdf", "kind": "note", "note": "Ask about the workshop agenda.", "createdAt": "2026-10-25T10:05:00Z", "updatedAt": "2026-10-25T10:05:00Z" }
      ] }
      """
    And I have run shelf with arguments "reindex --library library"

  Scenario: The export carries the metadata, the highlights and the notes
    When I run shelf with arguments "notes export 01J9Z3K0000000000000000002 --library library"
    Then the exit code is 0
    And standard output contains "citekey: alander2021event"
    And standard output contains 'title: "Event Sourcing in Practice"'
    And standard output contains 'subtitle: "Patterns: a guide"'
    And standard output contains "year: 2021"
    And standard output contains "publisher: Example Press"
    And standard output contains "architecture/ddd"
    And standard output contains "# Event Sourcing in Practice"
    And standard output contains "> builds a read model"
    And standard output contains "Compare with a **materialized** view."
    And standard output contains "Ask about the workshop agenda."

  Scenario: The file is named after the cite key
    When I run shelf with arguments "notes export 01J9Z3K0000000000000000002 --out export --library library"
    Then the exit code is 0
    And standard output contains "alander2021event.md"
    And the file "export/alander2021event.md" contains "> builds a read model"
    And the file "export/alander2021event.md" contains "citekey: alander2021event"

  Scenario Outline: A cite key that is not fit for a file name is made one
    Given the sidecar "library/Alander/Event Sourcing in Practice/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000002", "title": "Event Sourcing in Practice", "authors": ["Jördis Ålander"], "citeKey": "<key>", "files": [{ "name": "es.pdf" }] }
      """
    When I run shelf with arguments "notes export 01J9Z3K0000000000000000002 --out export --library library"
    Then the exit code is 0
    And the file "export/<file>" contains "<key>"

    Examples:
      | key                  | file                   |
      | ibryam2019kubernetes | ibryam2019kubernetes.md |
      | a/b:c                | a-b-c.md               |
      | smith-2020_x+y       | smith-2020_x+y.md      |

  Scenario: A work without annotations still exports its metadata
    Given the sidecar "library/Alander/Event Sourcing in Practice/annotations.json" is replaced by:
      """
      { "schema": 1, "annotations": [] }
      """
    And I have run shelf with arguments "reindex --library library"
    When I run shelf with arguments "notes export 01J9Z3K0000000000000000002 --library library"
    Then the exit code is 0
    And standard output contains "citekey: alander2021event"
    And standard output contains "No highlights or notes"

  Scenario Outline: A work without a cite key is not exported and nothing is written
    Given the sidecar "library/Alander/Event Sourcing in Practice/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000002", "title": "Event Sourcing in Practice", "authors": ["Jördis Ålander"], "files": [{ "name": "es.pdf" }] }
      """
    When I run shelf with arguments "notes export 01J9Z3K0000000000000000002 <more> --library library"
    Then the exit code is 1
    And standard error contains "cite key"
    And the file "export/alander2021event.md" does not exist

    Examples:
      | more        |
      |             |
      | --out export |
