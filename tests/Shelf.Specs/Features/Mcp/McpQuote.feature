@F-ANN-8 @PRD-7.2 @M11
Feature: A passage as a quote through MCP
  As an AI assistant
  I want a passage as a quote with a correct citation
  So that what I write for the person cites the book the way their notes do

  Background:
    Given an empty working directory
    And I have run shelf with arguments "init library"
    And a sample PDF "library/Alander/Event Sourcing in Practice/es.pdf" with the title "Event Sourcing in Practice", the author "Jördis Ålander" and the pages:
      | text                                            |
      | Event sourcing stores every change as an event. |
      | A projection builds a read model.               |
    And the sidecar "library/Alander/Event Sourcing in Practice/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000002", "title": "Event Sourcing in Practice", "authors": ["Jördis Ålander"], "language": "en", "year": 2021, "citeKey": "alander2021event", "files": [{ "name": "es.pdf" }] }
      """
    And I have run shelf with arguments "reindex --library library"
    And the MCP server is running on the library "library"

  Scenario: cite_passage gives the quote with the citation of the place
    When I call the tool "cite_passage" with the arguments:
      """
      { "id": "01J9Z3K0000000000000000002", "quote": "builds a read model", "locator": "page=2" }
      """
    Then the tool call succeeds
    And the result has the string "citeKey" equal to "alander2021event"
    And the result has the string "place" equal to "page 2"
    And the result has the string "text" containing "> builds a read model"
    And the result has the string "text" containing "[@alander2021event, p. 2]"
    And the result has the string "bibtex" containing "alander2021event,"

  Scenario Outline: The tool refuses what it cannot do and changes nothing
    Given I take a snapshot of the folder "library"
    When I call the tool "cite_passage" with the arguments:
      """
      <arguments>
      """
    Then the tool call fails with the message containing "<message>"
    And the folder "library" equals the snapshot

    Examples:
      | arguments                                                                       | message  |
      | { "id": "01ZZZZZZZZZZZZZZZZZZZZZZZZ", "quote": "x" }                            | work     |
      | { "id": "01J9Z3K0000000000000000002", "quote": "  " }                           | quote    |
      | { "id": "01J9Z3K0000000000000000002", "quote": "x", "locator": "chapter 2" }    | locator  |
      | { "id": "01J9Z3K0000000000000000002", "quote": "x", "file": "gone.pdf" }        | gone.pdf |

  Scenario: cite_passage only reads
    Then the tool "cite_passage" is read-only
