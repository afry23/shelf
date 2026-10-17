@PRD-10.2 @M1
Feature: The log level can be set with the SHELF_LOG_LEVEL environment variable
  As a user diagnosing a problem
  I want to raise the log level without changing the command line
  So that I can see diagnostic output on standard error while standard output stays clean

  Scenario Outline: SHELF_LOG_LEVEL selects the minimum log level
    Given the environment variable "SHELF_LOG_LEVEL" is set to "<value>"
    When I run shelf with arguments "--version"
    Then the exit code is 0
    And standard output matches the pattern "^shelf \d+\.\d+\.\d+"
    And standard error contains <trace> log lines at level "trace"
    And standard error contains <debug> log lines at level "debug"

    Examples:
      | value       | trace | debug |
      | Trace       | 1     | 2     |
      | trace       | 1     | 2     |
      | DEBUG       | 0     | 2     |
      | Information | 0     | 0     |
      | None        | 0     | 0     |

  Scenario Outline: An unset or empty SHELF_LOG_LEVEL keeps the default
    Given the environment variable "SHELF_LOG_LEVEL" is set to "<value>"
    When I run shelf with arguments "--version"
    Then the exit code is 0
    And standard error is empty

    Examples:
      | value |
      |       |

  Scenario Outline: An invalid SHELF_LOG_LEVEL is reported and ignored
    Given the environment variable "SHELF_LOG_LEVEL" is set to "<value>"
    When I run shelf with arguments "--version"
    Then the exit code is 0
    And standard output matches the pattern "^shelf \d+\.\d+\.\d+"
    And standard error contains the line "warning: ignoring invalid SHELF_LOG_LEVEL '<value>'"

    Examples:
      | value   |
      | verbose |
      | 3       |
      | warn    |
