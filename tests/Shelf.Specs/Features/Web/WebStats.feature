@F-STA-2 @PRD-7.3 @M12
Feature: Statistics of the library over HTTP
  As the statistics view of the web interface
  I want the numbers of the library
  So that a person sees what they read and when

  Background:
    Given an empty working directory
    And I have run shelf with arguments "init library"
    And a sample PDF "library/A/Kubernetes/kp.pdf" with the title "Kubernetes Patterns", the author "Bilgin Ibryam" and the pages:
      | text   |
      | One. |
    And the sidecar "library/A/Kubernetes/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000001", "title": "Kubernetes Patterns", "authors": ["Bilgin Ibryam"], "language": "en", "readStatus": "read", "finishedAt": "2026-09-12T10:00:00Z", "files": [{ "name": "kp.pdf", "progress": { "locator": "page=1", "updatedAt": "2026-09-12T09:00:00Z" } }] }
      """
    And a sample PDF "library/B/Events/es.pdf" with the title "Event Sourcing", the author "Jördis Ålander" and the pages:
      | text   |
      | Two. |
    And the sidecar "library/B/Events/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000002", "title": "Event Sourcing", "authors": ["Jördis Ålander"], "language": "en", "readStatus": "read", "finishedAt": "2026-09-30T18:00:00Z", "files": [{ "name": "es.pdf" }] }
      """
    And a sample PDF "library/C/Cloud/cp.pdf" with the title "Cloud Patterns", the author "Anna Müller" and the pages:
      | text   |
      | Three. |
    And the sidecar "library/C/Cloud/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000003", "title": "Cloud Patterns", "authors": ["Anna Müller"], "language": "en", "readStatus": "read", "finishedAt": "2026-10-05T08:00:00Z", "files": [{ "name": "cp.pdf" }] }
      """
    And a sample PDF "library/D/Systems/vs.pdf" with the title "Verteilte Systeme", the author "Lukas Weiß" and the pages:
      | text   |
      | Four. |
    And the sidecar "library/D/Systems/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000004", "title": "Verteilte Systeme", "authors": ["Lukas Weiß"], "language": "en", "readStatus": "read", "files": [{ "name": "vs.pdf" }] }
      """
    And a sample PDF "library/E/Attention/at.pdf" with the title "Attention Is All You Need", the author "Ashish Vaswani" and the pages:
      | text   |
      | Five. |
    And the sidecar "library/E/Attention/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000005", "title": "Attention Is All You Need", "authors": ["Ashish Vaswani"], "language": "en", "readStatus": "reading", "files": [{ "name": "at.pdf", "progress": { "locator": "page=1", "updatedAt": "2026-10-20T09:00:00Z" } }] }
      """
    And a sample PDF "library/F/Loose/ln.pdf" with the title "Loose Notes", the author "N. Body" and the pages:
      | text   |
      | Six. |
    And the sidecar "library/F/Loose/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000006", "title": "Loose Notes", "authors": ["N. Body"], "language": "en", "files": [{ "name": "ln.pdf", "progress": { "locator": "page=1", "updatedAt": "2026-10-25T07:30:00Z" } }] }
      """
    And I have run shelf with arguments "reindex --library library"
    And the web server is running on the library "library"

  Scenario: The library in numbers
    When I request "GET /api/v1/stats"
    Then the response status is 200
    And the JSON field "works" of the response is 6
    And the JSON field "readStatus.read" of the response is 4
    And the JSON field "readStatus.reading" of the response is 1
    And the JSON field "readStatus.unread" of the response is 1
    And the JSON field "readWithoutDate" of the response is 1

  Scenario: The books finished by month, oldest first
    When I request "GET /api/v1/stats"
    Then the JSON array "finishedByMonth" of the response has 2 entries
    And the JSON field "finishedByMonth[0].month" of the response is "2026-09"
    And the JSON field "finishedByMonth[0].works" of the response is 2
    And the JSON field "finishedByMonth[1].month" of the response is "2026-10"

  Scenario: What is being read and what was read lately
    When I request "GET /api/v1/stats"
    Then the JSON array "reading" of the response has 1 entries
    And the JSON field "reading[0].id" of the response is "01J9Z3K0000000000000000005"
    And the JSON field "reading[0].lastReadAt" of the response is "2026-10-20T09:00:00Z"
    And the JSON field "recent[0].title" of the response is "Loose Notes"
    And the JSON field "recent[0].status" of the response is "unread"

  Scenario: Setting a book to read in the web interface dates it
    When I send "PATCH /api/v1/works/01J9Z3K0000000000000000005" with the JSON:
      """
      { "readStatus": "read" }
      """
    Then the response status is 200
    And the file "library/E/Attention/book.json" contains "finishedAt"
    When I request "GET /api/v1/stats"
    Then the JSON field "readStatus.read" of the response is 5
    And the JSON array "reading" of the response has 0 entries

  Scenario: A request asks nobody and changes nothing
    Given I take a snapshot of the folder "library"
    When I request "GET /api/v1/stats"
    Then the folder "library" equals the snapshot
