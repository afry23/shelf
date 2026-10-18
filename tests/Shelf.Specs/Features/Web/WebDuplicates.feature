@F-DUP-4 @F-DUP-5 @F-DUP-6 @M4
Feature: Duplicates over HTTP
  As the duplicate view of the web interface
  I want to see the candidates and decide which file stays
  So that the others go to the trash and nothing is lost for good

  Background:
    Given an empty working directory
    And I have run shelf with arguments "init library"
    And a sample EPUB "library/Evans, Eric/Domain-Driven Design (2003)/ddd.epub" with the title "Domain-Driven Design", the authors "Eric Evans" and the text seed 1
    And a sample EPUB "library/Evans, Eric/Domain-Driven Design (2003)/ddd (1).epub" with the title "Domain-Driven Design", the authors "Eric Evans" and the text seed 1
    And the sidecar "library/Evans, Eric/Domain-Driven Design (2003)/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K1000000000000000001", "title": "Domain-Driven Design", "authors": ["Eric Evans"], "year": 2003, "files": [{ "name": "ddd.epub" }, { "name": "ddd (1).epub" }] }
      """
    And a sample EPUB "library/Ibryam, Bilgin/Kubernetes Patterns (2019)/kp.epub" with the title "Kubernetes Patterns", the authors "Bilgin Ibryam" and the text seed 3
    And the sidecar "library/Ibryam, Bilgin/Kubernetes Patterns (2019)/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K2000000000000000002", "title": "Kubernetes Patterns", "authors": ["Bilgin Ibryam"], "year": 2019, "tags": ["devops"], "files": [{ "name": "kp.epub" }] }
      """
    And a sample EPUB "library/Imports/kp/kp-copy.epub" with the title "Kubernetes Patterns", the authors "Bilgin Ibryam" and the text seed 3
    And the sidecar "library/Imports/kp/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K3000000000000000003", "title": "Kubernetes Patterns (copy)", "authors": ["Bilgin Ibryam"], "files": [{ "name": "kp-copy.epub" }] }
      """
    And a sample EPUB "library/Davis, Cornelia/Cloud Native Patterns (2019)/a.epub" with the title "Cloud Native Patterns", the authors "Cornelia Davis" and the text seed 2
    And a sample EPUB "library/Davis, Cornelia/Cloud Native Patterns (2019)/b.epub" with the title "Cloud Native Patterns", the authors "Cornelia Davis", the ISBN "9781617294297" and the text seed 5
    And the sidecar "library/Davis, Cornelia/Cloud Native Patterns (2019)/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K4000000000000000004", "title": "Cloud Native Patterns", "authors": ["Cornelia Davis"], "year": 2019, "files": [{ "name": "a.epub" }, { "name": "b.epub" }] }
      """
    And I have run shelf with arguments "reindex --library library"
    And I take a snapshot of the folder "library"
    And the web server is running on the library "library"

  # ---- the list ----

  Scenario: The groups of duplicates, in the order of the CLI
    When I request "GET /api/v1/duplicates"
    Then the response status is 200
    And the JSON field "total" of the response is 3
    And the JSON array "groups" of the response has 3 entries
    And the JSON field "groups[0].kind" of the response is "same-format"
    And the JSON field "groups[1].kind" of the response is "identical"
    And the JSON field "groups[2].kind" of the response is "identical"
    And the JSON field "groups[0].format" of the response is "epub"

  Scenario: A file of a group has what the comparison needs
    When I request "GET /api/v1/duplicates"
    Then the JSON field "groups[1].files[0].id" of the response is "01J9Z3K1000000000000000001~ddd (1).epub"
    And the JSON field "groups[1].files[0].workId" of the response is "01J9Z3K1000000000000000001"
    And the JSON field "groups[1].files[0].title" of the response is "Domain-Driven Design"
    And the JSON field "groups[1].files[0].name" of the response is "ddd (1).epub"
    And the JSON field "groups[1].files[0].path" of the response is "Evans, Eric/Domain-Driven Design (2003)/ddd (1).epub"
    And the JSON field "groups[1].files[0].sizeBytes" of the response is not zero
    And the JSON field "groups[1].files[0].pages" of the response is null
    And the JSON field "groups[1].files[0].completeness" of the response is null

  Scenario: Files with the same content in two works are one group
    When I request "GET /api/v1/duplicates"
    Then the JSON array "groups[2].files" of the response has 2 entries
    And the JSON field "groups[2].files[0].workId" of the response is "01J9Z3K2000000000000000002"
    And the JSON field "groups[2].files[1].workId" of the response is "01J9Z3K3000000000000000003"

  Scenario: Different files of one work in one format are rated by their metadata
    When I request "GET /api/v1/duplicates"
    Then the JSON field "groups[0].files[0].completeness" of the response is 3
    And the JSON field "groups[0].files[1].completeness" of the response is 4
    And the JSON field "groups[0].keep" of the response is 1

  Scenario: The suggestion for identical files is the file with the plain name
    When I request "GET /api/v1/duplicates"
    Then the JSON field "groups[1].keep" of the response is 1
    And the JSON field "groups[1].files[1].name" of the response is "ddd.epub"

  # ---- the actions ----

  Scenario: Keep a file; the others of its group go to the trash
    When I send "POST /api/v1/duplicates/keep" with the JSON:
      """
      { "fileId": "01J9Z3K1000000000000000001~ddd.epub" }
      """
    Then the response status is 200
    And the JSON field "trashed" of the response is 1
    And the JSON array "worksRemoved" of the response has 0 entries
    And the file "library/Evans, Eric/Domain-Driven Design (2003)/ddd.epub" exists
    And the file "library/Evans, Eric/Domain-Driven Design (2003)/ddd (1).epub" does not exist
    And the directory "library/.shelf/trash" exists
    When I request "GET /api/v1/duplicates"
    Then the JSON field "total" of the response is 2

  Scenario: Trash one file
    When I send "POST /api/v1/duplicates/trash" with the JSON:
      """
      { "fileId": "01J9Z3K1000000000000000001~ddd (1).epub" }
      """
    Then the response status is 200
    And the JSON field "trashed" of the response is 1
    And the file "library/Evans, Eric/Domain-Driven Design (2003)/ddd (1).epub" does not exist
    And the file "library/Evans, Eric/Domain-Driven Design (2003)/ddd.epub" exists

  Scenario: The sidecar and the index follow
    Given I send "POST /api/v1/duplicates/trash" with the JSON:
      """
      { "fileId": "01J9Z3K1000000000000000001~ddd (1).epub" }
      """
    When I request "GET /api/v1/works/01J9Z3K1000000000000000001"
    Then the JSON array "files" of the response has 1 entries
    And the JSON field "files[0].name" of the response is "ddd.epub"

  Scenario: A work that is left without files goes to the trash with its folder
    When I send "POST /api/v1/duplicates/keep" with the JSON:
      """
      { "fileId": "01J9Z3K2000000000000000002~kp.epub" }
      """
    Then the response status is 200
    And the JSON field "trashed" of the response is 1
    And the JSON array "worksRemoved" of the response has 1 entries
    And the JSON field "worksRemoved[0]" of the response is "01J9Z3K3000000000000000003"
    And the directory "library/Imports/kp" does not exist
    When I request "GET /api/v1/works/01J9Z3K3000000000000000003"
    Then the response status is 404

  Scenario: Mark a group as not duplicates; no file moves
    When I send "POST /api/v1/duplicates/ignore" with the JSON:
      """
      { "fileId": "01J9Z3K1000000000000000001~ddd.epub" }
      """
    Then the response status is 200
    And the JSON field "trashed" of the response is 0
    And the file "library/Evans, Eric/Domain-Driven Design (2003)/ddd.epub" exists
    And the file "library/Evans, Eric/Domain-Driven Design (2003)/ddd (1).epub" exists
    And the directory "library/.shelf/trash" does not exist
    When I request "GET /api/v1/duplicates"
    Then the JSON field "total" of the response is 2

  Scenario: The mark is written to the sidecar, the source of truth
    Given I send "POST /api/v1/duplicates/ignore" with the JSON:
      """
      { "fileId": "01J9Z3K2000000000000000002~kp.epub" }
      """
    Then the file "library/Ibryam, Bilgin/Kubernetes Patterns (2019)/book.json" contains "01J9Z3K3000000000000000003"
    And the file "library/Imports/kp/book.json" contains "01J9Z3K2000000000000000002"

  Scenario: Everything done over HTTP can be taken back with undo
    Given I send "POST /api/v1/duplicates/keep" with the JSON:
      """
      { "fileId": "01J9Z3K1000000000000000001~ddd.epub" }
      """
    When I run shelf with arguments "undo --library library"
    Then the exit code is 0
    And the folder "library" equals the snapshot

  Scenario: A work removed over HTTP comes back with undo
    Given I send "POST /api/v1/duplicates/keep" with the JSON:
      """
      { "fileId": "01J9Z3K2000000000000000002~kp.epub" }
      """
    When I run shelf with arguments "undo --library library"
    Then the folder "library" equals the snapshot

  Scenario: Nothing is deleted for good
    Given I send "POST /api/v1/duplicates/keep" with the JSON:
      """
      { "fileId": "01J9Z3K1000000000000000001~ddd.epub" }
      """
    Then the directory "library/.shelf/trash" exists

  # ---- refusals ----

  Scenario Outline: An action on a file that is not a duplicate is refused and nothing moves
    Given I send "POST /api/v1/duplicates/ignore" with the JSON:
      """
      { "fileId": "01J9Z3K1000000000000000001~ddd.epub" }
      """
    And I take a snapshot of the folder "library"
    When I send "POST /api/v1/duplicates/<action>" with the JSON:
      """
      { "fileId": "01J9Z3K1000000000000000001~ddd.epub" }
      """
    Then the response status is 409
    And the JSON field "detail" of the response contains "is not part of a group of duplicates"
    And the folder "library" equals the snapshot

    Examples:
      | action |
      | keep   |
      | trash  |
      | ignore |

  Scenario Outline: A file that does not exist is a 404
    When I send "POST /api/v1/duplicates/<action>" with the JSON:
      """
      { "fileId": "<id>" }
      """
    Then the response status is 404
    And the folder "library" equals the snapshot

    Examples:
      | action | id                                       |
      | keep   | 01J9Z3K1000000000000000001~nothing.epub  |
      | trash  | 01J9Z3K9000000000000000009~ddd.epub      |
      | ignore | 01J9Z3K1000000000000000001~..%2Fddd.epub |

  Scenario Outline: A body without the ID of a file is a 400
    When I send "POST /api/v1/duplicates/trash" with the JSON:
      """
      <body>
      """
    Then the response status is 400
    And the response header "Content-Type" is "application/problem+json"
    And the folder "library" equals the snapshot

    Examples:
      | body                |
      | { }                 |
      | { "fileId": 3 }     |
      | { "fileId": "" }    |
      | { "fileId": "ddd" } |
      | [ "x" ]             |
      | not json            |

  Scenario: The actions need the access token like every call of the API
    Given the web server is running on the library "library" with the arguments "--token s3cret"
    When I send "POST /api/v1/duplicates/trash" with the JSON:
      """
      { "fileId": "01J9Z3K1000000000000000001~ddd (1).epub" }
      """
    Then the response status is 401
    And the folder "library" equals the snapshot
