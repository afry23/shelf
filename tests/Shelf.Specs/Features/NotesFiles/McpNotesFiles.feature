@F-NOTEF-6 @M15
Feature: Notes files through MCP
  As an AI assistant
  I want to see which files of a work are notes files and to search their text
  So that I can answer from the notes a person keeps with a book

  Background:
    Given an empty working directory
    And I have run shelf with arguments "init library"
    And a sample PDF "library/Alander/Event Sourcing in Practice/es.pdf" with the title "Event Sourcing in Practice", the author "Jördis Ålander" and the pages:
      | text                                            |
      | Event sourcing stores every change as an event. |
    And the sidecar "library/Alander/Event Sourcing in Practice/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000002", "title": "Event Sourcing in Practice", "authors": ["Jördis Ålander"], "language": "en", "year": 2021, "files": [{ "name": "es.pdf" }, { "name": "kindle.txt", "role": "notes" }] }
      """
    And the file "library/Alander/Event Sourcing in Practice/kindle.txt" has the content "Highlight: the saga coordinates a compensating action."
    And I have run shelf with arguments "reindex --library library"
    And the MCP server is running on the library "library"

  Scenario: get_book lists the notes file next to the book
    When I call the tool "get_book" with the arguments:
      """
      { "id": "01J9Z3K0000000000000000002" }
      """
    Then the tool call succeeds
    And the result has the array "files" with 2 items

  Scenario: search finds the work by a word of the notes file
    When I call the tool "search_books" with the arguments:
      """
      { "query": "saga" }
      """
    Then the tool call succeeds
    And the result has the number "total" equal to 1
