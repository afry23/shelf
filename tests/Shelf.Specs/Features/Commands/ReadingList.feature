@F-RLS-1 @F-RLS-2 @F-RLS-3 @F-RLS-4 @M14
Feature: A reading list for books, papers and articles
  As a person with more to read than time
  I want to put works on a reading list and take them off again
  So that I know what to read next, and a work I finished leaves the list by itself

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
      { "schema": 1, "id": "01J9Z3K0000000000000000002", "kind": "paper", "title": "Attention Is All You Need", "authors": ["Ashish Vaswani"], "language": "en", "doi": "10.48550/arXiv.1706.03762", "files": [{ "name": "at.pdf" }] }
      """
    And an article "Why Rust" from the site "blog.example.com" with the text "Rust has no garbage collector." and 0 images and the ID "01J9Z3K0000000000000000003"
    And a sample PDF "library/D/Done/dn.pdf" with the title "Already Read", the author "N. Body" and the pages:
      | text  |
      | Four. |
    And the sidecar "library/D/Done/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000004", "title": "Already Read", "authors": ["N. Body"], "language": "en", "readStatus": "read", "files": [{ "name": "dn.pdf" }] }
      """
    And I have run shelf with arguments "reindex --library library"

  # ---- put on and take off (F-RLS-1, F-RLS-3) ----

  Scenario Outline: A work of any kind can be put on the reading list
    When I run shelf with arguments "readlist add <id> --library library"
    Then the exit code is 0
    And the file "<sidecar>" contains "readingList"
    When I run shelf with arguments "readlist show --library library"
    Then standard output contains "<title>"

    Examples:
      | kind    | id                         | sidecar                         | title                     |
      | book    | 01J9Z3K0000000000000000001 | library/A/Kubernetes/book.json  | Kubernetes Patterns       |
      | paper   | 01J9Z3K0000000000000000002 | library/B/Attention/book.json   | Attention Is All You Need |

  Scenario: An article can be put on the reading list
    When I run shelf with arguments "readlist add 01J9Z3K0000000000000000003 --library library"
    Then the exit code is 0
    When I run shelf with arguments "readlist show --library library"
    Then standard output contains "Why Rust"

  Scenario: Several works are put on the list with one command, oldest first
    When I run shelf with arguments "readlist add 01J9Z3K0000000000000000002 01J9Z3K0000000000000000001 --library library"
    Then the exit code is 0
    When I run shelf with arguments "readlist show --library library"
    Then "Attention Is All You Need" stands before "Kubernetes Patterns" in standard output

  Scenario: A work is taken off the reading list
    Given I have run shelf with arguments "readlist add 01J9Z3K0000000000000000001 --library library"
    When I run shelf with arguments "readlist rm 01J9Z3K0000000000000000001 --library library"
    Then the exit code is 0
    And the file "library/A/Kubernetes/book.json" does not contain "readingList"
    When I run shelf with arguments "readlist show --library library"
    Then standard output does not contain "Kubernetes Patterns"

  Scenario: Putting a work on the list twice changes nothing
    Given I have run shelf with arguments "readlist add 01J9Z3K0000000000000000001 --library library"
    When I run shelf with arguments "readlist add 01J9Z3K0000000000000000001 --library library"
    Then the exit code is 0
    And standard output contains "Already on the reading list"
    And the file "library/A/Kubernetes/book.json" contains "readingList"

  Scenario: Taking off a work that is not on the list is not an error
    When I run shelf with arguments "readlist rm 01J9Z3K0000000000000000001 --library library"
    Then the exit code is 0

  Scenario: A work that is read already cannot be put on the list
    When I run shelf with arguments "readlist add 01J9Z3K0000000000000000004 --library library"
    Then the exit code is 1
    And standard error contains "already read"
    And the file "library/D/Done/book.json" does not contain "readingList"

  Scenario: An unknown work is reported
    When I run shelf with arguments "readlist add 01J9Z3K0000000000000000099 --library library"
    Then the exit code is 1

  Scenario: The reading list is empty at the start
    When I run shelf with arguments "readlist show --json --library library"
    Then the exit code is 0
    And the JSON property "works" of standard output has 0 items

  Scenario: The list in JSON has the works oldest first
    Given I have run shelf with arguments "readlist add 01J9Z3K0000000000000000002 --library library"
    And I have run shelf with arguments "readlist add 01J9Z3K0000000000000000001 --library library"
    When I run shelf with arguments "readlist show --json --library library"
    Then the JSON property "works" of standard output has 2 items
    And the JSON of standard output has the string "works[0].id" equal to "01J9Z3K0000000000000000002"
    And the JSON of standard output has the string "works[1].id" equal to "01J9Z3K0000000000000000001"

  # ---- a read work leaves the list (F-RLS-2) ----

  Scenario: A work that is marked read leaves the list
    Given I have run shelf with arguments "readlist add 01J9Z3K0000000000000000001 --library library"
    When I run shelf with arguments "edit 01J9Z3K0000000000000000001 --status read --library library"
    Then the exit code is 0
    And the file "library/A/Kubernetes/book.json" does not contain "readingList"
    When I run shelf with arguments "readlist show --library library"
    Then standard output does not contain "Kubernetes Patterns"

  Scenario: A work that is being read stays on the list
    Given I have run shelf with arguments "readlist add 01J9Z3K0000000000000000001 --library library"
    When I run shelf with arguments "edit 01J9Z3K0000000000000000001 --status reading --library library"
    Then the exit code is 0
    And the file "library/A/Kubernetes/book.json" contains "readingList"

  Scenario: A work that is set back to unread is not put on the list again
    Given I have run shelf with arguments "readlist add 01J9Z3K0000000000000000001 --library library"
    And I have run shelf with arguments "edit 01J9Z3K0000000000000000001 --status read --library library"
    When I run shelf with arguments "edit 01J9Z3K0000000000000000001 --status unread --library library"
    Then the file "library/A/Kubernetes/book.json" does not contain "readingList"

  Scenario: Merging with a read work leaves the merged work off the list
    Given I have run shelf with arguments "readlist add 01J9Z3K0000000000000000001 --library library"
    When I run shelf with arguments "merge 01J9Z3K0000000000000000001 01J9Z3K0000000000000000004 --library library"
    Then the exit code is 0
    And the file "library/A/Kubernetes/book.json" does not contain "readingList"

  Scenario: Merging two works on the list keeps the earlier time
    Given the sidecar "library/A/Kubernetes/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000001", "title": "Kubernetes Patterns", "authors": ["Bilgin Ibryam"], "language": "en", "readingList": "2026-10-01T08:00:00Z", "files": [{ "name": "kp.pdf" }] }
      """
    And the sidecar "library/B/Attention/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000002", "title": "Attention Is All You Need", "authors": ["Ashish Vaswani"], "language": "en", "readingList": "2026-10-10T08:00:00Z", "files": [{ "name": "at.pdf" }] }
      """
    And I have run shelf with arguments "reindex --library library"
    When I run shelf with arguments "merge 01J9Z3K0000000000000000001 01J9Z3K0000000000000000002 --library library"
    Then the exit code is 0
    And the file "library/A/Kubernetes/book.json" contains "2026-10-01T08:00:00Z"
    And the file "library/A/Kubernetes/book.json" does not contain "2026-10-10T08:00:00Z"

  # ---- search and index (F-RLS-4) ----

  Scenario: The search filter selects the works on the list
    Given I have run shelf with arguments "readlist add 01J9Z3K0000000000000000001 --library library"
    When I run shelf with arguments "search readinglist:yes --library library"
    Then the exit code is 0
    And standard output contains "Kubernetes Patterns"
    And standard output does not contain "Attention Is All You Need"

  Scenario: The search filter selects the works that are not on the list
    Given I have run shelf with arguments "readlist add 01J9Z3K0000000000000000001 --library library"
    When I run shelf with arguments "search readinglist:no --library library"
    Then the exit code is 0
    And standard output contains "Attention Is All You Need"
    And standard output does not contain "Kubernetes Patterns"

  Scenario: A reindex restores the reading list from the sidecars
    Given the sidecar "library/A/Kubernetes/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000001", "title": "Kubernetes Patterns", "authors": ["Bilgin Ibryam"], "language": "en", "readingList": "2026-10-01T08:00:00Z", "files": [{ "name": "kp.pdf" }] }
      """
    When I run shelf with arguments "reindex --library library"
    And I run shelf with arguments "readlist show --library library"
    Then standard output contains "Kubernetes Patterns"

  Scenario: A time in the wrong form is reported when the library is read
    Given the sidecar "library/A/Kubernetes/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000001", "title": "Kubernetes Patterns", "authors": ["Bilgin Ibryam"], "readingList": "next week", "files": [{ "name": "kp.pdf" }] }
      """
    When I run shelf with arguments "reindex --library library"
    Then standard error contains "readingList"
