@F-ANN-6 @M6
Feature: Highlights and notes on the command line
  As a person who works in the terminal
  I want to list, add, remove and export the notes of a book
  So that my notes are not locked into the web interface

  Background:
    Given an empty working directory
    And I have run shelf with arguments "init library"
    And a sample PDF "library/Alander/Event Sourcing in Practice/es.pdf" with the title "Event Sourcing in Practice", the author "Jördis Ålander" and the pages:
      | text                                            |
      | Event sourcing stores every change as an event. |
      | A projection builds a read model.               |
    And the sidecar "library/Alander/Event Sourcing in Practice/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000002", "title": "Event Sourcing in Practice", "authors": ["Jördis Ålander"], "language": "en", "year": 2021, "files": [{ "name": "es.pdf" }] }
      """
    And the sidecar "library/Alander/Event Sourcing in Practice/annotations.json" is replaced by:
      """
      { "schema": 1, "annotations": [
        { "id": "01J9Z3K00000000000000000A1", "file": "es.pdf", "kind": "highlight", "locator": "page=2", "quote": { "exact": "builds a read model" }, "color": "green", "note": "Compare with a **materialized** view.", "createdAt": "2026-10-25T10:00:00Z", "updatedAt": "2026-10-25T10:00:00Z" },
        { "id": "01J9Z3K00000000000000000A2", "file": "es.pdf", "kind": "note", "note": "Ask about the workshop agenda.", "createdAt": "2026-10-25T10:05:00Z", "updatedAt": "2026-10-25T10:05:00Z" }
      ] }
      """
    And I have run shelf with arguments "reindex --library library"

  Scenario: The highlights and notes of a work are listed with their place
    When I run shelf with arguments "notes list 01J9Z3K0000000000000000002 --library library"
    Then the exit code is 0
    And standard output contains "page 2"
    And standard output contains "builds a read model"
    And standard output contains "Compare with a **materialized** view."
    And standard output contains "Ask about the workshop agenda."
    And standard output contains "01J9Z3K00000000000000000A1"

  Scenario: The list is available as JSON
    When I run shelf with arguments "notes list 01J9Z3K0000000000000000002 --json --library library"
    Then the exit code is 0
    And standard output contains '"kind":"highlight"'
    And standard output contains '"locator":"page=2"'
    And standard output contains '"color":"green"'

  Scenario: A work without notes says so
    Given a sample PDF "library/Ibryam/Kubernetes Patterns/kp.pdf" with the title "Kubernetes Patterns", the author "Bilgin Ibryam" and the pages:
      | text                 |
      | The sidecar pattern. |
    And the sidecar "library/Ibryam/Kubernetes Patterns/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000001", "title": "Kubernetes Patterns", "authors": ["Bilgin Ibryam"], "files": [{ "name": "kp.pdf" }] }
      """
    And I have run shelf with arguments "reindex --library library"
    When I run shelf with arguments "notes list 01J9Z3K0000000000000000001 --library library"
    Then the exit code is 0
    And standard output contains "No highlights or notes"

  Scenario: A note for the file is added and found by the search
    When I run shelf with arguments "notes add 01J9Z3K0000000000000000002 'Read the *snapshot* chapter again' --library library"
    Then the exit code is 0
    And the file "library/Alander/Event Sourcing in Practice/annotations.json" contains "Read the *snapshot* chapter again"
    When I run shelf with arguments "search snapshot --library library"
    Then the search results are, in any order: "Event Sourcing in Practice"

  Scenario: A work with several files needs the file for a new note
    Given a sample EPUB "library/Alander/Event Sourcing in Practice/es.epub" with the title "Event Sourcing in Practice", the authors "Jördis Ålander" and the chapters:
      | title | text            |
      | One   | Events are facts. |
    And the sidecar "library/Alander/Event Sourcing in Practice/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000002", "title": "Event Sourcing in Practice", "authors": ["Jördis Ålander"], "language": "en", "year": 2021, "files": [{ "name": "es.pdf" }, { "name": "es.epub" }] }
      """
    And I have run shelf with arguments "reindex --library library"
    When I run shelf with arguments "notes add 01J9Z3K0000000000000000002 'x' --library library"
    Then the exit code is 2
    And standard error contains "--file"
    When I run shelf with arguments "notes add 01J9Z3K0000000000000000002 'about the epub' --file es.epub --library library"
    Then the exit code is 0
    And the file "library/Alander/Event Sourcing in Practice/annotations.json" contains "about the epub"

  Scenario Outline: A note that cannot be added is refused and nothing changes
    Given I take a snapshot of the folder "library"
    When I run shelf with arguments "notes add <work> '<text>' <more> --library library"
    Then the exit code is <code>
    And standard error contains "<message>"
    And the folder "library" equals the snapshot

    Examples:
      | work                       | text | more           | code | message         |
      | 01J9Z3K0000000000000000002 |      |                | 2    | text            |
      | 01J9Z3K0000000000000000002 | x    | --file gone.pdf | 1   | gone.pdf        |
      | 01J9Z3K0000000000000000009 | x    |                | 1    | no work matches |

  Scenario: An annotation is removed by its ID
    When I run shelf with arguments "notes rm 01J9Z3K0000000000000000002 01J9Z3K00000000000000000A2 --library library"
    Then the exit code is 0
    And the file "library/Alander/Event Sourcing in Practice/annotations.json" does not contain "workshop agenda"
    And the file "library/Alander/Event Sourcing in Practice/annotations.json" contains "builds a read model"

  Scenario: An annotation that does not exist cannot be removed
    Given I take a snapshot of the folder "library"
    When I run shelf with arguments "notes rm 01J9Z3K0000000000000000002 01J9Z3K00000000000000000FF --library library"
    Then the exit code is 1
    And standard error contains "no annotation"
    And the folder "library" equals the snapshot

  Scenario: The notes of a work are exported as Markdown
    When I run shelf with arguments "notes export 01J9Z3K0000000000000000002 --library library"
    Then the exit code is 0
    And standard output contains "# Event Sourcing in Practice"
    And standard output contains "> builds a read model"
    And standard output contains "Compare with a **materialized** view."
    And standard output contains "Ask about the workshop agenda."
