@PRD-7.3 @PRD-8.2 @F-ORG-5 @F-ORG-6 @F-ORG-7 @M5
Feature: Organizing the library over HTTP
  As the organize view of the web interface
  I want to see what the pattern would move, run it, and take it back
  So that I tidy the folders from the browser and can always undo it

  Background:
    Given an empty working directory
    And I have run shelf with arguments "init library"
    And a sample EPUB "library/inbox/pea.epub" with the title "Patterns of Enterprise Application Architecture", the authors "Martin Fowler" and the text seed 1
    And a sample PDF "library/inbox/pea.pdf" with the title "Patterns of Enterprise Application Architecture", the author "Martin Fowler" and the text seed 1
    And the sidecar "library/inbox/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K1000000000000000001", "title": "Patterns of Enterprise Application Architecture", "authors": ["Martin Fowler"], "year": 2002, "tags": ["architecture"], "files": [{ "name": "pea.epub" }, { "name": "pea.pdf" }] }
      """
    And a sample EPUB "library/misc/ddia.epub" with the title "Designing Data-Intensive Applications", the authors "Martin Kleppmann" and the text seed 2
    And the sidecar "library/misc/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K2000000000000000002", "title": "Designing Data-Intensive Applications", "authors": ["Martin Kleppmann"], "year": 2017, "files": [{ "name": "ddia.epub" }] }
      """
    And I have run shelf with arguments "reindex --library library"
    And I take a snapshot of the folder "library"
    And the web server is running on the library "library"

  Scenario: The preview shows every move and changes nothing
    When I request "GET /api/v1/organize"
    Then the response status is 200
    And the JSON field "pattern" of the response is "{author_sort}/{title} ({year})/{title}.{ext}"
    And the JSON field "files" of the response is 3
    And the JSON field "works" of the response is 2
    And the JSON field "unchanged" of the response is 0
    And the JSON field "truncated" of the response is false
    And the JSON field "moves[0].from" of the response is "inbox/pea.epub"
    And the JSON field "moves[0].to" of the response is "Fowler, Martin/Patterns of Enterprise Application Architecture (2002)/Patterns of Enterprise Application Architecture.epub"
    And the JSON field "moves[0].id" of the response is "01J9Z3K1000000000000000001"
    And the JSON array "collisions" of the response has 0 entries
    And the JSON field "nextUndo" of the response is null
    And the folder "library" equals the snapshot

  Scenario: Another pattern can be tried
    When I request "GET /api/v1/organize?pattern=%7Bauthor%7D%2F%7Btitle%7D.%7Bext%7D"
    Then the response status is 200
    And the JSON field "moves[0].to" of the response is "Martin Fowler/Patterns of Enterprise Application Architecture.epub"
    And the folder "library" equals the snapshot

  Scenario Outline: A pattern that is not valid is refused
    When I request "GET /api/v1/organize?pattern=<pattern>"
    Then the response status is 400
    And the JSON field "detail" of the response contains "pattern"

    Examples:
      | pattern                  |
      | %7Bnope%7D.%7Bext%7D     |
      | %7Btitle%7D              |

  Scenario: Applying moves the files, the sidecar and the index follow
    When I send "POST /api/v1/organize" with the JSON:
      """
      {}
      """
    Then the response status is 200
    And the JSON field "files" of the response is 3
    And the JSON field "works" of the response is 2
    And the file "library/Fowler, Martin/Patterns of Enterprise Application Architecture (2002)/Patterns of Enterprise Application Architecture.epub" exists
    And the file "library/inbox/pea.epub" does not exist
    And the file "Patterns of Enterprise Application Architecture.epub" in the index has the path "Fowler, Martin/Patterns of Enterprise Application Architecture (2002)/Patterns of Enterprise Application Architecture.epub"
    When I request "GET /api/v1/organize"
    Then the JSON field "files" of the response is 0
    And the JSON field "nextUndo" of the response is "organize"

  Scenario: The pattern that was previewed is the one that is applied
    When I send "POST /api/v1/organize" with the JSON:
      """
      { "pattern": "{author}/{title}.{ext}" }
      """
    Then the response status is 200
    And the file "library/Martin Fowler/Patterns of Enterprise Application Architecture.epub" exists

  Scenario: Undo puts everything back exactly as it was
    Given I send "POST /api/v1/organize" with the JSON:
      """
      {}
      """
    When I send "POST /api/v1/undo" with the JSON:
      """
      {}
      """
    Then the response status is 200
    And the JSON field "label" of the response is "organize"
    And the JSON field "reverted" of the response is not zero
    And the JSON array "skipped" of the response has 0 entries
    And the folder "library" equals the snapshot
    When I request "GET /api/v1/organize"
    Then the JSON field "nextUndo" of the response is null

  Scenario: Undo reverts the last run, whatever it was
    Given I send "POST /api/v1/tags/rename" with the JSON:
      """
      { "from": "architecture", "to": "design" }
      """
    When I send "POST /api/v1/undo" with the JSON:
      """
      {}
      """
    Then the response status is 200
    And the JSON field "label" of the response is "tag rename"
    And the folder "library" equals the snapshot

  Scenario: Without a run there is nothing to undo
    When I send "POST /api/v1/undo" with the JSON:
      """
      {}
      """
    Then the response status is 409
    And the JSON field "detail" of the response contains "nothing to undo"

  Scenario: A file changed since is not moved back
    Given I send "POST /api/v1/organize" with the JSON:
      """
      {}
      """
    And the file "library/Fowler, Martin/Patterns of Enterprise Application Architecture (2002)/Patterns of Enterprise Application Architecture.pdf" has the content "changed"
    When I send "POST /api/v1/undo" with the JSON:
      """
      {}
      """
    Then the response status is 200
    And the JSON field "skipped[0].reason" of the response is "changed since"
    And the file "library/inbox/pea.epub" exists
