@ui @F-ANN-9 @F-ANN-10 @M17
Feature: Highlights in the browser
  As a person who reads in the browser
  I want a page with a few of my highlights and the export of all of them
  So that I meet my old reading notes again and can take them to other tools

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
        { "id": "01J9Z3K00000000000000000A1", "file": "es.pdf", "kind": "highlight", "locator": "page=2", "quote": { "exact": "builds a read model" }, "color": "green", "note": "Compare with a **materialized** view.", "createdAt": "2026-10-25T10:00:00Z", "updatedAt": "2026-10-25T10:00:00Z" }
      ] }
      """
    And I have run shelf with arguments "reindex --library library"
    And the web server is running on the library "library"

  Scenario: The header leads to the highlights
    Given I open "/library" in the browser
    When I click the "Highlights" link
    Then the browser address contains "/highlights"
    And the heading "Highlights" is shown

  Scenario: The page shows a highlight with its note, its work and its place
    When I open "/highlights" in the browser
    Then the page shows the text "builds a read model"
    And the page shows the text "Compare with a"
    And the highlights show "materialized" in bold
    And the page shows the text "Event Sourcing in Practice"
    And the page shows the text "Page 2"

  Scenario: The place of a highlight opens the reader there
    When I open "/highlights" in the browser
    Then the link "Event Sourcing in Practice, Page 2" points to an address containing "#page=2"

  Scenario Outline: The export of all highlights is offered in two formats
    When I open "/highlights" in the browser
    Then the link "<link>" points to an address containing "<address>"

    Examples:
      | link         | address                                           |
      | Markdown     | /api/v1/annotations/export?format=markdown        |
      | Readwise CSV | /api/v1/annotations/export?format=readwise-csv    |

  Scenario: A library without highlights says how to make one
    Given the sidecar "library/Alander/Event Sourcing in Practice/annotations.json" is replaced by:
      """
      { "schema": 1, "annotations": [] }
      """
    And I have run shelf with arguments "reindex --library library"
    When I open "/highlights" in the browser
    Then the page shows the text "No highlights yet"
