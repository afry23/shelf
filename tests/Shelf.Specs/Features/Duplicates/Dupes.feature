@F-DUP-1 @F-DUP-2 @M2
Feature: Finding duplicates
  As a user whose library collected copies over the years
  I want shelf dupes to list identical files and different files of the same book in the same format
  So that I can see what could go

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

  Scenario: Identical files and different files of one work in one format are listed
    When I run shelf with arguments "dupes --library library"
    Then the exit code is 0
    And standard output contains "identical files"
    And standard output contains "same work, same format"
    And standard output contains "3 groups of duplicates (3 files could go to the trash)"
    And standard output contains "--interactive"

  Scenario: Identical files are suggested for removal, all but one (F-DUP-1)
    When I run shelf with arguments "dupes --library library"
    Then standard output matches the pattern "(?m)^  \* Evans, Eric/Domain-Driven Design \(2003\)/ddd\.epub"
    And standard output matches the pattern "(?m)^    Evans, Eric/Domain-Driven Design \(2003\)/ddd \(1\)\.epub"

  Scenario: Identical files in two different works are found too
    When I run shelf with arguments "dupes --library library"
    Then standard output matches the pattern "(?m)^  \* Ibryam, Bilgin/Kubernetes Patterns \(2019\)/kp\.epub"
    And standard output matches the pattern "(?m)^    Imports/kp/kp-copy\.epub"

  Scenario: Two files of one work in one format are compared by size, pages and metadata (F-DUP-2)
    When I run shelf with arguments "dupes --library library"
    Then standard output matches the pattern "(?m)^    Davis, Cornelia/Cloud Native Patterns \(2019\)/a\.epub  .* [KM]?B  metadata [0-9]+/[0-9]+$"
    And standard output matches the pattern "(?m)^  \* Davis, Cornelia/Cloud Native Patterns \(2019\)/b\.epub  .* [KM]?B  metadata [0-9]+/[0-9]+$"

  Scenario: The file with the more complete metadata is the suggestion
    When I run shelf with arguments "dupes --library library --json"
    Then the exit code is 0
    And the JSON property "groups" of standard output has 3 items

  Scenario: Listing changes nothing
    When I run shelf with arguments "dupes --library library"
    Then the file "library/Evans, Eric/Domain-Driven Design (2003)/ddd (1).epub" exists
    And the file "library/Imports/kp/kp-copy.epub" exists
    And the directory "library/.shelf/journal" does not exist
    And the directory "library/.shelf/trash" does not exist

  Scenario: A library without duplicates says so
    Given an empty working directory
    And I have run shelf with arguments "init other"
    When I run shelf with arguments "dupes --library other"
    Then the exit code is 0
    And standard output is "No duplicates."
