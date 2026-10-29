@F-STA-1 @F-STA-2 @M12
Feature: What was read and when
  As a person who keeps a reading list
  I want to see how many books I finished each month and what I read lately
  So that I know how my reading goes

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

  # ---- the date of finishing (F-STA-1) ----

  Scenario: A book that is set to read gets the date it was finished
    When I run shelf with arguments "edit 01J9Z3K0000000000000000005 --status read --library library"
    Then the exit code is 0
    And the file "library/E/Attention/book.json" contains "finishedAt"

  Scenario: A book that is read already keeps its date
    When I run shelf with arguments "edit 01J9Z3K0000000000000000001 --status read --library library"
    Then the file "library/A/Kubernetes/book.json" contains "2026-09-12T10:00:00Z"

  Scenario Outline: A book that is not read any more has no date
    When I run shelf with arguments "edit 01J9Z3K0000000000000000001 --status <status> --library library"
    Then the exit code is 0
    And the file "library/A/Kubernetes/book.json" does not contain "finishedAt"

    Examples:
      | status  |
      | unread  |
      | reading |

  Scenario: A date in the wrong form is reported when the library is read
    Given the sidecar "library/F/Loose/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000006", "title": "Loose Notes", "authors": ["N. Body"], "readStatus": "read", "finishedAt": "last week", "files": [{ "name": "ln.pdf" }] }
      """
    When I run shelf with arguments "reindex --library library"
    Then standard error contains "finishedAt"

  Scenario: Merging keeps the later date of the two
    When I run shelf with arguments "merge 01J9Z3K0000000000000000001 01J9Z3K0000000000000000003 --library library"
    Then the exit code is 0
    And the file "library/A/Kubernetes/book.json" contains "2026-10-05T08:00:00Z"
    And the file "library/A/Kubernetes/book.json" does not contain "2026-09-12T10:00:00Z"

  # ---- the numbers (F-STA-2) ----

  Scenario: The books finished are counted by month
    When I run shelf with arguments "stats --library library"
    Then the exit code is 0
    And standard output matches the pattern "(?m)^  2026-09 +2$"
    And standard output matches the pattern "(?m)^  2026-10 +1$"
    And standard output contains "read without a date"

  Scenario: The numbers as JSON
    When I run shelf with arguments "stats --library library --json"
    Then the exit code is 0
    And the JSON of standard output has the number "finishedByMonth[0].works" equal to 2
    And the JSON of standard output has the string "finishedByMonth[0].month" equal to "2026-09"
    And the JSON of standard output has the string "finishedByMonth[1].month" equal to "2026-10"
    And the JSON of standard output has the number "readWithoutDate" equal to 1
    And the JSON property "reading" of standard output has 1 items
    And the JSON of standard output has the string "reading[0].title" equal to "Attention Is All You Need"
    And the JSON of standard output has the string "reading[0].lastReadAt" equal to "2026-10-20T09:00:00Z"

  Scenario: The works read lately come first, whatever their status
    When I run shelf with arguments "stats --library library --json"
    Then the JSON of standard output has the string "recent[0].title" equal to "Loose Notes"
    And the JSON of standard output has the string "recent[1].title" equal to "Attention Is All You Need"
    And the JSON of standard output has the string "recent[2].title" equal to "Kubernetes Patterns"
    And the JSON property "recent" of standard output has 3 items

  Scenario: A library without anything read has no reading sections
    Given an empty working directory
    And I have run shelf with arguments "init plain"
    When I run shelf with arguments "stats --library plain"
    Then standard output does not contain "Finished by month"
    And standard output does not contain "Recently read"

  Scenario: Reindex brings the dates back
    When I run shelf with arguments "reindex --library library"
    And I run shelf with arguments "stats --library library --json"
    Then the JSON of standard output has the number "finishedByMonth[0].works" equal to 2
