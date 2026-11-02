@F-SRC-11 @M19
Feature: Saved searches through MCP
  As an AI assistant
  I want to know the saved searches of a person and use them in a search
  So that "my unread DDD books" means what the person means

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
    And the MCP server is running on the library "library"

  Scenario: list_saved_searches gives the names and the queries
    When I call the tool "list_saved_searches" with the arguments:
      """
      {}
      """
    Then the tool call succeeds
    And the result has the array "searches" with 1 items
    And the result has the string "searches[0].name" equal to "ddd"
    And the result has the string "searches[0].query" equal to "tag:architektur/ddd"

  Scenario: search_books takes the name in its query
    When I call the tool "search_books" with the arguments:
      """
      { "query": "@ddd year:>=2020" }
      """
    Then the tool call succeeds
    And the result has the number "total" equal to 1
    And the result has the string "results[0].title" equal to "Event Sourcing in Practice"

  Scenario: A name that is not saved is an error of the tool
    When I call the tool "search_books" with the arguments:
      """
      { "query": "@nothing" }
      """
    Then the tool call fails with the message containing "no saved search"
