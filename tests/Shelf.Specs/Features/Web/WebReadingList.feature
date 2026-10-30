@PRD-7.3 @F-RLS-1 @F-RLS-2 @F-RLS-5 @M14
Feature: The reading list over HTTP
  As the web interface
  I want to read and change the reading list through the API
  So that a person can manage what to read next from the browser

  Background:
    Given an empty working directory
    And I have run shelf with arguments "init library"
    And a sample PDF "library/A/Kubernetes/kp.pdf" with the title "Kubernetes Patterns", the author "Bilgin Ibryam" and the pages:
      | text |
      | One. |
    And the sidecar "library/A/Kubernetes/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000001", "title": "Kubernetes Patterns", "authors": ["Bilgin Ibryam"], "language": "en", "files": [{ "name": "kp.pdf" }] }
      """
    And a sample PDF "library/B/Attention/at.pdf" with the title "Attention Is All You Need", the author "Ashish Vaswani" and the pages:
      | text |
      | Two. |
    And the sidecar "library/B/Attention/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000002", "kind": "paper", "title": "Attention Is All You Need", "authors": ["Ashish Vaswani"], "language": "en", "files": [{ "name": "at.pdf" }] }
      """
    And a sample PDF "library/D/Done/dn.pdf" with the title "Already Read", the author "N. Body" and the pages:
      | text  |
      | Four. |
    And the sidecar "library/D/Done/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000004", "title": "Already Read", "authors": ["N. Body"], "language": "en", "readStatus": "read", "files": [{ "name": "dn.pdf" }] }
      """
    And I have run shelf with arguments "reindex --library library"
    And the web server is running on the library "library"

  Scenario: The reading list is empty at the start
    When I request "GET /api/v1/reading-list"
    Then the response status is 200
    And the JSON array "works" of the response has 0 entries

  Scenario Outline: A work is put on the list and shows when
    When I request "PUT /api/v1/works/<id>/reading-list"
    Then the response status is 200
    And the JSON field "readingListAt" of the response is not empty
    When I request "GET /api/v1/works/<id>"
    Then the JSON field "readingListAt" of the response is not empty
    When I request "GET /api/v1/reading-list"
    Then the JSON array "works" of the response has 1 entries
    And the JSON field "works[0].title" of the response is "<title>"

    Examples:
      | id                         | title                     |
      | 01J9Z3K0000000000000000001 | Kubernetes Patterns       |
      | 01J9Z3K0000000000000000002 | Attention Is All You Need |

  Scenario: The list shows the works oldest first
    Given I request "PUT /api/v1/works/01J9Z3K0000000000000000002/reading-list"
    And I request "PUT /api/v1/works/01J9Z3K0000000000000000001/reading-list"
    When I request "GET /api/v1/reading-list"
    Then the JSON field "works[0].title" of the response is "Attention Is All You Need"
    And the JSON field "works[1].title" of the response is "Kubernetes Patterns"

  Scenario: A work is taken off the list
    Given I request "PUT /api/v1/works/01J9Z3K0000000000000000001/reading-list"
    When I request "DELETE /api/v1/works/01J9Z3K0000000000000000001/reading-list"
    Then the response status is 200
    And the JSON field "readingListAt" of the response is null
    When I request "GET /api/v1/reading-list"
    Then the JSON array "works" of the response has 0 entries

  Scenario: Putting a work on the list twice keeps the first time
    Given I request "PUT /api/v1/works/01J9Z3K0000000000000000001/reading-list"
    And I note the JSON field "readingListAt" of the response as the entry
    When I request "PUT /api/v1/works/01J9Z3K0000000000000000001/reading-list"
    Then the response status is 200
    And the JSON field "readingListAt" of the response is the noted entry

  Scenario: A work that is read already is refused
    When I request "PUT /api/v1/works/01J9Z3K0000000000000000004/reading-list"
    Then the response status is 409
    And the JSON field "detail" of the response contains "already read"

  Scenario: An unknown work is not found
    When I request "PUT /api/v1/works/01J9Z3K0000000000000000099/reading-list"
    Then the response status is 404

  Scenario: Marking a work as read takes it off the list
    Given I request "PUT /api/v1/works/01J9Z3K0000000000000000001/reading-list"
    When I send "PATCH /api/v1/works/01J9Z3K0000000000000000001" with the JSON:
      """
      { "readStatus": "read" }
      """
    Then the response status is 200
    And the JSON field "readingListAt" of the response is null
    When I request "GET /api/v1/reading-list"
    Then the JSON array "works" of the response has 0 entries

  Scenario: A work that is being read stays on the list
    Given I request "PUT /api/v1/works/01J9Z3K0000000000000000001/reading-list"
    When I send "PATCH /api/v1/works/01J9Z3K0000000000000000001" with the JSON:
      """
      { "readStatus": "reading" }
      """
    Then the response status is 200
    And the JSON field "readingListAt" of the response is not empty
