@F-TAG-1 @F-TAG-2 @F-TAG-5 @M2
Feature: Tags on works and the tag overview
  As a user with a growing library
  I want free, hierarchical tags and an overview of them
  So that I can sort my books by topic

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

  Scenario: The overview shows each tag with the number of its works
    When I run shelf with arguments "tag list --library library"
    Then the exit code is 0
    And standard output matches the pattern "(?m)^\s*1\s+architektur/ddd$"
    And standard output matches the pattern "(?m)^\s*1\s+cloud/azure$"
    And standard output matches the pattern "(?m)^\s*2\s+devops$"

  Scenario: The overview as JSON
    When I run shelf with arguments "tag list --library library --json"
    Then the exit code is 0
    And the JSON property "tags" of standard output has 3 items

  Scenario: A library without tags says so
    Given an empty working directory
    And I have run shelf with arguments "init other"
    When I run shelf with arguments "tag list --library other"
    Then the exit code is 0
    And standard output is "No tags."

  Scenario: A tag is added to a work and mirrored in the index
    When I run shelf with arguments "tag add reading 01J9Z3K1 --library library"
    Then the exit code is 0
    And standard output is "Added 'reading' to 1 work"
    And the work "01J9Z3K1000000000000000001" in the index has the tags "architektur/ddd, reading"
    And the file "library/Evans, Eric/Domain-Driven Design (2003)/book.json" contains "reading"

  Scenario: A hierarchical tag is accepted
    When I run shelf with arguments "tag add architektur/verteilte-systeme 01J9Z3K1 --library library"
    Then the work "01J9Z3K1000000000000000001" in the index has the tags "architektur/ddd, architektur/verteilte-systeme"

  Scenario: Surrounding blanks are removed from every part of the tag
    When I run shelf with arguments "tag add ' architektur / neu ' 01J9Z3K1 --library library"
    Then the work "01J9Z3K1000000000000000001" in the index has the tags "architektur/ddd, architektur/neu"

  Scenario: Adding a tag that a work has changes nothing and says so
    When I run shelf with arguments "tag add devops 01J9Z3K2 --library library"
    Then the exit code is 0
    And standard output is "Added 'devops' to 0 works (1 already had it)"

  Scenario Outline: A tag with an empty part is a usage error
    When I run shelf with arguments "tag add '<tag>' 01J9Z3K1 --library library"
    Then the exit code is 2
    And standard error contains "error: tag '<tag>': has an empty part"

    Examples:
      | tag    |
      | a//b   |
      | /a     |
      | a/     |

  Scenario: A tag is removed from a work
    When I run shelf with arguments "tag rm devops 01J9Z3K2 --library library"
    Then the exit code is 0
    And standard output is "Removed 'devops' from 1 work"
    And the work "01J9Z3K2000000000000000002" in the index has the tags "cloud/azure"
    And the work "01J9Z3K3000000000000000003" in the index has the tags "devops"

  Scenario: Removing a tag removes only that tag, not the ones below it
    Given I have run shelf with arguments "tag add cloud 01J9Z3K2 --library library"
    When I run shelf with arguments "tag rm cloud 01J9Z3K2 --library library"
    Then the work "01J9Z3K2000000000000000002" in the index has the tags "cloud/azure, devops"

  Scenario Outline: Add and remove need something to act on
    When I run shelf with arguments "tag <command> devops --library library"
    Then the exit code is 2
    And standard error contains "error: give at least one work or --query"

    Examples:
      | command |
      | add     |
      | rm      |

  Scenario: The tags in the sidecars are the truth: reindex brings them back
    Given I have run shelf with arguments "tag add reading 01J9Z3K1 --library library"
    And I delete the file "library/.shelf/index.db"
    When I run shelf with arguments "reindex --library library"
    Then the work "01J9Z3K1000000000000000001" in the index has the tags "architektur/ddd, reading"
