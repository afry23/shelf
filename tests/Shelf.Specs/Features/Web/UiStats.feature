@ui @F-STA-3 @PRD-8.2 @M12
Feature: The statistics view
  As a person who looks at their reading
  I want a page with the books I finished by month and what I read lately
  So that I see how my reading goes

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

  Scenario: The main navigation leads to the statistics
    Given I open "/library" in the browser
    When I click the "Statistics" link
    Then the browser address contains "/stats"
    And the heading "Statistics" is shown

  Scenario: The books finished are shown by month
    When I open "/stats" in the browser
    Then the page shows the text "Finished by month"
    And the list "Finished by month" has 2 items
    And the row "2026-09" of the list "Finished by month" shows "2"
    And the row "2026-10" of the list "Finished by month" shows "1"

  Scenario: What is being read links to the book
    When I open "/stats" in the browser
    Then the link "Attention Is All You Need" points to an address containing "/works/01J9Z3K0000000000000000005"
    And the page shows the text "Recently read"
    And the link "Loose Notes" points to an address containing "/works/01J9Z3K0000000000000000006"

  Scenario: The books read without a date are named
    When I open "/stats" in the browser
    Then the page shows the text "1 book is marked as read without a date"
