@F-ANN-10 @M17
Feature: Highlights that come back, over HTTP
  As the start page of the web interface
  I want a few highlights of the library for a day
  So that old reading notes show up again

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

  Scenario: The highlights of a day come with their work and place
    When I request "GET /api/v1/annotations/resurface?date=2026-11-02&count=2"
    Then the response status is 200
    And the response is a JSON document
    And the JSON array "highlights" of the response has 2 entries
    And the JSON field "date" of the response is "2026-11-02"

  Scenario: The same day gives the same answer
    When I request "GET /api/v1/annotations/resurface?date=2026-11-02&count=1"
    And I note the JSON field "highlights[0].id" of the response as the entry
    And I request "GET /api/v1/annotations/resurface?date=2026-11-02&count=1"
    Then the JSON field "highlights[0].id" of the response is the noted entry

  Scenario: The default is three highlights, and only highlights come
    When I request "GET /api/v1/annotations/resurface?date=2026-11-02"
    Then the response status is 200
    And the JSON array "highlights" of the response has 2 entries
    And the response body does not contain "Start here"

  Scenario Outline: A count or a date that makes no sense is a 400
    When I request "GET /api/v1/annotations/resurface?<query>"
    Then the response status is 400
    And the response body contains "<message>"

    Examples:
      | query          | message |
      | count=0        | count   |
      | count=500      | count   |
      | date=yesterday | date    |
