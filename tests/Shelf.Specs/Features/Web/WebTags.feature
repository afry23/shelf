@PRD-7.3 @PRD-8.2 @F-TAG-4 @F-TAG-5 @M5
Feature: Renaming and merging tags over HTTP
  As the tags view of the web interface
  I want to rename a tag and to merge two tags library-wide
  So that the tag tree stays tidy without the command line

  Background:
    Given an empty working directory
    And I have run shelf with arguments "init library"
    And a sample EPUB "library/Ibryam/Kubernetes Patterns/kp.epub" with the title "Kubernetes Patterns", the authors "Bilgin Ibryam" and the chapters:
      | title                 | text                                              |
      | Foundational Patterns | The sidecar container extends the main container. |
    And the sidecar "library/Ibryam/Kubernetes Patterns/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000001", "title": "Kubernetes Patterns", "subtitle": "Reusable Elements", "authors": ["Bilgin Ibryam"], "language": "en", "year": 2019, "description": "Design patterns for containers", "tags": ["cloud/kubernetes", "devops"], "files": [{ "name": "kp.epub" }] }
      """
    And a sample PDF "library/Alander/Event Sourcing in Practice/es.pdf" with the title "Event Sourcing in Practice", the author "Jördis Ålander" and the pages:
      | text                                            |
      | Event sourcing stores every change as an event. |
    And the sidecar "library/Alander/Event Sourcing in Practice/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000002", "title": "Event Sourcing in Practice", "authors": ["Jördis Ålander"], "language": "en", "year": 2021, "tags": ["architektur/ddd", "cloud/azure"], "files": [{ "name": "es.pdf" }] }
      """
    And I have run shelf with arguments "reindex --library library"
    And the web server is running on the library "library"

  Scenario: A tag is renamed on every work and the tree shows it
    When I send "POST /api/v1/tags/rename" with the JSON:
      """
      { "from": "devops", "to": "operations" }
      """
    Then the response status is 200
    And the JSON field "from" of the response is "devops"
    And the JSON field "to" of the response is "operations"
    And the JSON field "changed" of the response is 1
    When I request "GET /api/v1/works/01J9Z3K0000000000000000001"
    Then the JSON array "tags" of the response includes "operations"
    And the file "library/Ibryam/Kubernetes Patterns/book.json" contains "operations"
    And the file "library/Ibryam/Kubernetes Patterns/book.json" does not contain "devops"

  Scenario: The tags below a tag are renamed with it
    When I send "POST /api/v1/tags/rename" with the JSON:
      """
      { "from": "cloud", "to": "infrastruktur" }
      """
    Then the response status is 200
    And the JSON field "changed" of the response is 2
    When I request "GET /api/v1/works/01J9Z3K0000000000000000002"
    Then the JSON array "tags" of the response includes "infrastruktur/azure"

  Scenario: A rename can be undone with shelf undo
    Given I send "POST /api/v1/tags/rename" with the JSON:
      """
      { "from": "devops", "to": "operations" }
      """
    When I run shelf with arguments "undo --library library"
    And I request "GET /api/v1/works/01J9Z3K0000000000000000001"
    Then the JSON array "tags" of the response includes "devops"
    And the JSON array "tags" of the response does not include "operations"

  Scenario: A tag is merged into another one
    When I send "POST /api/v1/tags/merge" with the JSON:
      """
      { "from": "devops", "into": "cloud/azure" }
      """
    Then the response status is 200
    And the JSON field "changed" of the response is 1
    When I request "GET /api/v1/works/01J9Z3K0000000000000000001"
    Then the JSON array "tags" of the response includes "cloud/azure"
    And the JSON array "tags" of the response does not include "devops"

  Scenario: A work that has both tags keeps one
    Given I send "POST /api/v1/tags/merge" with the JSON:
      """
      { "from": "cloud/kubernetes", "into": "devops" }
      """
    When I request "GET /api/v1/works/01J9Z3K0000000000000000001"
    Then the JSON array "tags" of the response does not include "cloud/kubernetes"
    And the file "library/Ibryam/Kubernetes Patterns/book.json" does not contain "cloud/kubernetes"

  Scenario Outline: A request that cannot be done is refused and changes nothing
    Given I take a snapshot of the folder "library"
    When I send "POST /api/v1/tags/<action>" with the JSON:
      """
      <body>
      """
    Then the response status is <status>
    And the JSON field "detail" of the response contains "<reason>"
    And the folder "library" equals the snapshot

    Examples:
      | action | body                                            | status | reason                |
      | rename | { "from": "devops", "to": "cloud/azure" }       | 409    | already exists        |
      | rename | { "from": "nothing", "to": "other" }            | 404    | no work has the tag   |
      | rename | { "from": "devops", "to": "devops" }            | 400    | same tag              |
      | rename | { "from": "devops" }                            | 400    | to is required        |
      | rename | { "from": 3, "to": "x" }                        | 400    | from is required      |
      | merge  | { "from": "nothing", "into": "devops" }         | 404    | no work has the tag   |
      | merge  | { "from": "cloud", "into": "cloud/azure" }      | 400    | above or below        |
      | merge  | { "from": "devops" }                            | 400    | into is required      |
