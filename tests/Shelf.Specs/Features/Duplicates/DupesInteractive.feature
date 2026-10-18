@F-DUP-4 @F-DUP-5 @F-DUP-6 @M2
Feature: Cleaning up duplicates interactively
  As a user who sees the duplicates
  I want to decide for each group which file stays
  So that the others go to the trash and nothing is lost for good

  Background:
    Given an empty working directory
    And I have run shelf with arguments "init library"
    And a sample EPUB "library/Evans, Eric/Domain-Driven Design (2003)/ddd.epub" with the title "Domain-Driven Design", the authors "Eric Evans" and the text seed 1
    And a sample EPUB "library/Evans, Eric/Domain-Driven Design (2003)/ddd (1).epub" with the title "Domain-Driven Design", the authors "Eric Evans" and the text seed 1
    And the sidecar "library/Evans, Eric/Domain-Driven Design (2003)/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K1000000000000000001", "title": "Domain-Driven Design", "authors": ["Eric Evans"], "year": 2003, "files": [{ "name": "ddd.epub" }, { "name": "ddd (1).epub" }] }
      """
    And a sample EPUB "library/Ibryam, Bilgin/Kubernetes Patterns (2019)/kp.epub" with the title "Kubernetes Patterns", the authors "Bilgin Ibryam" and the text seed 3
    And the sidecar "library/Ibryam, Bilgin/Kubernetes Patterns (2019)/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K2000000000000000002", "title": "Kubernetes Patterns", "authors": ["Bilgin Ibryam"], "year": 2019, "tags": ["devops"], "files": [{ "name": "kp.epub" }] }
      """
    And a sample EPUB "library/Imports/kp/kp-copy.epub" with the title "Kubernetes Patterns", the authors "Bilgin Ibryam" and the text seed 3
    And the sidecar "library/Imports/kp/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K3000000000000000003", "title": "Kubernetes Patterns (copy)", "authors": ["Bilgin Ibryam"], "files": [{ "name": "kp-copy.epub" }] }
      """
    And a sample EPUB "library/Davis, Cornelia/Cloud Native Patterns (2019)/a.epub" with the title "Cloud Native Patterns", the authors "Cornelia Davis" and the text seed 2
    And a sample EPUB "library/Davis, Cornelia/Cloud Native Patterns (2019)/b.epub" with the title "Cloud Native Patterns", the authors "Cornelia Davis", the ISBN "9781617294297" and the text seed 5
    And the sidecar "library/Davis, Cornelia/Cloud Native Patterns (2019)/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K4000000000000000004", "title": "Cloud Native Patterns", "authors": ["Cornelia Davis"], "year": 2019, "files": [{ "name": "a.epub" }, { "name": "b.epub" }] }
      """
    And I have run shelf with arguments "reindex --library library"
    And I take a snapshot of the folder "library"

  Scenario: Enter accepts the suggestion; the other files move to the trash
    Given the answers are "<enter>, <enter>, <enter>"
    When I run shelf with arguments "dupes --interactive --library library"
    Then the exit code is 0
    And standard output contains "Moved 3 files to the trash"
    And standard output contains "shelf undo"
    And the file "library/Davis, Cornelia/Cloud Native Patterns (2019)/a.epub" does not exist
    And the file "library/Davis, Cornelia/Cloud Native Patterns (2019)/b.epub" exists
    And the file "library/Evans, Eric/Domain-Driven Design (2003)/ddd (1).epub" does not exist
    And the file "library/Evans, Eric/Domain-Driven Design (2003)/ddd.epub" exists
    And the file "library/Imports/kp/kp-copy.epub" does not exist
    And the file "library/Ibryam, Bilgin/Kubernetes Patterns (2019)/kp.epub" exists
    And the directory "library/.shelf/trash" exists

  Scenario: The sidecars and the index follow
    Given the answers are "<enter>, <enter>, <enter>"
    When I run shelf with arguments "dupes --interactive --library library"
    Then the sidecar "library/Evans, Eric/Domain-Driven Design (2003)/book.json" lists the files "ddd.epub"
    And the sidecar "library/Davis, Cornelia/Cloud Native Patterns (2019)/book.json" lists the files "b.epub"
    And the table "files" of the index has 3 rows

  Scenario: A work that is left without files goes to the trash with its folder
    Given the answers are "<enter>, <enter>, <enter>"
    When I run shelf with arguments "dupes --interactive --library library"
    Then the table "works" of the index has 3 rows
    And the directory "library/Imports" does not exist
    And the file "library/Ibryam, Bilgin/Kubernetes Patterns (2019)/book.json" contains "devops"

  Scenario: A number chooses the file that stays
    Given the answers are "1, s, s"
    When I run shelf with arguments "dupes --interactive --library library"
    Then standard output contains "Moved 1 file to the trash"
    And the file "library/Davis, Cornelia/Cloud Native Patterns (2019)/a.epub" exists
    And the file "library/Davis, Cornelia/Cloud Native Patterns (2019)/b.epub" does not exist

  Scenario: Skipping everything changes nothing
    Given the answers are "s, s, s"
    When I run shelf with arguments "dupes --interactive --library library"
    Then the exit code is 0
    And standard output contains "Nothing moved to the trash"
    And the folder "library" equals the snapshot
    And the directory "library/.shelf/journal" does not exist

  Scenario: Quit stops after the decisions made so far
    Given the answers are "<enter>, q"
    When I run shelf with arguments "dupes --interactive --library library"
    Then standard output contains "Moved 1 file to the trash"
    And the file "library/Davis, Cornelia/Cloud Native Patterns (2019)/a.epub" does not exist
    And the file "library/Evans, Eric/Domain-Driven Design (2003)/ddd (1).epub" exists

  Scenario: Without more input shelf quits like with q
    Given the answers are "<enter>"
    When I run shelf with arguments "dupes --interactive --library library"
    Then the exit code is 0
    And standard output contains "Moved 1 file to the trash"

  Scenario: An answer that makes no sense is asked again
    Given the answers are "x, 9, s, s, s"
    When I run shelf with arguments "dupes --interactive --library library"
    Then the exit code is 0
    And standard output contains "Please answer 1-2, Enter, s, n or q"
    And standard output contains "Nothing moved to the trash"

  Scenario: A pair marked as not a duplicate within a work is not suggested again (F-DUP-6)
    Given the answers are "s, n, s"
    And I have run shelf with arguments "dupes --interactive --library library"
    When I run shelf with arguments "dupes --library library"
    Then standard output contains "2 groups of duplicates"
    And standard output does not contain "ddd (1).epub"
    And the file "library/Evans, Eric/Domain-Driven Design (2003)/book.json" contains "notDuplicates"
    And the file "library/Evans, Eric/Domain-Driven Design (2003)/ddd (1).epub" exists

  Scenario: A pair marked as not a duplicate across works is stored on both sides (F-DUP-6)
    Given the answers are "s, s, n"
    And I have run shelf with arguments "dupes --interactive --library library"
    When I run shelf with arguments "dupes --library library"
    Then standard output does not contain "kp-copy.epub"
    And the file "library/Ibryam, Bilgin/Kubernetes Patterns (2019)/book.json" contains "01J9Z3K3000000000000000003"
    And the file "library/Imports/kp/book.json" contains "01J9Z3K2000000000000000002"

  Scenario: The whole session can be undone
    Given the answers are "<enter>, <enter>, <enter>"
    And I have run shelf with arguments "dupes --interactive --library library"
    When I run shelf with arguments "undo --library library"
    Then the exit code is 0
    And standard output contains "Reverted the last dupes run"
    And the folder "library" equals the snapshot
    And the table "works" of the index has 4 rows
    And the table "files" of the index has 6 rows

  Scenario: Interactive and JSON do not go together
    When I run shelf with arguments "dupes --interactive --json --library library"
    Then the exit code is 2
    And standard error contains "error: --interactive cannot be combined with --json"

  Scenario: A library without duplicates is not asked about anything
    Given an empty working directory
    And I have run shelf with arguments "init other"
    When I run shelf with arguments "dupes --interactive --library other"
    Then standard output is "No duplicates."
