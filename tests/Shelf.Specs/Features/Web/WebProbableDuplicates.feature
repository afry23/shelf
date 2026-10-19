@F-DUP-3 @F-DUP-6 @PRD-8.2 @M5
Feature: Probable duplicates over HTTP
  As the duplicates view of the web interface
  I want the works that probably are one book, and a way to say they are not
  So that a reader can check them and the answer sticks

  Background:
    Given an empty working directory
    And I have run shelf with arguments "init library"
    And a sample EPUB "library/Evans, Eric/Domain-Driven Design (2003)/ddd.epub" with the title "Domain-Driven Design", the authors "Eric Evans" and the text seed 1
    And the sidecar "library/Evans, Eric/Domain-Driven Design (2003)/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K1000000000000000001", "title": "Domain-Driven Design", "authors": ["Eric Evans"], "year": 2003, "files": [{ "name": "ddd.epub" }] }
      """
    And a sample EPUB "library/Avram, Abel/DDD Quickly/ddd-quickly.epub" with the title "DDD Quickly", the authors "Abel Avram" and the text seed 1
    And the sidecar "library/Avram, Abel/DDD Quickly/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K2000000000000000002", "title": "DDD Quickly", "authors": ["Abel Avram"], "files": [{ "name": "ddd-quickly.epub" }] }
      """
    And a sample EPUB "library/Ibryam, Bilgin/Kubernetes Patterns (2019)/kp.epub" with the title "Kubernetes Patterns", the authors "Bilgin Ibryam" and the text seed 3
    And the sidecar "library/Ibryam, Bilgin/Kubernetes Patterns (2019)/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K3000000000000000003", "title": "Kubernetes Patterns", "authors": ["Bilgin Ibryam"], "year": 2019, "files": [{ "name": "kp.epub" }] }
      """
    And I have run shelf with arguments "reindex --library library"
    And the web server is running on the library "library"

  Scenario: The probable duplicates, with the distance and both works
    When I request "GET /api/v1/duplicates/probable"
    Then the response status is 200
    And the JSON field "total" of the response is 1
    And the JSON field "pairs[0].hamming" of the response is 0
    And the JSON field "pairs[0].a.title" of the response is "DDD Quickly"
    And the JSON field "pairs[0].b.title" of the response is "Domain-Driven Design"
    And the JSON field "pairs[0].a.id" of the response is "01J9Z3K2000000000000000002"

  Scenario: The plain list of duplicates does not include them
    When I request "GET /api/v1/duplicates"
    Then the JSON field "total" of the response is 0

  Scenario: Mark a pair as not duplicates; nothing moves and the pair is gone
    When I send "POST /api/v1/duplicates/probable/ignore" with the JSON:
      """
      { "a": "01J9Z3K1000000000000000001", "b": "01J9Z3K2000000000000000002" }
      """
    Then the response status is 200
    And the JSON field "trashed" of the response is 0
    And the file "library/Evans, Eric/Domain-Driven Design (2003)/ddd.epub" exists
    And the file "library/Avram, Abel/DDD Quickly/ddd-quickly.epub" exists
    When I request "GET /api/v1/duplicates/probable"
    Then the JSON field "total" of the response is 0

  Scenario: The mark is written to the sidecars, the source of truth
    Given I send "POST /api/v1/duplicates/probable/ignore" with the JSON:
      """
      { "b": "01J9Z3K1000000000000000001", "a": "01J9Z3K2000000000000000002" }
      """
    Then the file "library/Evans, Eric/Domain-Driven Design (2003)/book.json" contains "01J9Z3K2000000000000000002"
    And the file "library/Avram, Abel/DDD Quickly/book.json" contains "01J9Z3K1000000000000000001"

  Scenario: A pair that is not a probable duplicate is refused and nothing changes
    Given I take a snapshot of the folder "library"
    When I send "POST /api/v1/duplicates/probable/ignore" with the JSON:
      """
      { "a": "01J9Z3K1000000000000000001", "b": "01J9Z3K3000000000000000003" }
      """
    Then the response status is 409
    And the JSON field "detail" of the response contains "are not probable duplicates"
    And the folder "library" equals the snapshot

  Scenario Outline: A body without two IDs of works is a 400
    Given I take a snapshot of the folder "library"
    When I send "POST /api/v1/duplicates/probable/ignore" with the JSON:
      """
      <body>
      """
    Then the response status is 400
    And the response header "Content-Type" is "application/problem+json"
    And the folder "library" equals the snapshot

    Examples:
      | body                                          |
      | {}                                            |
      | { "a": "01J9Z3K1000000000000000001" }        |
      | { "a": 1, "b": 2 }                            |
      | { "a": "x", "b": "y" }                        |
      | { "a": "01J9Z3K1000000000000000001", "b": "01J9Z3K1000000000000000001" } |

  Scenario: A work that does not exist is a 404
    When I send "POST /api/v1/duplicates/probable/ignore" with the JSON:
      """
      { "a": "01J9Z3K1000000000000000001", "b": "01J9Z3K9000000000000000009" }
      """
    Then the response status is 404
