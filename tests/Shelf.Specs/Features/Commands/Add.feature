@F-GRP-7 @M7
Feature: Adding a file to a work
  As a user who follows a book that is still being written, like a Manning MEAP
  I want to add a new version to its work with shelf add <work> <file>
  So that all versions live in one work and the old one can be replaced

  Background:
    Given an empty working directory
    And I have run shelf with arguments "init library"
    And a sample EPUB "library/Ibryam, Bilgin/Kubernetes Patterns (2019)/kp-v1.epub" with the title "Kubernetes Patterns", the authors "Bilgin Ibryam" and the text seed 1
    And the sidecar "library/Ibryam, Bilgin/Kubernetes Patterns (2019)/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K1000000000000000001", "title": "Kubernetes Patterns", "authors": ["Bilgin Ibryam"], "year": 2019, "files": [{ "name": "kp-v1.epub", "progress": { "locator": "page=42", "updatedAt": "2026-10-12T19:04:00Z" } }] }
      """
    And the sidecar "library/Ibryam, Bilgin/Kubernetes Patterns (2019)/annotations.json" is replaced by:
      """
      { "schema": 1, "annotations": [
        { "id": "01J9Z3K00000000000000000A1", "file": "kp-v1.epub", "kind": "note", "note": "Old note.", "createdAt": "2026-10-25T10:00:00Z", "updatedAt": "2026-10-25T10:00:00Z" }
      ] }
      """
    And a sample EPUB "incoming/kp-v2.epub" with the title "Some Other Title", the authors "Somebody Else" and the text seed 2
    And I have run shelf with arguments "reindex --library library"
    And I take a snapshot of the folder "library"

  Scenario: The file joins the work whatever its own metadata says
    When I run shelf with arguments "add 01J9Z3K1 incoming/kp-v2.epub --library library"
    Then the exit code is 0
    And standard output contains "Added kp-v2.epub to 01J9Z3K1000000000000000001"
    And the file "library/Ibryam, Bilgin/Kubernetes Patterns (2019)/kp-v2.epub" exists
    And the file "incoming/kp-v2.epub" exists
    And the sidecar "library/Ibryam, Bilgin/Kubernetes Patterns (2019)/book.json" lists the files "kp-v1.epub, kp-v2.epub"
    And the sidecar "library/Ibryam, Bilgin/Kubernetes Patterns (2019)/book.json" has the title "Kubernetes Patterns" and the authors "Bilgin Ibryam"
    And the table "works" of the index has 1 rows
    And the table "files" of the index has 2 rows

  Scenario: With --move the source is gone
    When I run shelf with arguments "add 01J9Z3K1 incoming/kp-v2.epub --move --library library"
    Then the exit code is 0
    And the file "incoming/kp-v2.epub" does not exist
    And the file "library/Ibryam, Bilgin/Kubernetes Patterns (2019)/kp-v2.epub" exists

  Scenario: With --replace the old file goes to the trash
    When I run shelf with arguments "add 01J9Z3K1 incoming/kp-v2.epub --replace kp-v1.epub --library library"
    Then the exit code is 0
    And standard output contains "Replaced kp-v1.epub by kp-v2.epub"
    And the file "library/Ibryam, Bilgin/Kubernetes Patterns (2019)/kp-v1.epub" does not exist
    And the directory "library/.shelf/trash" exists
    And the sidecar "library/Ibryam, Bilgin/Kubernetes Patterns (2019)/book.json" lists the files "kp-v2.epub"
    And the table "files" of the index has 1 rows

  Scenario: Reading position and notes stay behind unless asked for
    When I run shelf with arguments "add 01J9Z3K1 incoming/kp-v2.epub --replace kp-v1.epub --library library"
    Then the file "library/Ibryam, Bilgin/Kubernetes Patterns (2019)/book.json" does not contain "page=42"
    And the file "library/Ibryam, Bilgin/Kubernetes Patterns (2019)/annotations.json" does not exist

  Scenario: --carry-notes hands reading position and notes to the new file
    When I run shelf with arguments "add 01J9Z3K1 incoming/kp-v2.epub --replace kp-v1.epub --carry-notes --library library"
    Then the exit code is 0
    And the file "library/Ibryam, Bilgin/Kubernetes Patterns (2019)/book.json" contains "page=42"
    And the file "library/Ibryam, Bilgin/Kubernetes Patterns (2019)/annotations.json" contains "Old note."
    And the file "library/Ibryam, Bilgin/Kubernetes Patterns (2019)/annotations.json" contains "kp-v2.epub"

  Scenario: A replacement can be undone
    Given I have run shelf with arguments "add 01J9Z3K1 incoming/kp-v2.epub --replace kp-v1.epub --library library"
    When I run shelf with arguments "undo --library library"
    Then the exit code is 0
    And the sidecar "library/Ibryam, Bilgin/Kubernetes Patterns (2019)/book.json" lists the files "kp-v1.epub"
    And the file "library/Ibryam, Bilgin/Kubernetes Patterns (2019)/kp-v1.epub" exists
    And the file "library/Ibryam, Bilgin/Kubernetes Patterns (2019)/kp-v2.epub" does not exist

  Scenario: A taken file name gets a number
    Given a sample EPUB "incoming2/kp-v1.epub" with the title "Kubernetes Patterns", the authors "Bilgin Ibryam" and the text seed 3
    When I run shelf with arguments "add 01J9Z3K1 incoming2/kp-v1.epub --library library"
    Then the exit code is 0
    And the file "library/Ibryam, Bilgin/Kubernetes Patterns (2019)/kp-v1 (1).epub" exists

  Scenario: A file that is already in the library is refused
    Given a sample EPUB "incoming3/kp-v1.epub" with the title "Kubernetes Patterns", the authors "Bilgin Ibryam" and the text seed 1
    When I run shelf with arguments "add 01J9Z3K1 incoming3/kp-v1.epub --library library"
    Then the exit code is 1
    And standard error starts with "error: incoming3/kp-v1.epub is already in the library"
    And the folder "library" equals the snapshot

  Scenario Outline: Adding that cannot work is an error and nothing is changed
    When I run shelf with arguments "add <work> <source> <options> --library library"
    Then the exit code is 1
    And standard error starts with "error: <message>"
    And the folder "library" equals the snapshot

    Examples:
      | work     | source                | options                | message                                                  |
      | 01J9Z3K9 | incoming/kp-v2.epub   |                        | no work matches '01J9Z3K9'                                |
      | 01J9Z3K1 | incoming/missing.epub |                        | incoming/missing.epub does not exist                      |
      | 01J9Z3K1 | incoming/kp-v2.epub   | --replace missing.epub | 01J9Z3K1000000000000000001 has no file 'missing.epub'     |

  Scenario: Both arguments are needed
    When I run shelf with arguments "add 01J9Z3K1 --library library"
    Then the exit code is 2
    And standard error contains "Required argument missing"
