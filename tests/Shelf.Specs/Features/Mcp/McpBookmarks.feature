@F-RDR-6 @F-ANN-6 @M10
Feature: Bookmarks through MCP
  As an AI assistant
  I want to set a bookmark at a place in a book
  So that I can mark places for the person to come back to

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
    And I have run shelf with arguments "reindex --library library"
    And the MCP server is running on the library "library"

  Scenario: add_bookmark sets a bookmark in the file of the work
    When I call the tool "add_bookmark" with the arguments:
      """
      { "id": "01J9Z3K0000000000000000002", "locator": "page=2", "label": "The read model" }
      """
    Then the tool call succeeds
    And the result has the string "kind" equal to "bookmark"
    And the result has the string "locator" equal to "page=2"
    And the result has the string "note" equal to "The read model"
    When I call the tool "get_annotations" with the arguments:
      """
      { "id": "01J9Z3K0000000000000000002" }
      """
    Then the result has the array "annotations" with 1 items

  Scenario Outline: The tool refuses what it cannot do and changes nothing
    Given I take a snapshot of the folder "library"
    When I call the tool "add_bookmark" with the arguments:
      """
      <arguments>
      """
    Then the tool call fails with the message containing "<message>"
    And the folder "library" equals the snapshot

    Examples:
      | arguments                                                                       | message  |
      | { "id": "01ZZZZZZZZZZZZZZZZZZZZZZZZ", "locator": "page=1" }                     | work     |
      | { "id": "01J9Z3K0000000000000000002", "locator": "chapter 2" }                  | locator  |
      | { "id": "01J9Z3K0000000000000000002", "locator": "page=1", "file": "gone.pdf" } | gone.pdf |

  Scenario: add_bookmark is a write tool
    Then the tool "add_bookmark" is not read-only and not destructive
