@PRD-7.2 @F-DUP-3 @M5
Feature: Probable duplicates through MCP
  As an AI assistant
  I want to ask which works probably are one book
  So that I can point the user to them without removing anything

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
    And the MCP server is running on the library "library"

  Scenario: find_probable_duplicates lists works with the same text
    When I call the tool "find_probable_duplicates" with the arguments:
      """
      {}
      """
    Then the tool call succeeds
    And the result has the number "total" equal to 1
    And the result has the array "pairs" with 1 items
    And the result has the number "pairs[0].hamming" equal to 0
    And the result has the string "pairs[0].a.title" equal to "DDD Quickly"
    And the result has the string "pairs[0].b.title" equal to "Domain-Driven Design"
    And the result has the string "pairs[0].a.id" equal to "01J9Z3K2000000000000000002"
    And the result has the property "pairs[0].cosine" set to null

  Scenario: The tool only reads: nothing is removed
    When I call the tool "find_probable_duplicates" with the arguments:
      """
      {}
      """
    Then the tool call succeeds
    And the file "library/Evans, Eric/Domain-Driven Design (2003)/ddd.epub" exists
    And the file "library/Avram, Abel/DDD Quickly/ddd-quickly.epub" exists

  Scenario: The output schema is met
    When I call the tool "find_probable_duplicates" with the arguments:
      """
      {}
      """
    Then the result has every property that the output schema of "find_probable_duplicates" requires
