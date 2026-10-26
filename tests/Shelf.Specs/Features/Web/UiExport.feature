@ui @F-ANN-7 @M6
Feature: Export a book from its page
  As a person who looks at one book
  I want to save its metadata, highlights and notes as a Markdown file
  So that I can use them elsewhere

  Background:
    Given an empty working directory
    And I have run shelf with arguments "init library"
    And a sample PDF "library/Alander/Event Sourcing in Practice/es.pdf" with the title "Event Sourcing in Practice", the author "Jördis Ålander" and the pages:
      | text                                            |
      | A projection builds a read model.               |
    And the sidecar "library/Alander/Event Sourcing in Practice/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000002", "title": "Event Sourcing in Practice", "authors": ["Jördis Ålander"], "year": 2021, "citeKey": "alander2021event", "files": [{ "name": "es.pdf" }] }
      """
    And a sample PDF "library/Weiss/Verteilte Systeme/vs.pdf" with the title "Verteilte Systeme verstehen", the author "Lukas Weiß" and the pages:
      | text                   |
      | Ein Konsensprotokoll.  |
    And the sidecar "library/Weiss/Verteilte Systeme/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000003", "title": "Verteilte Systeme verstehen", "authors": ["Lukas Weiß"], "files": [{ "name": "vs.pdf" }] }
      """
    And I have run shelf with arguments "reindex --library library"
    And the web server is running on the library "library"

  Scenario: A book with a cite key offers the export
    When I open "/works/01J9Z3K0000000000000000002" in the browser
    Then the link "Export Markdown" points to an address containing "/api/v1/works/01J9Z3K0000000000000000002/export.md"

  Scenario: A book without a cite key asks for one
    When I open "/works/01J9Z3K0000000000000000003" in the browser
    Then the page shows the text "Set a cite key to export"
