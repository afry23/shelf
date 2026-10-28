@F-GRP-9 @M7
Feature: Creating a work by hand
  As a user whose file is not recognized or must not be grouped
  I want to create a work with shelf create <file>
  So that the work exists with the metadata I give

  Background:
    Given an empty working directory
    And I have run shelf with arguments "init library"
    And a sample EPUB "library/Ibryam, Bilgin/Kubernetes Patterns (2019)/kp.epub" with the title "Kubernetes Patterns", the authors "Bilgin Ibryam" and the text seed 1
    And the sidecar "library/Ibryam, Bilgin/Kubernetes Patterns (2019)/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K1000000000000000001", "title": "Kubernetes Patterns", "authors": ["Bilgin Ibryam"], "year": 2019, "files": [{ "name": "kp.epub" }] }
      """
    And a sample EPUB "incoming/draft.epub" with the title "Wrong Title", the authors "Wrong Author" and the text seed 7
    And I have run shelf with arguments "reindex --library library"

  Scenario: The metadata of the file is used when nothing is given
    When I run shelf with arguments "create incoming/draft.epub --library library"
    Then the exit code is 0
    And standard output matches the pattern "^Created 01[0-9A-HJKMNP-TV-Z]{24} from draft\.epub$"
    And the file "library/Author, Wrong/Wrong Title/draft.epub" exists
    And the file "incoming/draft.epub" exists
    And the table "works" of the index has 2 rows

  Scenario: Given values replace the metadata of the file
    When I run shelf with arguments "create incoming/draft.epub --title 'Real Title' --author 'Jane Doe' --year 2026 --library library"
    Then the exit code is 0
    And the file "library/Doe, Jane/Real Title (2026)/draft.epub" exists
    And the sidecar "library/Doe, Jane/Real Title (2026)/book.json" has the title "Real Title" and the authors "Jane Doe"
    And the table "works" of the index has 2 rows

  Scenario: The file is not grouped into a work it matches
    Given a sample EPUB "incoming/kp-copy.epub" with the title "Kubernetes Patterns", the authors "Bilgin Ibryam" and the text seed 2
    When I run shelf with arguments "create incoming/kp-copy.epub --library library"
    Then the exit code is 0
    And the table "works" of the index has 2 rows
    And the file "library/Ibryam, Bilgin/Kubernetes Patterns/kp-copy.epub" exists

  Scenario: With --move the source is gone
    When I run shelf with arguments "create incoming/draft.epub --move --library library"
    Then the exit code is 0
    And the file "incoming/draft.epub" does not exist

  Scenario: Creating can be undone
    Given I have run shelf with arguments "create incoming/draft.epub --library library"
    When I run shelf with arguments "undo --library library"
    Then the exit code is 0
    And the file "library/Author, Wrong/Wrong Title/draft.epub" does not exist
    And the table "works" of the index has 1 rows

  Scenario Outline: Creating that cannot work is an error and nothing is changed
    Given I take a snapshot of the folder "library"
    When I run shelf with arguments "create <source> --library library"
    Then the exit code is 1
    And standard error starts with "error: <message>"
    And the folder "library" equals the snapshot

    Examples:
      | source              | message                              |
      | incoming/missing.epub | incoming/missing.epub does not exist |
      | incoming            | incoming is not a file               |

  Scenario: The file argument is needed
    When I run shelf with arguments "create --library library"
    Then the exit code is 2
    And standard error contains "Required argument missing"
