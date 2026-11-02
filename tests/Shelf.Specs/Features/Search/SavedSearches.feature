@F-SRC-8 @M19
Feature: Saved searches
  As a person who runs the same searches again and again
  I want to give a search a name and keep it
  So that I do not have to type its filters each time

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

  Scenario: A search is saved under a name and listed
    When I run shelf with arguments "searches add ddd 'tag:architektur/ddd year:>=2016' --library library"
    Then the exit code is 0
    And standard output contains "ddd"
    When I run shelf with arguments "searches list --library library"
    Then the exit code is 0
    And standard output contains "ddd"
    And standard output contains "tag:architektur/ddd year:>=2016"

  Scenario: The saved searches are kept in the library, so that a reindex does not lose them
    Given I have run shelf with arguments "searches add ddd tag:architektur/ddd --library library"
    Then the file "library/.shelf/searches.json" contains "tag:architektur/ddd"
    When I run shelf with arguments "reindex --library library"
    And I run shelf with arguments "searches list --library library"
    Then standard output contains "ddd"

  Scenario: The list is available as JSON
    Given I have run shelf with arguments "searches add ddd tag:architektur/ddd --library library"
    When I run shelf with arguments "searches list --json --library library"
    Then the exit code is 0
    And the JSON property "searches" of standard output has 1 items
    And standard output contains '"name":"ddd"'
    And standard output contains '"query":"tag:architektur/ddd"'

  Scenario: A library without saved searches says so
    When I run shelf with arguments "searches list --library library"
    Then the exit code is 0
    And standard output contains "No saved searches"

  Scenario: A saved search is removed
    Given I have run shelf with arguments "searches add ddd tag:architektur/ddd --library library"
    When I run shelf with arguments "searches rm ddd --library library"
    Then the exit code is 0
    When I run shelf with arguments "searches list --library library"
    Then standard output contains "No saved searches"

  Scenario: A saved search with the same name is replaced only on request
    Given I have run shelf with arguments "searches add ddd tag:architektur/ddd --library library"
    When I run shelf with arguments "searches add ddd status:read --library library"
    Then the exit code is 1
    And standard error contains "already"
    When I run shelf with arguments "searches add ddd status:read --replace --library library"
    Then the exit code is 0
    And the file "library/.shelf/searches.json" contains "status:read"
    And the file "library/.shelf/searches.json" does not contain "architektur/ddd"

  Scenario: Names are not case sensitive
    Given I have run shelf with arguments "searches add DDD tag:architektur/ddd --library library"
    When I run shelf with arguments "searches add ddd status:read --library library"
    Then the exit code is 1
    And standard error contains "already"

  Scenario Outline: A name or a query that cannot be kept is refused and nothing is written
    When I run shelf with arguments "searches add <name> <query> --library library"
    Then the exit code is 1
    And standard error contains "<message>"
    And the file "library/.shelf/searches.json" does not exist

    Examples:
      | name       | query          | message             |
      | 'a b'      | status:read    | name                |
      | 'a@b'      | status:read    | name                |
      | ddd        | status:nothing | status              |
      | ddd        | @other         | another saved search |

  Scenario: Removing a search that does not exist is an error
    When I run shelf with arguments "searches rm nothing --library library"
    Then the exit code is 1
    And standard error contains "no saved search"
