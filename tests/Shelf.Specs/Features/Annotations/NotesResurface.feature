@F-ANN-10 @M17
Feature: Highlights that come back
  As a person who read a lot and forgets a lot
  I want a few highlights from the library each day
  So that I meet my old reading notes again

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

  Scenario: The default is three highlights, bookmarks and notes without a quote are left out
    When I run shelf with arguments "notes resurface --date 2026-11-02 --library library"
    Then the exit code is 0
    And standard output contains "builds a read model"
    And standard output contains "The sidecar pattern"
    And standard output does not contain "Start here"
    And standard output does not contain "Ask about the workshop agenda."

  Scenario: Each highlight says its work and its place
    When I run shelf with arguments "notes resurface --date 2026-11-02 --library library"
    Then standard output contains "Event Sourcing in Practice"
    And standard output contains "page 2"
    And standard output contains "Compare with a **materialized** view."

  Scenario: The same day gives the same highlights
    When I run shelf with arguments "notes resurface --count 1 --date 2026-11-02 --json --library library"
    Then the exit code is 0
    And I note the standard output as "first"
    When I run shelf with arguments "notes resurface --count 1 --date 2026-11-02 --json --library library"
    Then the standard output is the noted "first"

  Scenario: The count limits the number of highlights
    When I run shelf with arguments "notes resurface --count 1 --date 2026-11-02 --json --library library"
    Then the exit code is 0
    And the JSON property "highlights" of standard output has 1 items

  Scenario: A library without highlights says so
    Given the sidecar "library/Alander/Event Sourcing in Practice/annotations.json" is replaced by:
      """
      { "schema": 1, "annotations": [] }
      """
    And the sidecar "library/Ibryam/Kubernetes Patterns/annotations.json" is replaced by:
      """
      { "schema": 1, "annotations": [] }
      """
    And I have run shelf with arguments "reindex --library library"
    When I run shelf with arguments "notes resurface --date 2026-11-02 --library library"
    Then the exit code is 0
    And standard output contains "No highlights"

  Scenario Outline: A count or a date that makes no sense is refused
    When I run shelf with arguments "notes resurface <arguments> --library library"
    Then the exit code is 1
    And standard error contains "<message>"

    Examples:
      | arguments            | message |
      | --count 0            | count   |
      | --count -2           | count   |
      | --date yesterday     | date    |
      | --date 2026-13-40    | date    |
