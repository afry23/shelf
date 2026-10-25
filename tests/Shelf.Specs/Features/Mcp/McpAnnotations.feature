@F-ANN-6 @M6
Feature: Highlights and notes through MCP
  As an AI assistant
  I want to read the highlights and notes of a book and to add a note
  So that I can work with what the person marked and wrote

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
        { "id": "01J9Z3K00000000000000000A1", "file": "es.pdf", "kind": "highlight", "locator": "page=2", "quote": { "exact": "builds a read model" }, "color": "green", "note": "Compare with a **materialized** view.", "createdAt": "2026-10-25T10:00:00Z", "updatedAt": "2026-10-25T10:00:00Z" }
      ] }
      """
    And I have run shelf with arguments "reindex --library library"
    And the MCP server is running on the library "library"

  Scenario: get_annotations gives the highlights and notes of a work with their locations
    When I call the tool "get_annotations" with the arguments:
      """
      { "id": "01J9Z3K0000000000000000002" }
      """
    Then the tool call succeeds
    And the result has the string "title" equal to "Event Sourcing in Practice"
    And the result has the array "annotations" with 1 items

  Scenario: add_note adds a note for the file of the work
    When I call the tool "add_note" with the arguments:
      """
      { "id": "01J9Z3K0000000000000000002", "text": "The person wants a **summary** of chapter 2." }
      """
    Then the tool call succeeds
    And the result has the string "kind" equal to "note"
    And the result has the string "note" containing "summary"
    And the file "library/Alander/Event Sourcing in Practice/annotations.json" contains "summary"
    When I call the tool "get_annotations" with the arguments:
      """
      { "id": "01J9Z3K0000000000000000002" }
      """
    Then the result has the array "annotations" with 2 items

  Scenario Outline: The tools refuse what they cannot do and change nothing
    Given I take a snapshot of the folder "library"
    When I call the tool "<tool>" with the arguments:
      """
      <arguments>
      """
    Then the tool call fails with the message containing "<message>"
    And the folder "library" equals the snapshot

    Examples:
      | tool            | arguments                                                              | message  |
      | get_annotations | { "id": "01ZZZZZZZZZZZZZZZZZZZZZZZZ" }                                 | work     |
      | add_note        | { "id": "01ZZZZZZZZZZZZZZZZZZZZZZZZ", "text": "x" }                    | work     |
      | add_note        | { "id": "01J9Z3K0000000000000000002", "text": "  " }                   | note     |
      | add_note        | { "id": "01J9Z3K0000000000000000002", "text": "x", "file": "gone.pdf" } | gone.pdf |

  Scenario: add_note is a write tool, get_annotations only reads
    Then the tool "add_note" is not read-only and not destructive
    And the tool "get_annotations" is read-only
