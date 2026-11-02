@F-ANN-9 @M17
Feature: Export of all highlights and notes over HTTP
  As the library view of the web interface
  I want to download the notes of the whole library
  So that I can hand them to another tool

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
    And the web server is running on the library "library"

  Scenario: The Readwise CSV is a download
    When I request "GET /api/v1/annotations/export?format=readwise-csv"
    Then the response status is 200
    And the response header "Content-Type" starts with "text/csv"
    And the response header "Content-Disposition" contains "attachment"
    And the response header "Content-Disposition" contains "shelf-highlights.csv"
    And the response body contains "Highlight,Title,Author,URL,Note,Location,Location Type,Date"
    And the response body contains "builds a read model,Event Sourcing in Practice"
    And the response body does not contain "Start here"

  Scenario: Markdown for the whole library is one file, and works without a cite key are left out and counted
    When I request "GET /api/v1/annotations/export?format=markdown"
    Then the response status is 200
    And the response header "Content-Type" starts with "text/markdown"
    And the response header "Content-Disposition" contains "shelf-highlights.md"
    And the response body contains "# Event Sourcing in Practice"
    And the response header "X-Shelf-Works" is "1"
    And the response header "X-Shelf-Skipped" is "1"
    And the response body does not contain "# Kubernetes Patterns"
    And the response body contains "> builds a read model"

  Scenario: A query narrows the export
    When I request "GET /api/v1/annotations/export?format=readwise-csv&q=author%3AIbryam"
    Then the response status is 200
    And the response body contains "Kubernetes Patterns"
    And the response body does not contain "Event Sourcing in Practice"

  Scenario Outline: What cannot be exported is a 400
    When I request "GET /api/v1/annotations/export<query>"
    Then the response status is 400
    And the response body contains "<message>"

    Examples:
      | query             | message  |
      | ?format=docx      | markdown |
      |                   | format   |
