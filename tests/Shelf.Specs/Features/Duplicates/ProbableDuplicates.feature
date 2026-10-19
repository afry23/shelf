@F-DUP-3 @F-DUP-6 @F-GRP-6 @M5
Feature: Finding probable duplicates across works
  As a user whose library holds the same book under two titles
  I want shelf dupes --probable to show works with (nearly) the same text
  So that I can check by hand whether they are one book, and say so when they are not

  Background:
    Given an empty working directory
    And I have run shelf with arguments "init library"
    And a sample EPUB "library/Evans, Eric/Domain-Driven Design (2003)/ddd.epub" with the title "Domain-Driven Design", the authors "Eric Evans" and the text seed 1
    And the sidecar "library/Evans, Eric/Domain-Driven Design (2003)/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K1000000000000000001", "title": "Domain-Driven Design", "authors": ["Eric Evans"], "year": 2003, "files": [{ "name": "ddd.epub" }] }
      """
    And a sample EPUB "library/Avram, Abel/DDD Quickly/ddd-quickly.epub" with the title "DDD Quickly", the authors "Abel Avram" and the text seed 1
    And the sidecar "library/Avram, Abel/DDD Quickly/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K2000000000000000002", "title": "DDD Quickly", "authors": ["Abel Avram"], "files": [{ "name": "ddd-quickly.epub" }] }
      """
    And a sample EPUB "library/Ibryam, Bilgin/Kubernetes Patterns (2019)/kp.epub" with the title "Kubernetes Patterns", the authors "Bilgin Ibryam" and the text seed 3
    And the sidecar "library/Ibryam, Bilgin/Kubernetes Patterns (2019)/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K3000000000000000003", "title": "Kubernetes Patterns", "authors": ["Bilgin Ibryam"], "year": 2019, "files": [{ "name": "kp.epub" }] }
      """
    And a sample EPUB "library/Davis, Cornelia/Cloud Native Patterns (2019)/cnp.epub" with the title "Cloud Native Patterns", the authors "Cornelia Davis" and the text seed 2
    And the sidecar "library/Davis, Cornelia/Cloud Native Patterns (2019)/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K4000000000000000004", "title": "Cloud Native Patterns", "authors": ["Cornelia Davis"], "year": 2019, "files": [{ "name": "cnp.epub" }] }
      """
    And I have run shelf with arguments "reindex --library library"

  Scenario: Two works with the same text are probable duplicates
    When I run shelf with arguments "dupes --probable --library library"
    Then the exit code is 0
    And standard output contains "Domain-Driven Design"
    And standard output contains "DDD Quickly"
    And standard output contains "text distance 0"
    And standard output contains "1 pair of probable duplicates"
    And standard output does not contain "Kubernetes Patterns"
    And standard output does not contain "Cloud Native Patterns"

  Scenario: The plain duplicates list does not show them
    When I run shelf with arguments "dupes --library library"
    Then standard output is "No duplicates."

  Scenario: Probable duplicates are for review only: nothing is moved
    When I run shelf with arguments "dupes --probable --library library"
    Then the file "library/Evans, Eric/Domain-Driven Design (2003)/ddd.epub" exists
    And the file "library/Avram, Abel/DDD Quickly/ddd-quickly.epub" exists

  Scenario: Works with identical files are listed as identical duplicates only
    Given a sample EPUB "library/Imports/kp/kp-copy.epub" with the title "Kubernetes Patterns", the authors "Bilgin Ibryam" and the text seed 3
    And the sidecar "library/Imports/kp/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K5000000000000000005", "title": "Kubernetes Patterns (copy)", "authors": ["Bilgin Ibryam"], "files": [{ "name": "kp-copy.epub" }] }
      """
    And I have run shelf with arguments "reindex --library library"
    When I run shelf with arguments "dupes --probable --library library"
    Then standard output does not contain "Kubernetes Patterns"
    And standard output contains "1 pair of probable duplicates"

  Scenario: Nothing to show
    Given I delete the folder "library/Avram, Abel"
    And I have run shelf with arguments "reindex --library library"
    When I run shelf with arguments "dupes --probable --library library"
    Then standard output is "No probable duplicates."

  # ---- F-GRP-6: the distance is a setting ----

  Scenario Outline: A second edition with some words changed is found up to the configured distance
    Given a sample EPUB "library/Evans, Eric/Domain-Driven Design, second edition/ddd2.epub" with the title "Domain-Driven Design, second edition", the authors "Eric Evans" and the text seed 1 with <changed> words changed
    And the sidecar "library/Evans, Eric/Domain-Driven Design, second edition/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K6000000000000000006", "title": "Domain-Driven Design, second edition", "authors": ["Eric Evans"], "year": 2015, "files": [{ "name": "ddd2.epub" }] }
      """
    And the global config file contains the line "grouping.probable_hamming = <setting>"
    And I have run shelf with arguments "reindex --library library"
    When I run shelf with arguments "dupes --probable --library library"
    Then standard output <shown> "Domain-Driven Design, second edition"

    Examples:
      | changed | setting | shown            | what                                                          |
      | 10      | 3       | contains         | 1% of the words changed: distance 3, the default allows it    |
      | 10      | 2       | does not contain | the same text, one step above the setting                     |
      | 50      | 3       | does not contain | 5% changed: distance 10, another book by default              |
      | 50      | 10      | contains         | the setting can be widened                                    |
      | 200     | 64      | contains         | every text is close at 64                                     |

  # ---- machine-readable ----

  Scenario: The JSON output names both works, the distance and the way they were found
    When I run shelf with arguments "dupes --probable --json --library library"
    Then the exit code is 0
    And the JSON property "pairs" of standard output has 1 items
    And standard output matches the pattern "hamming.:0"
    And standard output matches the pattern "title.:.DDD Quickly"
    And standard output matches the pattern "title.:.Domain-Driven Design"

  # ---- F-DUP-6: "not a duplicate" ----

  Scenario: A pair that is marked as not a duplicate is not suggested again
    Given the answers are "n"
    When I run shelf with arguments "dupes --probable --interactive --library library"
    Then the exit code is 0
    And standard output contains "Marked as not duplicates; they are not suggested again."
    And the file "library/Evans, Eric/Domain-Driven Design (2003)/book.json" contains "01J9Z3K2000000000000000002"
    And the file "library/Avram, Abel/DDD Quickly/book.json" contains "01J9Z3K1000000000000000001"
    When I run shelf with arguments "dupes --probable --library library"
    Then standard output is "No probable duplicates."

  Scenario: The marking survives a rebuild of the index
    Given the answers are "n"
    And I have run shelf with arguments "dupes --probable --interactive --library library"
    And I have run shelf with arguments "reindex --library library"
    When I run shelf with arguments "dupes --probable --library library"
    Then standard output is "No probable duplicates."

  Scenario: Skipping a pair changes nothing
    Given the answers are "s"
    When I run shelf with arguments "dupes --probable --interactive --library library"
    Then standard output contains "Nothing marked."
    When I run shelf with arguments "dupes --probable --library library"
    Then standard output contains "1 pair of probable duplicates"

  Scenario: Only n, s and q are offered: files are never removed on a guess
    Given the answers are "1, q"
    When I run shelf with arguments "dupes --probable --interactive --library library"
    Then standard output contains "Please answer n, s or q."
    And the file "library/Evans, Eric/Domain-Driven Design (2003)/ddd.epub" exists
    And the file "library/Avram, Abel/DDD Quickly/ddd-quickly.epub" exists

  Scenario: Interactive mode and JSON do not go together
    When I run shelf with arguments "dupes --probable --interactive --json --library library"
    Then the exit code is 2
    And standard error contains "error: --interactive cannot be combined with --json"
