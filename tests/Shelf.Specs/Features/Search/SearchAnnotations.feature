@F-ANN-5 @M6
Feature: Search finds highlights and notes
  As a person who wrote notes in a book
  I want the search to find the words of my notes and highlights
  So that I find a book by what I thought about it, and jump to the place

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
    And a sample PDF "library/Ibryam/Kubernetes Patterns/kp.pdf" with the title "Kubernetes Patterns", the author "Bilgin Ibryam" and the pages:
      | text                                              |
      | The sidecar container extends the main container. |
    And the sidecar "library/Ibryam/Kubernetes Patterns/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000001", "title": "Kubernetes Patterns", "authors": ["Bilgin Ibryam"], "language": "en", "year": 2019, "files": [{ "name": "kp.pdf" }] }
      """
    And the sidecar "library/Alander/Event Sourcing in Practice/annotations.json" is replaced by:
      """
      { "schema": 1, "annotations": [
        { "id": "01J9Z3K00000000000000000A1", "file": "es.pdf", "kind": "highlight", "locator": "page=2", "quote": { "exact": "builds a read model" }, "color": "yellow", "note": "Compare with a **materialized** view.", "createdAt": "2026-10-25T10:00:00Z", "updatedAt": "2026-10-25T10:00:00Z" },
        { "id": "01J9Z3K00000000000000000A2", "file": "es.pdf", "kind": "note", "note": "Ask about the workshop agenda.", "createdAt": "2026-10-25T10:00:00Z", "updatedAt": "2026-10-25T10:00:00Z" }
      ] }
      """
    And I have run shelf with arguments "reindex --library library"

  Scenario: A word that is only in the note of a highlight finds the work and shows the place
    When I run shelf with arguments "search materialized --library library"
    Then the exit code is 0
    And the search results are, in any order: "Event Sourcing in Practice"
    And standard output contains "highlight, page 2"

  Scenario: A word in a note for the whole file finds the work
    When I run shelf with arguments "search workshop --library library"
    Then the search results are, in any order: "Event Sourcing in Practice"
    And standard output contains "Ask about the [workshop] agenda"

  Scenario: The marked text of a highlight is searchable
    When I run shelf with arguments "search 'read model' --library library"
    Then the search results are, in any order: "Event Sourcing in Practice"

  Scenario: The location of a hit in a note is given in the JSON output
    When I run shelf with arguments "search materialized --json --library library"
    Then standard output contains "page=2"

  Scenario: A search with a filter still applies to the notes
    When I run shelf with arguments "search materialized author:ibryam --library library"
    Then the search results are, in any order: ""
