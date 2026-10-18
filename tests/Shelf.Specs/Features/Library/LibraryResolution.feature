@F-LIB-1 @PRD-9.2 @M1
Feature: Finding the library a command works on
  As a user with one or more libraries
  I want shelf to find the right library without typing the path every time
  So that I can run commands from inside a library or use a default library

  Background:
    Given the global config is:
      """
      default_library = "main"

      [[libraries]]
      name = "main"
      path = "/books/main"

      [[libraries]]
      name = "work"
      path = "/books/work/"
      """
    And the directory "/books/main" is a library
    And the directory "/books/work" is a library
    And the directory "/elsewhere/papers" is a library

  Scenario Outline: The library comes from the option, the working directory or the default, in that order
    Given the working directory is "<working directory>"
    When shelf locates the library with the option "<option>"
    Then the library root is "<root>"
    And the library comes from the "<source>"

    Examples:
      | working directory           | option             | root              | source            |
      | /elsewhere/papers           |                    | /elsewhere/papers | working directory |
      | /elsewhere/papers/a/b       |                    | /elsewhere/papers | working directory |
      | /tmp                        |                    | /books/main       | default library   |
      | /elsewhere/papers           | work               | /books/work       | option            |
      | /elsewhere/papers           | /elsewhere/papers  | /elsewhere/papers | option            |
      | /elsewhere                  | papers             | /elsewhere/papers | option            |
      | /tmp                        | /books/work        | /books/work       | option            |

  Scenario Outline: Locating fails with a message that says what to do
    Given the working directory is "<working directory>"
    When shelf locates the library with the option "<option>"
    Then locating the library fails with an error containing "<message>"

    Examples:
      | working directory | option      | message                                   |
      | /tmp              | /tmp/nowhere | '/tmp/nowhere' is not a shelf library     |
      | /tmp              | nowhere     | 'nowhere' is not a shelf library          |
