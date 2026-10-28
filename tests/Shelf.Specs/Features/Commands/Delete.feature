@F-DUP-7 @F-DUP-4 @M5
Feature: Deleting files and works
  As a user who wants a file or a whole book gone from the library
  I want to delete it with shelf delete <work> [file]
  So that it goes to the trash, can be undone and is deleted for good only by shelf trash empty

  Background:
    Given an empty working directory
    And I have run shelf with arguments "init library"
    And a sample EPUB "library/Ibryam, Bilgin/Kubernetes Patterns (2019)/kp.epub" with the title "Kubernetes Patterns", the authors "Bilgin Ibryam" and the text seed 1
    And a sample PDF "library/Ibryam, Bilgin/Kubernetes Patterns (2019)/kp.pdf" with the title "Kubernetes Patterns", the author "Bilgin Ibryam" and the text seed 2
    And the sidecar "library/Ibryam, Bilgin/Kubernetes Patterns (2019)/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K1000000000000000001", "title": "Kubernetes Patterns", "authors": ["Bilgin Ibryam"], "year": 2019, "tags": ["devops"], "files": [{ "name": "kp.epub" }, { "name": "kp.pdf" }] }
      """
    And the sidecar "library/Ibryam, Bilgin/Kubernetes Patterns (2019)/annotations.json" is replaced by:
      """
      { "schema": 1, "annotations": [
        { "id": "01J9Z3K00000000000000000A1", "file": "kp.epub", "kind": "note", "note": "Epub note.", "createdAt": "2026-10-25T10:00:00Z", "updatedAt": "2026-10-25T10:00:00Z" },
        { "id": "01J9Z3K00000000000000000A2", "file": "kp.pdf", "kind": "note", "note": "Pdf note.", "createdAt": "2026-10-25T10:05:00Z", "updatedAt": "2026-10-25T10:05:00Z" }
      ] }
      """
    And a sample EPUB "library/Davis, Cornelia/Cloud Native Patterns (2019)/cnp.epub" with the title "Cloud Native Patterns", the authors "Cornelia Davis" and the text seed 3
    And the sidecar "library/Davis, Cornelia/Cloud Native Patterns (2019)/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K2000000000000000002", "title": "Cloud Native Patterns", "authors": ["Cornelia Davis"], "year": 2019, "files": [{ "name": "cnp.epub" }] }
      """
    And I have run shelf with arguments "reindex --library library"
    And I take a snapshot of the folder "library"

  Scenario: Without --yes it is a dry run
    When I run shelf with arguments "delete 01J9Z3K1 kp.pdf --library library"
    Then the exit code is 0
    And standard output contains "Would move kp.pdf to the trash"
    And standard output contains "--yes"
    And the folder "library" equals the snapshot

  Scenario: A file goes to the trash and the work keeps the other file
    When I run shelf with arguments "delete 01J9Z3K1 kp.pdf --yes --library library"
    Then the exit code is 0
    And standard output contains "Moved kp.pdf to the trash"
    And the file "library/Ibryam, Bilgin/Kubernetes Patterns (2019)/kp.pdf" does not exist
    And the file "library/Ibryam, Bilgin/Kubernetes Patterns (2019)/kp.epub" exists
    And the directory "library/.shelf/trash" exists
    And the sidecar "library/Ibryam, Bilgin/Kubernetes Patterns (2019)/book.json" lists the files "kp.epub"
    And the sidecar "library/Ibryam, Bilgin/Kubernetes Patterns (2019)/book.json" has the title "Kubernetes Patterns" and the authors "Bilgin Ibryam"
    And the table "works" of the index has 2 rows
    And the table "files" of the index has 2 rows

  Scenario: The notes of the deleted file go, the notes of the other file stay
    When I run shelf with arguments "delete 01J9Z3K1 kp.pdf --yes --library library"
    Then the file "library/Ibryam, Bilgin/Kubernetes Patterns (2019)/annotations.json" contains "Epub note."
    And the file "library/Ibryam, Bilgin/Kubernetes Patterns (2019)/annotations.json" does not contain "Pdf note."

  Scenario Outline: The file can be named in several ways
    When I run shelf with arguments "delete 01J9Z3K1 '<file>' --yes --library library"
    Then the exit code is 0
    And the file "library/Ibryam, Bilgin/Kubernetes Patterns (2019)/kp.pdf" does not exist

    Examples:
      | file                                                             |
      | kp.pdf                                                           |
      | library/Ibryam, Bilgin/Kubernetes Patterns (2019)/kp.pdf         |

  Scenario: Deleting the last file deletes the work
    Given I have run shelf with arguments "delete 01J9Z3K1 kp.pdf --yes --library library"
    When I run shelf with arguments "delete 01J9Z3K1 kp.epub --yes --library library"
    Then the exit code is 0
    And standard output contains "Moved kp.epub to the trash"
    And standard output contains "removed the work 01J9Z3K1000000000000000001"
    And the directory "library/Ibryam, Bilgin/Kubernetes Patterns (2019)" does not exist
    And the table "works" of the index has 1 rows

  Scenario: A whole work goes to the trash with its sidecar and notes
    When I run shelf with arguments "delete 01J9Z3K1 --yes --library library"
    Then the exit code is 0
    And standard output contains "Moved 01J9Z3K1000000000000000001 to the trash (2 files)"
    And the directory "library/Ibryam, Bilgin/Kubernetes Patterns (2019)" does not exist
    And the file "library/Davis, Cornelia/Cloud Native Patterns (2019)/cnp.epub" exists
    And the table "works" of the index has 1 rows
    And the table "files" of the index has 1 rows

  Scenario: The result is available as JSON
    When I run shelf with arguments "delete 01J9Z3K1 --yes --json --library library"
    Then the exit code is 0
    And the JSON of standard output has the string "work" equal to "01J9Z3K1000000000000000001"
    And the JSON of standard output has the number "trashed" equal to 2
    And the JSON of standard output has the boolean "workRemoved" equal to "true"

  Scenario: Undo brings back a file and a work
    Given I have run shelf with arguments "delete 01J9Z3K1 --yes --library library"
    When I run shelf with arguments "undo --library library"
    Then the exit code is 0
    And the folder "library" equals the snapshot
    And the table "works" of the index has 2 rows

  Scenario: Only the trash empty deletes for good
    Given I have run shelf with arguments "delete 01J9Z3K1 --yes --library library"
    When I run shelf with arguments "trash empty --yes --library library"
    Then the exit code is 0
    And standard output contains "for good"

  Scenario Outline: Deleting that cannot work is an error and nothing is changed
    When I run shelf with arguments "delete <work> <file> --yes --library library"
    Then the exit code is 1
    And standard error starts with "error: <message>"
    And the folder "library" equals the snapshot

    Examples:
      | work     | file        | message                                    |
      | 01J9Z3K1 | missing.pdf | 01J9Z3K1000000000000000001 has no file 'missing.pdf' |
      | 01J9Z3K9 | kp.pdf      | no work matches '01J9Z3K9'                 |
