@F-GRP-4 @F-DUP-3 @M5
Feature: Merging works over HTTP
  As the duplicate view and the work view of the web interface
  I want to merge one work into another
  So that I do not need the command line to join a book that was split into two works

  Background:
    Given an empty working directory
    And I have run shelf with arguments "init library"
    And a sample EPUB "library/Evans, Eric/Domain-Driven Design (2003)/ddd.epub" with the title "Domain-Driven Design", the authors "Eric Evans" and the text seed 1
    And the sidecar "library/Evans, Eric/Domain-Driven Design (2003)/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K1000000000000000001", "title": "Domain-Driven Design", "authors": ["Eric Evans"], "year": 2003, "tags": ["architecture"], "files": [{ "name": "ddd.epub" }] }
      """
    And a sample EPUB "library/Avram, Abel/DDD Quickly/ddd.epub" with the title "DDD Quickly", the authors "Abel Avram" and the text seed 1
    And a sample PDF "library/Avram, Abel/DDD Quickly/ddd-quickly.pdf" with the title "DDD Quickly", the author "Abel Avram" and the text seed 1
    And the sidecar "library/Avram, Abel/DDD Quickly/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K2000000000000000002", "title": "DDD Quickly", "authors": ["Abel Avram"], "tags": ["ddd"], "publisher": "InfoQ", "files": [{ "name": "ddd.epub" }, { "name": "ddd-quickly.pdf" }] }
      """
    And a sample EPUB "library/Ibryam, Bilgin/Kubernetes Patterns (2019)/kp.epub" with the title "Kubernetes Patterns", the authors "Bilgin Ibryam" and the text seed 3
    And the sidecar "library/Ibryam, Bilgin/Kubernetes Patterns (2019)/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K3000000000000000003", "title": "Kubernetes Patterns", "authors": ["Bilgin Ibryam"], "year": 2019, "files": [{ "name": "kp.epub" }] }
      """
    And I have run shelf with arguments "reindex --library library"
    And I take a snapshot of the folder "library"
    And the web server is running on the library "library"

  Scenario: The files of the second work move into the first one
    When I send "POST /api/v1/works/01J9Z3K1000000000000000001/merge" with the JSON:
      """
      { "from": "01J9Z3K2000000000000000002" }
      """
    Then the response status is 200
    And the JSON field "id" of the response is "01J9Z3K1000000000000000001"
    And the JSON field "merged" of the response is "01J9Z3K2000000000000000002"
    And the JSON field "movedFiles" of the response is 2
    And the file "library/Evans, Eric/Domain-Driven Design (2003)/ddd.epub" exists
    And the file "library/Evans, Eric/Domain-Driven Design (2003)/ddd (1).epub" exists
    And the file "library/Evans, Eric/Domain-Driven Design (2003)/ddd-quickly.pdf" exists
    And the file "library/Avram, Abel/DDD Quickly/book.json" does not exist

  Scenario: The surviving work keeps its metadata, takes the tags and fills its gaps
    When I send "POST /api/v1/works/01J9Z3K1000000000000000001/merge" with the JSON:
      """
      { "from": "01J9Z3K2000000000000000002" }
      """
    Then the file "library/Evans, Eric/Domain-Driven Design (2003)/book.json" contains "Domain-Driven Design"
    And the file "library/Evans, Eric/Domain-Driven Design (2003)/book.json" does not contain "DDD Quickly"
    And the file "library/Evans, Eric/Domain-Driven Design (2003)/book.json" contains "architecture"
    And the file "library/Evans, Eric/Domain-Driven Design (2003)/book.json" contains "ddd"
    And the file "library/Evans, Eric/Domain-Driven Design (2003)/book.json" contains "InfoQ"

  Scenario: The direction decides which work survives
    When I send "POST /api/v1/works/01J9Z3K2000000000000000002/merge" with the JSON:
      """
      { "from": "01J9Z3K1000000000000000001" }
      """
    Then the response status is 200
    And the JSON field "id" of the response is "01J9Z3K2000000000000000002"
    And the file "library/Avram, Abel/DDD Quickly/book.json" contains "DDD Quickly"
    And the file "library/Evans, Eric/Domain-Driven Design (2003)/book.json" does not exist

  Scenario: The index follows the merge
    When I send "POST /api/v1/works/01J9Z3K1000000000000000001/merge" with the JSON:
      """
      { "from": "01J9Z3K2000000000000000002" }
      """
    And I request "GET /api/v1/works"
    Then the JSON field "total" of the response is 2
    When I request "GET /api/v1/works/01J9Z3K2000000000000000002"
    Then the response status is 404

  # ---- refusals ----

  Scenario: A work cannot be merged into itself
    When I send "POST /api/v1/works/01J9Z3K1000000000000000001/merge" with the JSON:
      """
      { "from": "01J9Z3K1000000000000000001" }
      """
    Then the response status is 409
    And the JSON field "detail" of the response contains "into itself"
    And the folder "library" equals the snapshot

  Scenario Outline: A work that does not exist is a 404
    When I send "POST /api/v1/works/<target>/merge" with the JSON:
      """
      { "from": "<from>" }
      """
    Then the response status is 404
    And the folder "library" equals the snapshot

    Examples:
      | target                     | from                       |
      | 01J9Z3K9000000000000000009 | 01J9Z3K1000000000000000001 |
      | 01J9Z3K1000000000000000001 | 01J9Z3K9000000000000000009 |

  Scenario Outline: A request that is not well formed is a bad request
    When I send "POST /api/v1/works/<target>/merge" with the JSON:
      """
      <body>
      """
    Then the response status is 400
    And the folder "library" equals the snapshot

    Examples:
      | target                     | body                                  |
      | not-an-id                  | { "from": "01J9Z3K2000000000000000002" } |
      | 01J9Z3K1000000000000000001 | { "from": "not-an-id" }                 |
      | 01J9Z3K1000000000000000001 | { "from": 7 }                           |
      | 01J9Z3K1000000000000000001 | {}                                      |
