@F-GRP-4 @M1
Feature: Merging two works by hand
  As a user whose library holds one book as two works
  I want to merge them with shelf merge <work-a> <work-b>
  So that all formats of the book are in one work in one folder

  Background:
    Given an empty working directory
    And I have run shelf with arguments "init library"
    And a sample EPUB "library/Ibryam, Bilgin/Kubernetes Patterns (2019)/kp.epub" with the title "Kubernetes Patterns", the authors "Bilgin Ibryam" and the text seed 1
    And the sidecar "library/Ibryam, Bilgin/Kubernetes Patterns (2019)/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K1000000000000000001", "title": "Kubernetes Patterns", "authors": ["Bilgin Ibryam"], "year": 2019, "tags": ["devops"], "readStatus": "unread", "files": [{ "name": "kp.epub" }] }
      """
    And a sample PDF "library/Ibryam, Bilgin/Kubernetes Patterns/kp.pdf" with the title "Kubernetes Patterns", the author "B. Ibryam" and the text seed 1
    And the sidecar "library/Ibryam, Bilgin/Kubernetes Patterns/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K3000000000000000003", "title": "Kubernetes Patterns", "subtitle": "Reusable Elements", "authors": ["B. Ibryam"], "language": "en", "isbn13": "9781492050285", "tags": ["books", "devops"], "rating": 4, "readStatus": "read", "files": [{ "name": "kp.pdf", "progress": { "locator": "page=42", "updatedAt": "2026-10-12T19:04:00Z" } }] }
      """
    And a sample PDF "library/Ålander, Jördis/Event Sourcing in Practice/es.pdf" with the title "Event Sourcing in Practice", the author "Jördis Ålander" and the text seed 2
    And the sidecar "library/Ålander, Jördis/Event Sourcing in Practice/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K2000000000000000002", "title": "Event Sourcing in Practice", "authors": ["Jördis Ålander"], "files": [{ "name": "es.pdf" }] }
      """
    And I have run shelf with arguments "reindex --library library"

  Scenario: The files of the second work move into the folder of the first
    When I run shelf with arguments "merge 01J9Z3K1 01J9Z3K3 --library library"
    Then the exit code is 0
    And standard output is "Merged 01J9Z3K3000000000000000003 into 01J9Z3K1000000000000000001: 1 file moved"
    And the file "library/Ibryam, Bilgin/Kubernetes Patterns (2019)/kp.epub" exists
    And the file "library/Ibryam, Bilgin/Kubernetes Patterns (2019)/kp.pdf" exists
    And the file "library/Ibryam, Bilgin/Kubernetes Patterns/kp.pdf" does not exist
    And the file "library/Ibryam, Bilgin/Kubernetes Patterns/book.json" does not exist
    And the directory "library/Ibryam, Bilgin/Kubernetes Patterns" does not exist
    And the sidecar "library/Ibryam, Bilgin/Kubernetes Patterns (2019)/book.json" lists the files "kp.epub, kp.pdf"
    And the file "library/Ålander, Jördis/Event Sourcing in Practice/es.pdf" exists

  Scenario: The index follows at once
    When I run shelf with arguments "merge 01J9Z3K1 01J9Z3K3 --library library"
    Then the table "works" of the index has 2 rows
    And the table "files" of the index has 3 rows
    And the file "kp.pdf" in the index has the path "Ibryam, Bilgin/Kubernetes Patterns (2019)/kp.pdf"
    And the file "kp.pdf" in the index has a hash and a fingerprint

  Scenario: The first work keeps its own metadata and gets what it lacked
    Given I have run shelf with arguments "merge 01J9Z3K1 01J9Z3K3 --library library"
    When I run shelf with arguments "show 01J9Z3K1 --library library --json"
    Then the JSON of standard output has the string "title" equal to "Kubernetes Patterns"
    And the JSON of standard output has the string "authors[0]" equal to "Bilgin Ibryam"
    And the JSON of standard output has the number "year" equal to 2019
    And the JSON of standard output has the string "subtitle" equal to "Reusable Elements"
    And the JSON of standard output has the string "language" equal to "en"
    And the JSON of standard output has the string "isbn13" equal to "9781492050285"
    And the JSON of standard output has the number "rating" equal to 4
    And the JSON of standard output has the string "readStatus" equal to "read"
    And the JSON of standard output has the string "tags[0]" equal to "books"
    And the JSON of standard output has the string "tags[1]" equal to "devops"
    And the JSON property "tags" of standard output has 2 items
    And the JSON of standard output has the string "files[1].progress.locator" equal to "page=42"

  Scenario: The work that was merged away is gone
    Given I have run shelf with arguments "merge 01J9Z3K1 01J9Z3K3 --library library"
    When I run shelf with arguments "show 01J9Z3K3 --library library"
    Then the exit code is 1
    And standard error starts with "error: no work matches '01J9Z3K3'"

  Scenario: A file name that is taken is changed, nothing is overwritten
    Given a sample PDF "library/Ibryam, Bilgin/Kubernetes Patterns (2019)/kp.pdf" with the title "Kubernetes Patterns", the author "Bilgin Ibryam" and the text seed 7
    And the sidecar "library/Ibryam, Bilgin/Kubernetes Patterns (2019)/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K1000000000000000001", "title": "Kubernetes Patterns", "authors": ["Bilgin Ibryam"], "files": [{ "name": "kp.epub" }, { "name": "kp.pdf" }] }
      """
    And I have run shelf with arguments "reindex --library library"
    When I run shelf with arguments "merge 01J9Z3K1 01J9Z3K3 --library library"
    Then the exit code is 0
    And the file "library/Ibryam, Bilgin/Kubernetes Patterns (2019)/kp.pdf" exists
    And the file "library/Ibryam, Bilgin/Kubernetes Patterns (2019)/kp (1).pdf" exists
    And the sidecar "library/Ibryam, Bilgin/Kubernetes Patterns (2019)/book.json" lists the files "kp.epub, kp.pdf, kp (1).pdf"

  Scenario: The works can be named by their folders
    When I run shelf with arguments "merge 'library/Ibryam, Bilgin/Kubernetes Patterns (2019)' 'library/Ibryam, Bilgin/Kubernetes Patterns' --library library"
    Then the exit code is 0
    And the sidecar "library/Ibryam, Bilgin/Kubernetes Patterns (2019)/book.json" lists the files "kp.epub, kp.pdf"

  Scenario: Review entries about the merged works are settled
    Given the review queue of "library" gets a grouping entry for the work "01J9Z3K3000000000000000003" about "Ibryam, Bilgin/Kubernetes Patterns/kp.pdf" with the candidate "01J9Z3K1000000000000000001" at 0.6 because "same title and author"
    And the review queue of "library" gets a grouping entry for the work "01J9Z3K3000000000000000003" about "Ibryam, Bilgin/Kubernetes Patterns/kp.pdf" with the candidate "01J9Z3K2000000000000000002" at 0.55 because "same year"
    When I run shelf with arguments "merge 01J9Z3K1 01J9Z3K3 --library library"
    Then the exit code is 0
    And the review queue of "library" has 1 entry
    And entry 1 of "library" has the candidate "01J9Z3K2000000000000000002" at 0.55 because "same year"
    And the review entry 1 of "library" is about the work "01J9Z3K1000000000000000001" and the file "Ibryam, Bilgin/Kubernetes Patterns (2019)/kp.pdf"

  Scenario: The JSON form names both works and the moved files
    When I run shelf with arguments "merge 01J9Z3K1 01J9Z3K3 --library library --json"
    Then the exit code is 0
    And the JSON of standard output has the string "id" equal to "01J9Z3K1000000000000000001"
    And the JSON of standard output has the string "merged" equal to "01J9Z3K3000000000000000003"
    And the JSON of standard output has the number "movedFiles" equal to 1

  Scenario Outline: Merging that cannot work is an error and nothing is changed
    When I run shelf with arguments "merge <a> <b> --library library"
    Then the exit code is 1
    And standard error starts with "error: <message>"
    And the table "works" of the index has 3 rows
    And the file "library/Ibryam, Bilgin/Kubernetes Patterns/kp.pdf" exists

    Examples:
      | a                          | b                                              | message                                            |
      | 01J9Z3K1                   | 01J9Z3K1000000000000000001                     | cannot merge a work into itself                    |
      | 01J9Z3K1                   | 'library/Ibryam, Bilgin/Kubernetes Patterns (2019)/kp.epub' | cannot merge a work into itself    |
      | 01J9Z3K1                   | 01J9Z3K9                                       | no work matches '01J9Z3K9'                         |
      | 01J9Z3K9                   | 01J9Z3K3                                       | no work matches '01J9Z3K9'                         |

  Scenario: Both works are needed
    When I run shelf with arguments "merge 01J9Z3K1 --library library"
    Then the exit code is 2
    And standard error contains "Required argument missing"
