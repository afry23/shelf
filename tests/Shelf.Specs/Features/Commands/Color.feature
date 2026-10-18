@PRD-7.1 @M1
Feature: Colored output in the terminal
  As a user who works in a terminal
  I want headings and IDs to stand out, but only where color is wanted
  So that output is readable on screen and clean in pipes, files and with NO_COLOR

  Background:
    Given an empty working directory
    And I have run shelf with arguments "init library"
    And a sample EPUB "library/Ibryam, Bilgin/Kubernetes Patterns (2019)/kp.epub" with the title "Kubernetes Patterns", the authors "Bilgin Ibryam" and the text seed 1
    And the sidecar "library/Ibryam, Bilgin/Kubernetes Patterns (2019)/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K1000000000000000001", "title": "Kubernetes Patterns", "authors": ["Bilgin Ibryam"], "year": 2019, "files": [{ "name": "kp.epub" }] }
      """
    And the review queue of "library" gets an entry of the kind "weak-metadata" about "a.epub" with the message "title and authors come from the file name"
    And I have run shelf with arguments "reindex --library library"

  Scenario Outline: On a terminal the commands use color
    Given standard output is a terminal
    When I run shelf with arguments "<command> --library library"
    Then the exit code is 0
    And standard output contains an ANSI escape sequence

    Examples:
      | command                          |
      | show 01J9Z3K1                    |
      | stats                            |
      | review                           |
      | search kubernetes                |

  Scenario Outline: NO_COLOR switches color off even on a terminal
    Given standard output is a terminal
    And the environment variable "NO_COLOR" is set to "1"
    When I run shelf with arguments "<command> --library library"
    Then the exit code is 0
    And standard output contains no ANSI escape sequences
    And standard error contains no ANSI escape sequences

    Examples:
      | command           |
      | show 01J9Z3K1     |
      | stats             |
      | review            |
      | search kubernetes |

  Scenario Outline: Without a terminal there is no color
    When I run shelf with arguments "<command> --library library"
    Then the exit code is 0
    And standard output contains no ANSI escape sequences
    And standard error contains no ANSI escape sequences

    Examples:
      | command           |
      | show 01J9Z3K1     |
      | stats             |
      | review            |
      | search kubernetes |

  Scenario Outline: JSON is never colored
    Given standard output is a terminal
    When I run shelf with arguments "<command> --json --library library"
    Then the exit code is 0
    And standard output contains no ANSI escape sequences

    Examples:
      | command           |
      | show 01J9Z3K1     |
      | stats             |
      | review            |
      | search kubernetes |

  Scenario: An empty NO_COLOR does not switch color off
    Given standard output is a terminal
    And the environment variable "NO_COLOR" is set to ""
    When I run shelf with arguments "stats --library library"
    Then standard output contains an ANSI escape sequence

  Scenario: Color changes only the escape sequences, not the text
    Given standard output is a terminal
    When I run shelf with arguments "show 01J9Z3K1 --library library"
    Then standard output without ANSI escape sequences has the line "Authors   Bilgin Ibryam"
    And standard output without ANSI escape sequences has the line "Kubernetes Patterns"
