@F-STA-2 @PRD-7.2 @M12
Feature: Statistics of the library through MCP
  As an AI assistant
  I want to see what the person read and when
  So that I can talk about their reading

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
    And the MCP server is running on the library "library"

  Scenario: get_stats gives the numbers of the library
    When I call the tool "get_stats" with the arguments:
      """
      {}
      """
    Then the tool call succeeds
    And the result has the number "works" equal to 6
    And the result has the number "readWithoutDate" equal to 1
    And the result has the string "finishedByMonth[0].month" equal to "2026-09"
    And the result has the string "reading[0].title" equal to "Attention Is All You Need"
    And the result has the string "recent[0].title" equal to "Loose Notes"

  Scenario: get_stats only reads
    Then the tool "get_stats" is read-only
