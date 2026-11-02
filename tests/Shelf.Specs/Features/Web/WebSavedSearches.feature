@F-SRC-10 @M19
Feature: Saved searches over HTTP
  As the library view of the web interface
  I want to list, save and remove saved searches
  So that a person finds their searches in the sidebar

  Background:
    Given an empty working directory
    And I have run shelf with arguments "init library"
    And a sample PDF "library/Ibryam/Kubernetes Patterns/kp.pdf" with the title "Kubernetes Patterns", the author "Bilgin Ibryam" and the pages:
      | text                                     |
      | The sidecar container extends the app.   |
    And the sidecar "library/Ibryam/Kubernetes Patterns/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000001", "title": "Kubernetes Patterns", "authors": ["Bilgin Ibryam"], "language": "en", "year": 2019, "tags": ["cloud/kubernetes"], "readStatus": "read", "files": [{ "name": "kp.pdf" }] }
      """
    And a sample PDF "library/Alander/Event Sourcing in Practice/es.pdf" with the title "Event Sourcing in Practice", the author "Jördis Ålander" and the pages:
      | text                                            |
      | A projection builds a read model for kubernetes. |
    And the sidecar "library/Alander/Event Sourcing in Practice/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000002", "title": "Event Sourcing in Practice", "authors": ["Jördis Ålander"], "language": "en", "year": 2021, "tags": ["architektur/ddd"], "files": [{ "name": "es.pdf" }] }
      """
    And a sample PDF "library/Weiss/Verteilte Systeme/vs.pdf" with the title "Verteilte Systeme verstehen", the author "Lukas Weiß" and the pages:
      | text                    |
      | Ein Konsensprotokoll.   |
    And the sidecar "library/Weiss/Verteilte Systeme/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000003", "title": "Verteilte Systeme verstehen", "authors": ["Lukas Weiß"], "language": "de", "year": 2015, "tags": ["architektur/ddd", "architektur/verteilte-systeme"], "files": [{ "name": "vs.pdf" }] }
      """
    And I have run shelf with arguments "reindex --library library"
    And I have run shelf with arguments "searches add ddd tag:architektur/ddd --library library"
    And the web server is running on the library "library"

  Scenario: The saved searches are listed
    When I request "GET /api/v1/saved-searches"
    Then the response status is 200
    And the JSON array "searches" of the response has 1 entries
    And the JSON field "searches[0].name" of the response is "ddd"
    And the JSON field "searches[0].query" of the response is "tag:architektur/ddd"

  Scenario: A search is saved, and replaced when the name is used again
    When I send "PUT /api/v1/saved-searches/read" with the JSON:
      """
      { "query": "status:read" }
      """
    Then the response status is 200
    And the JSON field "name" of the response is "read"
    And the file "library/.shelf/searches.json" contains "status:read"
    When I send "PUT /api/v1/saved-searches/read" with the JSON:
      """
      { "query": "status:unread" }
      """
    Then the response status is 200
    And the file "library/.shelf/searches.json" contains "status:unread"
    And the file "library/.shelf/searches.json" does not contain "status:read"

  Scenario: A saved search is removed
    When I request "DELETE /api/v1/saved-searches/ddd"
    Then the response status is 204
    And the file "library/.shelf/searches.json" does not contain "ddd"

  Scenario Outline: What cannot be kept or removed is refused
    When I send "<request>" with the JSON:
      """
      <body>
      """
    Then the response status is <status>
    And the response body contains "<message>"

    Examples:
      | request                         | body                          | status | message            |
      | PUT /api/v1/saved-searches/a%20b | { "query": "status:read" }    | 400    | name               |
      | PUT /api/v1/saved-searches/x    | { "query": "" }               | 400    | query              |
      | PUT /api/v1/saved-searches/x    | { "query": "status:nothing" } | 400    | status             |
      | PUT /api/v1/saved-searches/x    | { "query": "@ddd" }           | 400    | another saved search |

  Scenario: An unknown search cannot be removed
    When I request "DELETE /api/v1/saved-searches/nothing"
    Then the response status is 404

  Scenario: The library list takes the name in its query
    When I request "GET /api/v1/works?q=@ddd"
    Then the response status is 200
    And the JSON field "total" of the response is 2

  Scenario: A name that is not saved is a 400
    When I request "GET /api/v1/works?q=@nothing"
    Then the response status is 400
    And the response body contains "no saved search"
