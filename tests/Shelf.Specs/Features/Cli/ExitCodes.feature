# Template feature file. Conventions (see CLAUDE.md):
# - English only.
# - Tag every feature or scenario with the PRD requirement ID and the milestone.
# - Back every scenario with concrete examples.
# - Express variations as a Scenario Outline with an Examples table.
@PRD-7.1 @M0
Feature: CLI exit codes
  As a user or a script calling shelf
  I want predictable exit codes and output streams
  So that I can tell success from usage errors

  Scenario: Printing the version succeeds
    When I run shelf with arguments "--version"
    Then the exit code is 0
    And standard output matches the pattern "^shelf \d+\.\d+\.\d+"
    And standard error is empty

  Scenario: Printing the help succeeds
    When I run shelf with arguments "--help"
    Then the exit code is 0
    And standard output contains "Usage:"
    And standard output contains "shelf [command] [options]"
    And standard error is empty

  Scenario Outline: Usage errors exit with code 2 and write to standard error
    When I run shelf with arguments "<arguments>"
    Then the exit code is 2
    And standard output is empty
    And standard error starts with "<message>"

    Examples:
      | arguments    | message                       |
      |              | Usage: shelf                  |
      | frobnicate   | Unknown command 'frobnicate'  |
      | --frobnicate | Unknown option '--frobnicate' |
