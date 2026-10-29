@F-RDR-6 @F-ANN-3 @M10
Feature: Bookmarks of a file
  As a person who reads a book over several days
  I want to set a bookmark at a place in a file
  So that I can come back to places that matter without highlighting text

  Background:
    Given an empty working directory
    And I have run shelf with arguments "init library"
    And a sample EPUB "library/Ibryam/Kubernetes Patterns/kp.epub" with the title "Kubernetes Patterns", the authors "Bilgin Ibryam" and the chapters:
      | title                 | text                                              |
      | Foundational Patterns | The sidecar container extends the main container. |
      | Behavioral Patterns   | A batch job runs to completion.                   |
    And the sidecar "library/Ibryam/Kubernetes Patterns/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000001", "title": "Kubernetes Patterns", "authors": ["Bilgin Ibryam"], "language": "en", "year": 2019, "citeKey": "ibryam2019kubernetes", "files": [{ "name": "kp.epub" }] }
      """
    And a sample PDF "library/Alander/Event Sourcing in Practice/es.pdf" with the title "Event Sourcing in Practice", the author "Jördis Ålander" and the pages:
      | text                                            |
      | Event sourcing stores every change as an event. |
      | A projection builds a read model.               |
    And the sidecar "library/Alander/Event Sourcing in Practice/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000002", "title": "Event Sourcing in Practice", "authors": ["Jördis Ålander"], "language": "en", "year": 2021, "citeKey": "alander2021event", "files": [{ "name": "es.pdf" }] }
      """
    And I have run shelf with arguments "reindex --library library"

  # ---- over HTTP ----

  Scenario Outline: A bookmark is stored in annotations.json next to book.json
    Given the web server is running on the library "library"
    When I send "POST /api/v1/files/<file>/annotations" with the JSON:
      """
      { "kind": "bookmark", "locator": "<locator>", "note": "<label>" }
      """
    Then the response status is 201
    And the JSON field "kind" of the response is "bookmark"
    And the JSON field "locator" of the response is "<locator>"
    And the JSON field "note" of the response is "<label>"
    And the JSON field "quote" of the response is null
    And the JSON field "color" of the response is null
    And the file "library/<folder>/annotations.json" contains "<locator>"
    When I request "GET /api/v1/files/<file>/annotations"
    Then the JSON array "" of the response has 1 entries
    And the JSON field "[0].kind" of the response is "bookmark"

    Examples:
      | file                               | folder                             | locator                     | label          |
      | 01J9Z3K0000000000000000001~kp.epub | Ibryam/Kubernetes Patterns         | epubcfi(/6/4!/4/2/1:0)      | Sidecar        |
      | 01J9Z3K0000000000000000002~es.pdf  | Alander/Event Sourcing in Practice | page=2                      | Read model     |

  Scenario: A bookmark does not need a label
    Given the web server is running on the library "library"
    When I send "POST /api/v1/files/01J9Z3K0000000000000000002~es.pdf/annotations" with the JSON:
      """
      { "kind": "bookmark", "locator": "page=1" }
      """
    Then the response status is 201
    And the JSON field "note" of the response is null

  Scenario: A second bookmark at the same place is the first one
    Given the web server is running on the library "library"
    And I send "POST /api/v1/files/01J9Z3K0000000000000000002~es.pdf/annotations" with the JSON:
      """
      { "kind": "bookmark", "locator": "page=2" }
      """
    And I note the JSON field "id" of the response as the entry
    When I send "POST /api/v1/files/01J9Z3K0000000000000000002~es.pdf/annotations" with the JSON:
      """
      { "kind": "bookmark", "locator": "page=2" }
      """
    Then the response status is 200
    When I request "GET /api/v1/files/01J9Z3K0000000000000000002~es.pdf/annotations"
    Then the JSON array "" of the response has 1 entries

  Scenario: The label of a bookmark can be changed and removed
    Given the web server is running on the library "library"
    And I send "POST /api/v1/files/01J9Z3K0000000000000000002~es.pdf/annotations" with the JSON:
      """
      { "kind": "bookmark", "locator": "page=2", "note": "Read model" }
      """
    And I note the JSON field "id" of the response as the entry
    When I send "PATCH /api/v1/files/01J9Z3K0000000000000000002~es.pdf/annotations/{entry}" with the JSON:
      """
      { "note": "Projections" }
      """
    Then the response status is 200
    And the JSON field "note" of the response is "Projections"
    When I send "PATCH /api/v1/files/01J9Z3K0000000000000000002~es.pdf/annotations/{entry}" with the JSON:
      """
      { "note": null }
      """
    Then the response status is 200
    And the JSON field "note" of the response is null
    And the JSON field "kind" of the response is "bookmark"

  Scenario: A bookmark can be deleted
    Given the web server is running on the library "library"
    And I send "POST /api/v1/files/01J9Z3K0000000000000000002~es.pdf/annotations" with the JSON:
      """
      { "kind": "bookmark", "locator": "page=2" }
      """
    And I note the JSON field "id" of the response as the entry
    When I send "DELETE /api/v1/files/01J9Z3K0000000000000000002~es.pdf/annotations/{entry}" with the JSON:
      """
      {}
      """
    Then the response status is 204
    When I request "GET /api/v1/files/01J9Z3K0000000000000000002~es.pdf/annotations"
    Then the JSON array "" of the response has 0 entries

  Scenario Outline: A bookmark that cannot be set is refused and nothing changes
    Given the web server is running on the library "library"
    And I take a snapshot of the folder "library"
    When I send "POST /api/v1/files/01J9Z3K0000000000000000002~es.pdf/annotations" with the JSON:
      """
      <body>
      """
    Then the response status is 400
    And the folder "library" equals the snapshot

    Examples:
      | body                                                                  |
      | { "kind": "bookmark" }                                                |
      | { "kind": "bookmark", "locator": "chapter 2" }                        |
      | { "kind": "bookmark", "locator": "page=2", "color": "pink" }          |
      | { "kind": "bookmark", "locator": "page=2", "quote": { "exact": "x" } } |

  Scenario: The label of a bookmark is searchable
    Given the web server is running on the library "library"
    And I send "POST /api/v1/files/01J9Z3K0000000000000000002~es.pdf/annotations" with the JSON:
      """
      { "kind": "bookmark", "locator": "page=2", "note": "zebrastripe" }
      """
    When I run shelf with arguments "search zebrastripe --library library"
    Then the search results are, in any order: "Event Sourcing in Practice"

  Scenario: Reindex brings the bookmarks back
    Given the web server is running on the library "library"
    And I send "POST /api/v1/files/01J9Z3K0000000000000000002~es.pdf/annotations" with the JSON:
      """
      { "kind": "bookmark", "locator": "page=2", "note": "Read model" }
      """
    When I run shelf with arguments "reindex --library library"
    And I request "GET /api/v1/files/01J9Z3K0000000000000000002~es.pdf/annotations"
    Then the JSON array "" of the response has 1 entries
    And the JSON field "[0].note" of the response is "Read model"

  # ---- on the command line ----

  Scenario: A bookmark is set on the command line and listed with its place
    When I run shelf with arguments "notes bookmark 01J9Z3K0000000000000000002 page=2 'Read model' --library library"
    Then the exit code is 0
    And the file "library/Alander/Event Sourcing in Practice/annotations.json" contains "bookmark"
    When I run shelf with arguments "notes list 01J9Z3K0000000000000000002 --library library"
    Then standard output contains "bookmark"
    And standard output contains "page 2"
    And standard output contains "Read model"

  Scenario: A work with several files needs the file for a new bookmark
    Given a sample EPUB "library/Alander/Event Sourcing in Practice/es.epub" with the title "Event Sourcing in Practice", the authors "Jördis Ålander" and the chapters:
      | title | text              |
      | One   | Events are facts. |
    And the sidecar "library/Alander/Event Sourcing in Practice/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000002", "title": "Event Sourcing in Practice", "authors": ["Jördis Ålander"], "language": "en", "year": 2021, "files": [{ "name": "es.pdf" }, { "name": "es.epub" }] }
      """
    And I have run shelf with arguments "reindex --library library"
    When I run shelf with arguments "notes bookmark 01J9Z3K0000000000000000002 page=1 --library library"
    Then the exit code is 2
    And standard error contains "--file"
    When I run shelf with arguments "notes bookmark 01J9Z3K0000000000000000002 page=1 --file es.pdf --library library"
    Then the exit code is 0

  Scenario: A bookmark with a place that does not fit the format is refused
    When I run shelf with arguments "notes bookmark 01J9Z3K0000000000000000002 epubcfi(/6/4) --library library"
    Then the exit code is 2
    And the file "library/Alander/Event Sourcing in Practice/annotations.json" does not exist

  # ---- export ----

  Scenario: The export of a work lists its bookmarks
    When I run shelf with arguments "notes bookmark 01J9Z3K0000000000000000002 page=2 'Read model' --library library"
    And I run shelf with arguments "notes export 01J9Z3K0000000000000000002 --library library"
    Then the exit code is 0
    And standard output contains "## Bookmarks"
    And standard output contains "- page 2: Read model"
