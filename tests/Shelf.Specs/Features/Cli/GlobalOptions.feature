@PRD-7.1 @M0
Feature: Global CLI options and environment
  As a user or a script calling shelf
  I want options and environment settings that apply to every command
  So that machine-readable and colorless output work the same everywhere

  Scenario: The help lists the global --json option
    When I run shelf with arguments "--help"
    Then the exit code is 0
    And standard output contains "--json"
    And standard error is empty

  Scenario Outline: --json is a known global option
    When I run shelf with arguments "<arguments>"
    Then the exit code is 2
    And standard output is empty
    And standard error starts with "<message>"
    And standard error does not contain "Unknown option '--json'"

    Examples:
      | arguments           | message                       |
      | --json              | Usage: shelf                  |
      | --json frobnicate   | Unknown command 'frobnicate'  |
      | --json --frobnicate | Unknown option '--frobnicate' |

  Scenario Outline: The version is printed as a JSON object with --json
    When I run shelf with arguments "<arguments>"
    Then the exit code is 0
    And standard output is a JSON object with the string property "version" matching "^\d+\.\d+\.\d+"
    And standard error is empty

    Examples:
      | arguments        |
      | --version --json |
      | --json --version |

  Scenario Outline: The help stays human-readable text with --json
    When I run shelf with arguments "<arguments>"
    Then the exit code is 0
    And standard output contains "Usage:"
    And standard output is not a JSON document
    And standard error is empty

    Examples:
      | arguments      |
      | --help --json  |
      | --json --help  |

  Scenario Outline: NO_COLOR suppresses color codes on a terminal
    Given standard output is a terminal
    And the environment variable "NO_COLOR" is set to "<value>"
    When I run shelf with arguments "<arguments>"
    Then the exit code is <exit code>
    And standard output contains no ANSI escape sequences
    And standard error contains no ANSI escape sequences

    Examples:
      | value | arguments    | exit code |
      | 1     | --help       | 0         |
      | 1     |              | 2         |
      | 1     | --frobnicate | 2         |
      | true  | --help       | 0         |
