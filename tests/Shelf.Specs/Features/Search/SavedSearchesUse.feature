@F-SRC-9 @M19
Feature: Using a saved search in a query
  As a person with saved searches
  I want to write @name in a query
  So that the saved filters are part of it, wherever a query is accepted

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
    And I have run shelf with arguments "searches add read status:read --library library"

  Scenario: A saved search finds what its query finds
    When I run shelf with arguments "search @ddd --library library"
    Then the exit code is 0
    And the search results are, in any order: "Event Sourcing in Practice; Verteilte Systeme verstehen"

  Scenario: Words and filters are added to the saved search
    When I run shelf with arguments "search @ddd year:>=2020 --library library"
    Then the search results are, in any order: "Event Sourcing in Practice"

  Scenario: Two saved searches are both applied
    When I run shelf with arguments "search @ddd @read kubernetes --library library"
    Then the exit code is 0
    And standard output contains "No results"

  Scenario: Words and the saved search can come in any order
    When I run shelf with arguments "search kubernetes @read --library library"
    Then the search results are, in any order: "Kubernetes Patterns"

  Scenario: The name is not case sensitive
    When I run shelf with arguments "search @DDD --library library"
    Then the search results are, in any order: "Event Sourcing in Practice; Verteilte Systeme verstehen"

  Scenario: A saved search that changed gives the new result
    Given I have run shelf with arguments "searches add ddd status:read --replace --library library"
    When I run shelf with arguments "search @ddd --library library"
    Then the search results are, in any order: "Kubernetes Patterns"

  Scenario: An @ inside a phrase or in the middle of a word is not a name
    When I run shelf with arguments "search 'sidecar \"container @nothing\"' --library library"
    Then the exit code is 0
    When I run shelf with arguments "search user@example --library library"
    Then the exit code is 0

  Scenario: A name that is not saved is an error
    When I run shelf with arguments "search @nothing --library library"
    Then the exit code is 2
    And standard error contains "no saved search 'nothing'"

  Scenario Outline: Other commands take the name as well
    When I run shelf with arguments "<command> --library library"
    Then the exit code is 0
    And standard output contains "<text>"

    Examples:
      | command                                       | text                        |
      | export @read                                  | Ibryam                      |
      | notes export --all --query @ddd --format readwise-csv | Highlight,Title     |
