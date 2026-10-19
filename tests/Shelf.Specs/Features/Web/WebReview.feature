@PRD-7.3 @PRD-8.2 @F-IMP-7 @F-GRP-4 @M5
Feature: Deciding the review queue over HTTP
  As the review view of the web interface
  I want the uncertain assignments of the import and a way to confirm or correct each
  So that nothing stays filed wrongly and nothing is decided without a person

  Background:
    Given an empty working directory
    And I have run shelf with arguments "init library"
    And a sample EPUB "library/Ibryam, Bilgin/Kubernetes Patterns (2019)/kp.epub" with the title "Kubernetes Patterns", the authors "Bilgin Ibryam" and the text seed 1
    And the sidecar "library/Ibryam, Bilgin/Kubernetes Patterns (2019)/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K1000000000000000001", "title": "Kubernetes Patterns", "authors": ["Bilgin Ibryam"], "year": 2019, "files": [{ "name": "kp.epub" }] }
      """
    And a sample PDF "library/Ibryam, Bilgin/Kubernetes Patterns/kp.pdf" with the title "Kubernetes Patterns", the author "B. Ibryam" and the text seed 1
    And the sidecar "library/Ibryam, Bilgin/Kubernetes Patterns/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K3000000000000000003", "title": "Kubernetes Patterns", "authors": ["B. Ibryam"], "files": [{ "name": "kp.pdf" }] }
      """
    And a sample PDF "library/Ålander, Jördis/Event Sourcing in Practice/es.pdf" with the title "Event Sourcing in Practice", the author "Jördis Ålander" and the text seed 2
    And the sidecar "library/Ålander, Jördis/Event Sourcing in Practice/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K2000000000000000002", "title": "Event Sourcing in Practice", "authors": ["Jördis Ålander"], "files": [{ "name": "es.pdf" }] }
      """
    And I have run shelf with arguments "reindex --library library"

  Scenario: An empty queue is an empty list
    Given the web server is running on the library "library"
    When I request "GET /api/v1/review"
    Then the response status is 200
    And the JSON field "total" of the response is 0
    And the JSON array "entries" of the response has 0 entries

  Scenario: The entries come with the titles of the works they name
    Given the review queue of "library" gets a grouping entry for the work "01J9Z3K3000000000000000003" about "Ibryam, Bilgin/Kubernetes Patterns/kp.pdf" with the candidate "01J9Z3K1000000000000000001" at 0.6 because "same title and author, year differs"
    And the review queue of "library" gets an entry of the kind "extraction-error" about "Ålander, Jördis/Event Sourcing in Practice/es.pdf" with the message "bad-pdf: not a PDF"
    And the web server is running on the library "library"
    When I request "GET /api/v1/review"
    Then the JSON field "total" of the response is 2
    And the JSON field "entries[0].kind" of the response is "grouping"
    And the JSON field "entries[0].file" of the response is "Ibryam, Bilgin/Kubernetes Patterns/kp.pdf"
    And the JSON field "entries[0].work" of the response is "01J9Z3K3000000000000000003"
    And the JSON field "entries[0].workTitle" of the response is "Kubernetes Patterns"
    And the JSON field "entries[0].candidates[0].work" of the response is "01J9Z3K1000000000000000001"
    And the JSON field "entries[0].candidates[0].title" of the response is "Kubernetes Patterns (2019)"
    And the JSON field "entries[0].candidates[0].confidence" of the response is 0.6
    And the JSON field "entries[0].candidates[0].signals[0]" of the response is "same title and author, year differs"
    And the JSON field "entries[1].kind" of the response is "extraction-error"
    And the JSON field "entries[1].message" of the response is "bad-pdf: not a PDF"
    And the JSON array "entries[1].candidates" of the response has 0 entries

  Scenario: Accepting merges the work into the best candidate and settles the entry
    Given the review queue of "library" gets a grouping entry for the work "01J9Z3K3000000000000000003" about "Ibryam, Bilgin/Kubernetes Patterns/kp.pdf" with the candidate "01J9Z3K1000000000000000001" at 0.6 because "same title and author, year differs"
    And the web server is running on the library "library"
    And I request "GET /api/v1/review"
    And I note the JSON field "entries[0].id" of the response as the entry
    When I send "POST /api/v1/review/{entry}/accept" with the JSON:
      """
      {}
      """
    Then the response status is 200
    And the JSON field "into" of the response is "01J9Z3K1000000000000000001"
    And the JSON field "movedFiles" of the response is 1
    And the sidecar "library/Ibryam, Bilgin/Kubernetes Patterns (2019)/book.json" lists the files "kp.epub, kp.pdf"
    When I request "GET /api/v1/review"
    Then the JSON field "total" of the response is 0

  Scenario: Accepting can name another work
    Given the review queue of "library" gets a grouping entry for the work "01J9Z3K3000000000000000003" about "Ibryam, Bilgin/Kubernetes Patterns/kp.pdf" with the candidate "01J9Z3K1000000000000000001" at 0.6 because "same title and author, year differs"
    And the web server is running on the library "library"
    And I request "GET /api/v1/review"
    And I note the JSON field "entries[0].id" of the response as the entry
    When I send "POST /api/v1/review/{entry}/accept" with the JSON:
      """
      { "into": "01J9Z3K2000000000000000002" }
      """
    Then the response status is 200
    And the JSON field "into" of the response is "01J9Z3K2000000000000000002"
    And the sidecar "library/Ålander, Jördis/Event Sourcing in Practice/book.json" lists the files "es.pdf, kp.pdf"

  Scenario: Dismissing keeps the works apart and remembers it
    Given the review queue of "library" gets a grouping entry for the work "01J9Z3K3000000000000000003" about "Ibryam, Bilgin/Kubernetes Patterns/kp.pdf" with the candidate "01J9Z3K1000000000000000001" at 0.6 because "same title and author, year differs"
    And the web server is running on the library "library"
    And I request "GET /api/v1/review"
    And I note the JSON field "entries[0].id" of the response as the entry
    When I send "POST /api/v1/review/{entry}/dismiss" with the JSON:
      """
      {}
      """
    Then the response status is 200
    And the table "works" of the index has 3 rows
    And the sidecar "library/Ibryam, Bilgin/Kubernetes Patterns (2019)/book.json" read back marks 1 work as not duplicates
    When I request "GET /api/v1/review"
    Then the JSON field "total" of the response is 0

  Scenario: An entry of another kind is settled by dismissing it
    Given the review queue of "library" gets an entry of the kind "weak-metadata" about "Ålander, Jördis/Event Sourcing in Practice/es.pdf" with the message "title from the file name"
    And the web server is running on the library "library"
    And I request "GET /api/v1/review"
    And I note the JSON field "entries[0].id" of the response as the entry
    When I send "POST /api/v1/review/{entry}/dismiss" with the JSON:
      """
      {}
      """
    Then the response status is 200
    And the sidecar "library/Ålander, Jördis/Event Sourcing in Practice/book.json" read back marks 0 works as not duplicates

  Scenario: Only a grouping entry can be accepted
    Given the review queue of "library" gets an entry of the kind "weak-metadata" about "Ålander, Jördis/Event Sourcing in Practice/es.pdf" with the message "title from the file name"
    And the web server is running on the library "library"
    And I request "GET /api/v1/review"
    And I note the JSON field "entries[0].id" of the response as the entry
    When I send "POST /api/v1/review/{entry}/accept" with the JSON:
      """
      {}
      """
    Then the response status is 400
    And the JSON field "detail" of the response contains "only a grouping entry"

  Scenario Outline: A request that cannot be done is refused and changes nothing
    Given the review queue of "library" gets a grouping entry for the work "01J9Z3K3000000000000000003" about "Ibryam, Bilgin/Kubernetes Patterns/kp.pdf" with the candidate "01J9Z3K1000000000000000001" at 0.6 because "same title and author, year differs"
    And the web server is running on the library "library"
    And I request "GET /api/v1/review"
    And I note the JSON field "entries[0].id" of the response as the entry
    And I take a snapshot of the folder "library"
    When I send "POST /api/v1/review/<entry>/<action>" with the JSON:
      """
      <body>
      """
    Then the response status is <status>
    And the folder "library" equals the snapshot

    Examples:
      | entry                      | action  | body                                   | status |
      | 01J9Z3K9000000000000000009 | accept  | {}                                     | 404    |
      | 01J9Z3K9000000000000000009 | dismiss | {}                                     | 404    |
      | not-an-id                  | dismiss | {}                                     | 404    |
      | {entry}                    | accept  | { "into": "01J9Z3K9000000000000000009" } | 404    |
      | {entry}                    | accept  | { "into": 3 }                          | 400    |
