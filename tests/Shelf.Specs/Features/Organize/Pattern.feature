@F-ORG-1 @F-ORG-2 @F-ORG-3 @F-ORG-4 @M2
Feature: Folder and file names from the pattern
  As a user
  I want the default pattern to give readable, platform-safe names
  So that the library looks the same on every system

  Background:
    Given an empty working directory
    And I have run shelf with arguments "init library"
    And a sample EPUB "library/inbox/book.epub" with the title "Any title", the authors "Any Author" and the text seed 1

  Scenario Outline: The default pattern builds Author / Title (Year) / Title.ext
    Given the sidecar "library/inbox/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K1000000000000000001", "title": "<title>", "authors": <authors>, <year>"files": [{ "name": "book.epub" }] }
      """
    And I have run shelf with arguments "reindex --library library"
    When I run shelf with arguments "organize --apply --library library"
    Then the exit code is 0
    And the file "library/<path>" exists

    Examples:
      | title                | authors                                      | year          | path                                                             |
      | Refactoring          | ["Martin Fowler"]                            | "year": 1999, | Fowler, Martin/Refactoring (1999)/Refactoring.epub               |
      | Refactoring          | ["Martin Fowler"]                            |               | Fowler, Martin/Refactoring/Refactoring.epub                      |
      | Flatland: A Romance  | ["Edwin Abbott"]                             | "year": 1884, | Abbott, Edwin/Flatland - A Romance (1884)/Flatland - A Romance.epub |
      | AC/DC: Live          | ["Jo Smith"]                                 | "year": 2001, | Smith, Jo/AC-DC - Live (2001)/AC-DC - Live.epub                  |
      | Is it? Yes *really*  | ["Jo Smith"]                                 |               | Smith, Jo/Is it Yes really/Is it Yes really.epub                 |

  Scenario Outline: Only the first author names the folder; four or more add et al
    Given the sidecar "library/inbox/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K1000000000000000001", "title": "Domain-Driven Design", "authors": <authors>, "year": 2003, "files": [{ "name": "book.epub" }] }
      """
    And I have run shelf with arguments "reindex --library library"
    When I run shelf with arguments "organize --apply --library library"
    Then the file "library/<folder>/Domain-Driven Design (2003)/Domain-Driven Design.epub" exists

    Examples:
      | authors                                              | folder                  |
      | ["Eric Evans"]                                       | Evans, Eric             |
      | ["Eric Evans", "Ann Bee", "Cy Dee"]                  | Evans, Eric             |
      | ["Eric Evans", "Ann Bee", "Cy Dee", "Di Eff"]        | Evans, Eric et al       |
      | ["Eric Evans", "Ann Bee", "Cy Dee", "Di Eff", "Ed Gee"] | Evans, Eric et al    |
