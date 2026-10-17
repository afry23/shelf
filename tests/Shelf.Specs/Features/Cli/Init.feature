@F-LIB-1 @PRD-7.1 @M1
Feature: Creating a library with shelf init
  As a user with a folder of e-books
  I want to turn a folder into a shelf library
  So that shelf has a place for its settings and its review queue

  Background:
    Given an empty working directory

  Scenario: Init creates the library folder and its .shelf folder
    When I run shelf with arguments "init library"
    Then the exit code is 0
    And standard output contains "Initialized library in "
    And standard output contains "library"
    And standard error is empty
    And the directory "library/.shelf" exists
    And the file "library/.shelf/config.toml" contains "auto_merge"
    And the file "library/.shelf/review.json" contains '"entries": []'

  Scenario: Init can turn a folder that already holds books into a library without touching them
    Given a file "books/Flatland.epub" in the working directory
    And a file "books/old/Notes.pdf" in the working directory
    When I run shelf with arguments "init books"
    Then the exit code is 0
    And the directory "books/.shelf" exists
    And the file "books/Flatland.epub" contains "data"
    And the file "books/old/Notes.pdf" contains "data"

  Scenario: A path with spaces is one argument
    When I run shelf with arguments 'init "my library"'
    Then the exit code is 0
    And the directory "my library/.shelf" exists

  Scenario: The JSON output names the library root
    When I run shelf with arguments "init library --json"
    Then the exit code is 0
    And standard output is a JSON object with the string property "root" matching "[/\\]library$"

  Scenario: Init refuses a folder that is already a library
    Given I have run shelf with arguments "init library"
    When I run shelf with arguments "init library"
    Then the exit code is 1
    And standard output is empty
    And standard error starts with "error: "
    And standard error contains "is already a shelf library"

  Scenario: Init refuses to nest a library inside another one
    Given I have run shelf with arguments "init library"
    When I run shelf with arguments "init library/sub"
    Then the exit code is 1
    And standard error contains "is inside the library"
    And the directory "library/sub" does not exist

  Scenario: Init refuses a path that is a file
    Given a file "afile" in the working directory
    When I run shelf with arguments "init afile"
    Then the exit code is 1
    And standard error contains "is a file"

  Scenario Outline: Init reports usage errors with exit code 2
    When I run shelf with arguments "<arguments>"
    Then the exit code is 2
    And standard output is empty
    And standard error starts with "<message>"
    And standard error contains "Usage: shelf"

    Examples:
      | arguments            | message                                    |
      | init                 | Required argument missing for command: 'init'. |
      | init --frobnicate    | Unknown option '--frobnicate'              |
      | init a b             | Unexpected argument 'b'                    |

  Scenario: Unknown keys in the global config are reported and ignored
    Given a global config file with content:
      """
      colour = true
      """
    When I run shelf with arguments "init library"
    Then the exit code is 0
    And standard error contains "unknown key 'colour'"
    And the directory "library/.shelf" exists

  Scenario: An invalid global config stops init before anything is created
    Given a global config file with content:
      """
      [grouping]
      auto_merge = 7
      """
    When I run shelf with arguments "init library"
    Then the exit code is 1
    And standard error starts with "error: "
    And standard error contains "grouping.auto_merge: must be between 0 and 1"
    And the directory "library" does not exist
