@F-ANN-10 @M17
Feature: Highlights that come back, for an assistant
  As an assistant that talks to a person about their reading
  I want a few highlights of the library
  So that I can remind them of what they read

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
    And the MCP server is running on the library "library"

  Scenario: resurface_highlights gives the highlights of a day with their work
    When I call the tool "resurface_highlights" with the arguments:
      """
      { "date": "2026-11-02", "count": 2 }
      """
    Then the tool call succeeds
    And the result has the string "date" equal to "2026-11-02"
    And the result has the array "highlights" with 2 items

  Scenario Outline: The tool refuses what it cannot do
    When I call the tool "resurface_highlights" with the arguments:
      """
      <arguments>
      """
    Then the tool call fails with the message containing "<message>"

    Examples:
      | arguments            | message |
      | { "count": 0 }       | count   |
      | { "date": "someday" } | date    |
