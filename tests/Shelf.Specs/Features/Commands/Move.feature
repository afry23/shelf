@F-GRP-8 @M7
Feature: Moving a file to another work
  As a user whose file was put into the wrong work
  I want to move it with shelf move <work> <file>
  So that it belongs to the right work or to a new one

  Background:
    Given an empty working directory
    And I have run shelf with arguments "init library"
    And a sample EPUB "library/Ibryam, Bilgin/Kubernetes Patterns (2019)/kp.epub" with the title "Kubernetes Patterns", the authors "Bilgin Ibryam" and the text seed 1
    And a sample PDF "library/Ibryam, Bilgin/Kubernetes Patterns (2019)/cnp.pdf" with the title "Kubernetes Patterns", the author "Bilgin Ibryam" and the text seed 3
    And the sidecar "library/Ibryam, Bilgin/Kubernetes Patterns (2019)/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K1000000000000000001", "title": "Kubernetes Patterns", "authors": ["Bilgin Ibryam"], "year": 2019, "tags": ["devops"], "files": [{ "name": "kp.epub" }, { "name": "cnp.pdf", "progress": { "locator": "page=42", "updatedAt": "2026-10-12T19:04:00Z" } }] }
      """
    And the sidecar "library/Ibryam, Bilgin/Kubernetes Patterns (2019)/annotations.json" is replaced by:
      """
      { "schema": 1, "annotations": [
        { "id": "01J9Z3K00000000000000000A1", "file": "cnp.pdf", "kind": "note", "note": "Pdf note.", "createdAt": "2026-10-25T10:00:00Z", "updatedAt": "2026-10-25T10:00:00Z" }
      ] }
      """
    And a sample EPUB "library/Davis, Cornelia/Cloud Native Patterns (2019)/cnp.epub" with the title "Cloud Native Patterns", the authors "Cornelia Davis" and the text seed 2
    And the sidecar "library/Davis, Cornelia/Cloud Native Patterns (2019)/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K2000000000000000002", "title": "Cloud Native Patterns", "authors": ["Cornelia Davis"], "year": 2019, "files": [{ "name": "cnp.epub" }] }
      """
    And I have run shelf with arguments "reindex --library library"
    And I take a snapshot of the folder "library"

  Scenario: Without --yes it is a dry run
    When I run shelf with arguments "move 01J9Z3K1 cnp.pdf --to 01J9Z3K2 --library library"
    Then the exit code is 0
    And standard output contains "Would move cnp.pdf from 01J9Z3K1000000000000000001 to 01J9Z3K2000000000000000002"
    And standard output contains "--yes"
    And the folder "library" equals the snapshot

  Scenario: The file moves to the other work
    When I run shelf with arguments "move 01J9Z3K1 cnp.pdf --to 01J9Z3K2 --yes --library library"
    Then the exit code is 0
    And standard output contains "Moved cnp.pdf to 01J9Z3K2000000000000000002"
    And the file "library/Davis, Cornelia/Cloud Native Patterns (2019)/cnp.pdf" exists
    And the file "library/Ibryam, Bilgin/Kubernetes Patterns (2019)/cnp.pdf" does not exist
    And the sidecar "library/Davis, Cornelia/Cloud Native Patterns (2019)/book.json" lists the files "cnp.epub, cnp.pdf"
    And the sidecar "library/Ibryam, Bilgin/Kubernetes Patterns (2019)/book.json" lists the files "kp.epub"
    And the table "works" of the index has 2 rows
    And the table "files" of the index has 3 rows

  Scenario: Reading position and notes go with the file, the tags stay
    When I run shelf with arguments "move 01J9Z3K1 cnp.pdf --to 01J9Z3K2 --yes --library library"
    Then the file "library/Davis, Cornelia/Cloud Native Patterns (2019)/book.json" contains "page=42"
    And the file "library/Davis, Cornelia/Cloud Native Patterns (2019)/annotations.json" contains "Pdf note."
    And the file "library/Ibryam, Bilgin/Kubernetes Patterns (2019)/book.json" contains "devops"
    And the file "library/Ibryam, Bilgin/Kubernetes Patterns (2019)/book.json" does not contain "page=42"
    And the file "library/Ibryam, Bilgin/Kubernetes Patterns (2019)/annotations.json" does not exist

  Scenario: A taken file name gets a number
    Given a sample PDF "library/Davis, Cornelia/Cloud Native Patterns (2019)/cnp.pdf" with the title "Cloud Native Patterns", the author "Cornelia Davis" and the text seed 9
    And the sidecar "library/Davis, Cornelia/Cloud Native Patterns (2019)/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K2000000000000000002", "title": "Cloud Native Patterns", "authors": ["Cornelia Davis"], "year": 2019, "files": [{ "name": "cnp.epub" }, { "name": "cnp.pdf" }] }
      """
    And I have run shelf with arguments "reindex --library library"
    When I run shelf with arguments "move 01J9Z3K1 cnp.pdf --to 01J9Z3K2 --yes --library library"
    Then the exit code is 0
    And the file "library/Davis, Cornelia/Cloud Native Patterns (2019)/cnp (1).pdf" exists

  Scenario: With --new the file becomes a work of its own, named by its metadata
    When I run shelf with arguments "move 01J9Z3K1 cnp.pdf --new --title 'Cloud Notes' --author 'Jane Doe' --yes --library library"
    Then the exit code is 0
    And standard output matches the pattern "^Moved cnp\.pdf to 01[0-9A-HJKMNP-TV-Z]{24}\. Undo with shelf undo\.$"
    And the file "library/Doe, Jane/Cloud Notes/cnp.pdf" exists
    And the sidecar "library/Doe, Jane/Cloud Notes/book.json" has the title "Cloud Notes" and the authors "Jane Doe"
    And the table "works" of the index has 3 rows

  Scenario: The last file of a work takes the work along
    Given I have run shelf with arguments "move 01J9Z3K1 cnp.pdf --to 01J9Z3K2 --yes --library library"
    When I run shelf with arguments "move 01J9Z3K1 kp.epub --to 01J9Z3K2 --yes --library library"
    Then the exit code is 0
    And the directory "library/Ibryam, Bilgin/Kubernetes Patterns (2019)" does not exist
    And the table "works" of the index has 1 rows
    And the sidecar "library/Davis, Cornelia/Cloud Native Patterns (2019)/book.json" lists the files "cnp.epub, cnp.pdf, kp.epub"

  Scenario: A move can be undone
    Given I have run shelf with arguments "move 01J9Z3K1 cnp.pdf --to 01J9Z3K2 --yes --library library"
    When I run shelf with arguments "undo --library library"
    Then the exit code is 0
    And the sidecar "library/Ibryam, Bilgin/Kubernetes Patterns (2019)/book.json" lists the files "kp.epub, cnp.pdf"
    And the sidecar "library/Davis, Cornelia/Cloud Native Patterns (2019)/book.json" lists the files "cnp.epub"
    And the file "library/Ibryam, Bilgin/Kubernetes Patterns (2019)/cnp.pdf" exists

  Scenario: The index follows at once
    When I run shelf with arguments "move 01J9Z3K1 cnp.pdf --to 01J9Z3K2 --yes --library library"
    Then the file "cnp.pdf" in the index has the path "Davis, Cornelia/Cloud Native Patterns (2019)/cnp.pdf"

  Scenario Outline: Moving that cannot work is an error and nothing is changed
    When I run shelf with arguments "move <work> <file> <target> --yes --library library"
    Then the exit code is 1
    And standard error starts with "error: <message>"
    And the folder "library" equals the snapshot

    Examples:
      | work     | file        | target           | message                                              |
      | 01J9Z3K1 | missing.pdf | --to 01J9Z3K2    | 01J9Z3K1000000000000000001 has no file 'missing.pdf'  |
      | 01J9Z3K9 | cnp.pdf     | --to 01J9Z3K2    | no work matches '01J9Z3K9'                           |
      | 01J9Z3K1 | cnp.pdf     | --to 01J9Z3K9    | no work matches '01J9Z3K9'                           |
      | 01J9Z3K1 | cnp.pdf     | --to 01J9Z3K1    | cannot move a file into its own work                 |

  Scenario: A target is needed
    When I run shelf with arguments "move 01J9Z3K1 cnp.pdf --yes --library library"
    Then the exit code is 2
    And standard error contains "--to"
