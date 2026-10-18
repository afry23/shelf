@F-DUP-4 @M2
Feature: The trash
  As a user who is sure that the trashed files can go
  I want one command that deletes them for good
  So that nothing is ever deleted by accident

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

  Scenario: Deleting means moving to the trash
    Given the answers are "<enter>, <enter>, <enter>"
    When I run shelf with arguments "dupes --interactive --library library"
    Then the directory "library/.shelf/trash" exists
    And the file "library/Evans, Eric/Domain-Driven Design (2003)/ddd (1).epub" does not exist

  # Three duplicates and the sidecar of the work that was left without a file.
  Scenario: Emptying the trash is a dry run unless --yes is given
    Given the answers are "<enter>, <enter>, <enter>"
    And I have run shelf with arguments "dupes --interactive --library library"
    When I run shelf with arguments "trash empty --library library"
    Then the exit code is 0
    And standard output contains "Would delete 4 files"
    And standard output contains "--yes"
    When I run shelf with arguments "undo --library library"
    Then the exit code is 0
    And the file "library/Evans, Eric/Domain-Driven Design (2003)/ddd (1).epub" exists

  Scenario: With --yes the trash is deleted for good
    Given the answers are "<enter>, <enter>, <enter>"
    And I have run shelf with arguments "dupes --interactive --library library"
    When I run shelf with arguments "trash empty --yes --library library"
    Then the exit code is 0
    And standard output contains "Deleted 4 files"
    When I run shelf with arguments "trash empty --yes --library library"
    Then standard output is "Trash is empty."

  Scenario: Files deleted for good cannot be brought back by undo
    Given the answers are "<enter>, <enter>, <enter>"
    And I have run shelf with arguments "dupes --interactive --library library"
    And I have run shelf with arguments "trash empty --yes --library library"
    When I run shelf with arguments "undo --library library"
    Then the exit code is 1
    And standard error contains ": missing"

  Scenario: The result as JSON
    Given the answers are "<enter>, <enter>, <enter>"
    And I have run shelf with arguments "dupes --interactive --library library"
    When I run shelf with arguments "trash empty --yes --library library --json"
    Then standard output is a JSON object with the integer properties "files=4"

  Scenario: Without a trash there is nothing to empty
    When I run shelf with arguments "trash empty --library library"
    Then the exit code is 0
    And standard output is "Trash is empty."
