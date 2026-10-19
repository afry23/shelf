@F-GRP-4 @F-DUP-5 @M5
Feature: Splitting a file off a work over HTTP
  As the duplicate view of the web interface
  I want to split a file that was grouped with the wrong book into a work of its own
  So that I do not need the command line to correct the grouping

  Background:
    Given an empty working directory
    And I have run shelf with arguments "init library"
    And a sample PDF "library/Love, Steve/C# Brain Teasers/c-brain-teasers_P1.0.pdf" with the title "C# Brain Teasers", the author "Steve Love" and the text seed 1
    And a sample PDF "library/Love, Steve/C# Brain Teasers/javascript-brain-teasers_P1.0.pdf" with the title "C# Brain Teasers", the author "Steve Love" and the text seed 5
    And the sidecar "library/Love, Steve/C# Brain Teasers/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K1000000000000000001", "title": "C# Brain Teasers", "authors": ["Steve Love"], "tags": ["puzzles"], "files": [{ "name": "c-brain-teasers_P1.0.pdf" }, { "name": "javascript-brain-teasers_P1.0.pdf", "progress": { "locator": "page=42", "updatedAt": "2026-10-12T19:04:00Z" } }] }
      """
    And a sample EPUB "library/Ibryam, Bilgin/Kubernetes Patterns (2019)/kp.epub" with the title "Kubernetes Patterns", the authors "Bilgin Ibryam" and the text seed 3
    And the sidecar "library/Ibryam, Bilgin/Kubernetes Patterns (2019)/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K2000000000000000002", "title": "Kubernetes Patterns", "authors": ["Bilgin Ibryam"], "year": 2019, "files": [{ "name": "kp.epub" }] }
      """
    And I have run shelf with arguments "reindex --library library"
    And I take a snapshot of the folder "library"
    And the web server is running on the library "library"

  Scenario: A file becomes a work of its own
    When I send "POST /api/v1/files/01J9Z3K1000000000000000001~javascript-brain-teasers_P1.0.pdf/split" with the JSON:
      """
      {}
      """
    Then the response status is 200
    And the JSON field "work" of the response is "01J9Z3K1000000000000000001"
    And the JSON field "newWork" of the response is not empty
    And the JSON field "file" of the response is "javascript-brain-teasers_P1.0.pdf"
    And the JSON field "folder" of the response is "Love, Steve/C# Brain Teasers (2)"
    And the file "library/Love, Steve/C# Brain Teasers (2)/javascript-brain-teasers_P1.0.pdf" exists
    And the file "library/Love, Steve/C# Brain Teasers/javascript-brain-teasers_P1.0.pdf" does not exist
    And the file "library/Love, Steve/C# Brain Teasers/c-brain-teasers_P1.0.pdf" exists

  Scenario: The group of duplicates is gone and the two works are listed
    When I request "GET /api/v1/duplicates"
    Then the JSON field "total" of the response is 1
    When I send "POST /api/v1/files/01J9Z3K1000000000000000001~javascript-brain-teasers_P1.0.pdf/split" with the JSON:
      """
      {}
      """
    And I request "GET /api/v1/duplicates"
    Then the JSON field "total" of the response is 0
    When I request "GET /api/v1/works"
    Then the JSON field "total" of the response is 3

  Scenario: The old work keeps its tags and the file takes its reading position along
    When I send "POST /api/v1/files/01J9Z3K1000000000000000001~javascript-brain-teasers_P1.0.pdf/split" with the JSON:
      """
      {}
      """
    Then the file "library/Love, Steve/C# Brain Teasers/book.json" contains "puzzles"
    And the file "library/Love, Steve/C# Brain Teasers/book.json" does not contain "page=42"
    And the file "library/Love, Steve/C# Brain Teasers (2)/book.json" contains "page=42"
    And the sidecar "library/Love, Steve/C# Brain Teasers/book.json" lists the files "c-brain-teasers_P1.0.pdf"
    And the sidecar "library/Love, Steve/C# Brain Teasers (2)/book.json" lists the files "javascript-brain-teasers_P1.0.pdf"

  Scenario: The two works are marked as not belonging together
    When I send "POST /api/v1/files/01J9Z3K1000000000000000001~javascript-brain-teasers_P1.0.pdf/split" with the JSON:
      """
      {}
      """
    Then the sidecar "library/Love, Steve/C# Brain Teasers/book.json" read back marks 1 work as not duplicates
    And the sidecar "library/Love, Steve/C# Brain Teasers (2)/book.json" read back marks 1 work as not duplicates

  Scenario: The new work can be opened and its title corrected over HTTP
    Given I send "POST /api/v1/files/01J9Z3K1000000000000000001~javascript-brain-teasers_P1.0.pdf/split" with the JSON:
      """
      {}
      """
    And I note the JSON field "newWork" of the response as the entry
    When I send "PATCH /api/v1/works/{entry}" with the JSON:
      """
      { "title": "JavaScript Brain Teasers" }
      """
    Then the response status is 200
    And the JSON field "title" of the response is "JavaScript Brain Teasers"
    And the JSON array "files" of the response has 1 entries

  # ---- refusals ----

  Scenario: The only file of a work cannot be split off
    When I send "POST /api/v1/files/01J9Z3K2000000000000000002~kp.epub/split" with the JSON:
      """
      {}
      """
    Then the response status is 409
    And the JSON field "detail" of the response contains "has only one file"
    And the folder "library" equals the snapshot

  Scenario Outline: A file that does not exist is a 404
    When I send "POST /api/v1/files/<id>/split" with the JSON:
      """
      {}
      """
    Then the response status is 404
    And the folder "library" equals the snapshot

    Examples:
      | id                                                  |
      | 01J9Z3K1000000000000000001~missing.pdf              |
      | 01J9Z3K9000000000000000009~c-brain-teasers_P1.0.pdf |

  Scenario: A malformed file ID is a bad request
    When I send "POST /api/v1/files/not-an-id/split" with the JSON:
      """
      {}
      """
    Then the response status is 400
    And the folder "library" equals the snapshot
