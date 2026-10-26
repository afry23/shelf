@F-ANN-7 @M6
Feature: Export of a work over HTTP
  As the export button of the web interface
  I want one Markdown file with the metadata, highlights and notes of a work
  So that it can be saved under the cite key of the work

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

  Scenario: The export is a Markdown download named after the cite key
    When I request "GET /api/v1/works/01J9Z3K0000000000000000002/export.md"
    Then the response status is 200
    And the response header "Content-Type" starts with "text/markdown"
    And the response header "Content-Disposition" contains "attachment"
    And the response header "Content-Disposition" contains "alander2021event.md"
    And the response body contains "citekey: alander2021event"
    And the response body contains "# Event Sourcing in Practice"
    And the response body contains "> builds a read model"
    And the response body contains "Compare with a **materialized** view."

  Scenario: A work without a cite key cannot be exported
    Given the sidecar "library/Alander/Event Sourcing in Practice/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000002", "title": "Event Sourcing in Practice", "authors": ["Jördis Ålander"], "files": [{ "name": "es.pdf" }] }
      """
    And I have run shelf with arguments "reindex --library library"
    When I request "GET /api/v1/works/01J9Z3K0000000000000000002/export.md"
    Then the response status is 400
    And the response body contains "cite key"

  Scenario: An unknown work is a 404
    When I request "GET /api/v1/works/01J9Z3K0000000000000000009/export.md"
    Then the response status is 404
