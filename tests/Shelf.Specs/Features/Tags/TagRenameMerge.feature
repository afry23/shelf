@F-TAG-4 @M2
Feature: Renaming and merging tags library-wide
  As a user whose tags grew untidy
  I want to rename a tag everywhere and to merge two tags into one
  So that the tags stay consistent without editing every book

  Background:
    Given an empty working directory
    And I have run shelf with arguments "init library"
    And a sample EPUB "library/Evans, Eric/Domain-Driven Design (2003)/ddd.epub" with the title "Domain-Driven Design", the authors "Eric Evans" and the text seed 1
    And the sidecar "library/Evans, Eric/Domain-Driven Design (2003)/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K1000000000000000001", "title": "Domain-Driven Design", "authors": ["Eric Evans"], "year": 2003, "tags": ["architektur/ddd"], "files": [{ "name": "ddd.epub" }] }
      """
    And a sample EPUB "library/Davis, Cornelia/Cloud Native Patterns (2019)/cnp.epub" with the title "Cloud Native Patterns", the authors "Cornelia Davis" and the text seed 2
    And the sidecar "library/Davis, Cornelia/Cloud Native Patterns (2019)/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K2000000000000000002", "title": "Cloud Native Patterns", "authors": ["Cornelia Davis"], "year": 2019, "tags": ["cloud/azure", "devops"], "files": [{ "name": "cnp.epub" }] }
      """
    And a sample EPUB "library/Ibryam, Bilgin/Kubernetes Patterns (2019)/kp.epub" with the title "Kubernetes Patterns", the authors "Bilgin Ibryam" and the text seed 3
    And the sidecar "library/Ibryam, Bilgin/Kubernetes Patterns (2019)/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K3000000000000000003", "title": "Kubernetes Patterns", "authors": ["Bilgin Ibryam"], "year": 2019, "tags": ["devops"], "files": [{ "name": "kp.epub" }] }
      """
    And I have run shelf with arguments "reindex --library library"

  Scenario: Renaming a tag changes it on every work
    When I run shelf with arguments "tag rename devops operations --library library"
    Then the exit code is 0
    And standard output is "Renamed 'devops' to 'operations' in 2 works"
    And the work "01J9Z3K2000000000000000002" in the index has the tags "cloud/azure, operations"
    And the work "01J9Z3K3000000000000000003" in the index has the tags "operations"

  Scenario: Renaming a tag renames the tags below it too
    When I run shelf with arguments "tag rename cloud infrastruktur --library library"
    Then standard output is "Renamed 'cloud' to 'infrastruktur' in 1 work"
    And the work "01J9Z3K2000000000000000002" in the index has the tags "devops, infrastruktur/azure"

  Scenario: A tag that only shares the start of the name is not touched
    Given I have run shelf with arguments "tag add cloudy 01J9Z3K1 --library library"
    When I run shelf with arguments "tag rename cloud infrastruktur --library library"
    Then the work "01J9Z3K1000000000000000001" in the index has the tags "architektur/ddd, cloudy"

  Scenario: Rename refuses a name that is in use and points to merge
    When I run shelf with arguments "tag rename cloud/azure devops --library library"
    Then the exit code is 1
    And standard error contains "error: tag 'devops' already exists; use tag merge"
    And the work "01J9Z3K2000000000000000002" in the index has the tags "cloud/azure, devops"

  Scenario Outline: A tag that no work has is an error
    When I run shelf with arguments "tag <command> nope devops --library library"
    Then the exit code is 1
    And standard error contains "error: no work has the tag 'nope'"

    Examples:
      | command |
      | rename  |
      | merge   |

  Scenario Outline: The new name must differ from the old one
    When I run shelf with arguments "tag <command> devops devops --library library"
    Then the exit code is 2
    And standard error contains "error: the new name is the old one"

    Examples:
      | command |
      | rename  |
      | merge   |

  Scenario: Merging puts two tags into one, also on a work that has both
    When I run shelf with arguments "tag merge cloud/azure devops --library library"
    Then the exit code is 0
    And standard output is "Merged 'cloud/azure' into 'devops' in 1 work"
    And the work "01J9Z3K2000000000000000002" in the index has the tags "devops"
    And the work "01J9Z3K3000000000000000003" in the index has the tags "devops"

  Scenario: Merging a tag into a new place moves the tags below it along
    When I run shelf with arguments "tag merge cloud platform --library library"
    Then the work "01J9Z3K2000000000000000002" in the index has the tags "devops, platform/azure"

  Scenario: The result as JSON
    When I run shelf with arguments "tag rename devops operations --library library --json"
    Then standard output is a JSON object with the integer properties "changed=2"

  Scenario: A rename can be undone
    Given I have run shelf with arguments "tag rename devops operations --library library"
    When I run shelf with arguments "undo --library library"
    Then standard output contains "Reverted the last tag rename run"
    And the work "01J9Z3K2000000000000000002" in the index has the tags "cloud/azure, devops"
    And the work "01J9Z3K3000000000000000003" in the index has the tags "devops"

  Scenario: A rename keeps the tags after reindex
    Given I have run shelf with arguments "tag rename devops operations --library library"
    And I delete the file "library/.shelf/index.db"
    When I run shelf with arguments "reindex --library library"
    Then the work "01J9Z3K3000000000000000003" in the index has the tags "operations"
