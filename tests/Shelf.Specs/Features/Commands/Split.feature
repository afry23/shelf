@F-GRP-4 @M1
Feature: Splitting a file off a work
  As a user whose library put two different books into one work
  I want to split a file off with shelf split <work> <file>
  So that the file becomes a work of its own and is not grouped back

  Background:
    Given an empty working directory
    And I have run shelf with arguments "init library"
    And a sample EPUB "library/Ibryam, Bilgin/Kubernetes Patterns (2019)/kp.epub" with the title "Kubernetes Patterns", the authors "Bilgin Ibryam" and the text seed 1
    And a sample PDF "library/Ibryam, Bilgin/Kubernetes Patterns (2019)/companion.pdf" with the title "Kubernetes Companion", the author "Roland Huss" and the text seed 5
    And the sidecar "library/Ibryam, Bilgin/Kubernetes Patterns (2019)/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K1000000000000000001", "title": "Kubernetes Patterns", "authors": ["Bilgin Ibryam"], "year": 2019, "tags": ["devops"], "files": [{ "name": "kp.epub" }, { "name": "companion.pdf", "progress": { "locator": "page=42", "updatedAt": "2026-10-12T19:04:00Z" } }] }
      """
    And I have run shelf with arguments "reindex --library library"

  Scenario: The file becomes a work in a folder named after its own metadata
    When I run shelf with arguments "split 01J9Z3K1 companion.pdf --library library"
    Then the exit code is 0
    And standard output matches the pattern "^Split companion\.pdf off 01J9Z3K1000000000000000001 into 01[0-9A-HJKMNP-TV-Z]{24}$"
    And the file "library/Huss, Roland/Kubernetes Companion/companion.pdf" exists
    And the sidecar "library/Huss, Roland/Kubernetes Companion/book.json" has the title "Kubernetes Companion" and the authors "Roland Huss"
    And the sidecar "library/Huss, Roland/Kubernetes Companion/book.json" lists the files "companion.pdf"
    And the file "library/Ibryam, Bilgin/Kubernetes Patterns (2019)/companion.pdf" does not exist
    And the sidecar "library/Ibryam, Bilgin/Kubernetes Patterns (2019)/book.json" lists the files "kp.epub"

  Scenario: The reading position goes with the file, the tags stay
    Given I have run shelf with arguments "split 01J9Z3K1 companion.pdf --library library"
    Then the file "library/Huss, Roland/Kubernetes Companion/book.json" contains "page=42"
    And the file "library/Ibryam, Bilgin/Kubernetes Patterns (2019)/book.json" contains "devops"
    And the file "library/Ibryam, Bilgin/Kubernetes Patterns (2019)/book.json" does not contain "page=42"

  Scenario: The index follows at once and both works are searchable
    When I run shelf with arguments "split 01J9Z3K1 companion.pdf --library library"
    Then the table "works" of the index has 2 rows
    And the table "files" of the index has 2 rows
    And the file "companion.pdf" in the index has the path "Huss, Roland/Kubernetes Companion/companion.pdf"
    And the full-text index finds the work "01J9Z3K1000000000000000001" for the query "patterns"

  Scenario: The two works are marked as not belonging together
    When I run shelf with arguments "split 01J9Z3K1 companion.pdf --library library --json"
    Then the exit code is 0
    And the JSON of standard output has the string "id" equal to "01J9Z3K1000000000000000001"
    And the JSON of standard output has the string "file" equal to "companion.pdf"
    And the JSON of standard output has the string "folder" equal to "Huss, Roland/Kubernetes Companion"
    And the sidecar "library/Ibryam, Bilgin/Kubernetes Patterns (2019)/book.json" read back marks 1 work as not duplicates
    And the sidecar "library/Huss, Roland/Kubernetes Companion/book.json" read back marks 1 work as not duplicates

  Scenario: The file can be named by its path
    When I run shelf with arguments "split 01J9Z3K1 'library/Ibryam, Bilgin/Kubernetes Patterns (2019)/companion.pdf' --library library"
    Then the exit code is 0
    And the file "library/Huss, Roland/Kubernetes Companion/companion.pdf" exists

  Scenario: A taken folder name gets a number
    Given a sample PDF "library/Huss, Roland/Kubernetes Companion/other.pdf" with the title "Kubernetes Companion", the author "Roland Huss" and the text seed 9
    And the sidecar "library/Huss, Roland/Kubernetes Companion/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K4000000000000000004", "title": "Kubernetes Companion", "authors": ["Roland Huss"], "files": [{ "name": "other.pdf" }] }
      """
    And I have run shelf with arguments "reindex --library library"
    When I run shelf with arguments "split 01J9Z3K1 companion.pdf --library library"
    Then the exit code is 0
    And the file "library/Huss, Roland/Kubernetes Companion (2)/companion.pdf" exists
    And the file "library/Huss, Roland/Kubernetes Companion/other.pdf" exists

  Scenario Outline: Splitting that cannot work is an error and nothing is changed
    When I run shelf with arguments "split <work> <file> --library library"
    Then the exit code is 1
    And standard error starts with "error: <message>"
    And the table "works" of the index has 1 rows
    And the sidecar "library/Ibryam, Bilgin/Kubernetes Patterns (2019)/book.json" lists the files "kp.epub, companion.pdf"

    Examples:
      | work       | file          | message                                                              |
      | 01J9Z3K1   | missing.pdf   | 01J9Z3K1000000000000000001 has no file 'missing.pdf'                  |
      | 01J9Z3K9   | kp.epub       | no work matches '01J9Z3K9'                                           |

  Scenario: A work with one file cannot give it away
    Given I have run shelf with arguments "split 01J9Z3K1 companion.pdf --library library"
    When I run shelf with arguments "split 01J9Z3K1 kp.epub --library library"
    Then the exit code is 1
    And standard error starts with "error: 01J9Z3K1000000000000000001 has only one file"
    And the file "library/Ibryam, Bilgin/Kubernetes Patterns (2019)/kp.epub" exists

  Scenario: Both arguments are needed
    When I run shelf with arguments "split 01J9Z3K1 --library library"
    Then the exit code is 2
    And standard error contains "Required argument missing"
