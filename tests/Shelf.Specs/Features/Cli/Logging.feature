@PRD-10.2 @M0
Feature: Diagnostic logging goes to standard error only
  As a user, a script or an MCP client reading shelf's standard output
  I want diagnostic log output to appear on standard error only
  So that command output and protocol messages on standard output stay clean

  Scenario Outline: Log lines on standard error follow the log level
    Given the log level is "<level>"
    When I run shelf with arguments "--version"
    Then the exit code is 0
    And standard output matches the pattern "^shelf \d+\.\d+\.\d+"
    And standard error contains <trace> log lines at level "trace"
    And standard error contains <debug> log lines at level "debug"

    Examples:
      | level       | trace | debug |
      | Trace       | 1     | 2     |
      | Debug       | 0     | 2     |
      | Information | 0     | 0     |
      | Warning     | 0     | 0     |
      | None        | 0     | 0     |

  Scenario Outline: Exact log lines for a successful run
    Given the log level is "Trace"
    When I run shelf with arguments "--version"
    Then standard error contains the line "<line>"

    Examples:
      | line                                                                    |
      | [trace] Shelf.Cli.ShelfApp: Parsed command line: 1 token(s), 0 error(s) |
      | [debug] Shelf.Cli.ShelfApp: Exiting with code 0                         |

  Scenario Outline: JSON output stays a single valid JSON object while logging
    Given the log level is "Trace"
    When I run shelf with arguments "<arguments>"
    Then the exit code is 0
    And standard output is a JSON object with the string property "version" matching "^\d+\.\d+\.\d+"
    And standard error contains 2 log lines at level "debug"

    Examples:
      | arguments        |
      | --version --json |
      | --json --version |

  Scenario Outline: Usage errors keep standard output empty while logging
    Given the log level is "Trace"
    When I run shelf with arguments "<arguments>"
    Then the exit code is 2
    And standard output is empty
    And standard error contains "<message>"
    And standard error contains the line "[trace] Shelf.Cli.ShelfApp: Parsed command line: <tokens> token(s), <errors> error(s)"
    And standard error contains the line "[debug] Shelf.Cli.ShelfApp: Exiting with code 2"

    Examples:
      | arguments         | message                       | tokens | errors |
      |                   | Usage: shelf                  | 0      | 0      |
      | --frobnicate      | Unknown option '--frobnicate' | 1      | 1      |
      | frobnicate --json | Unknown command 'frobnicate'  | 2      | 1      |

  Scenario Outline: Help text on standard output contains no log lines
    Given the log level is "Trace"
    When I run shelf with arguments "<arguments>"
    Then the exit code is 0
    And standard output contains "Usage:"
    And standard output does not contain "Shelf.Cli.ShelfApp"
    And standard error contains the line "[debug] Shelf.Cli.ShelfApp: Exiting with code 0"

    Examples:
      | arguments     |
      | --help        |
      | --help --json |

  Scenario Outline: Without a log level setting no log lines are written
    When I run shelf with arguments "<arguments>"
    Then the exit code is <exit code>
    And standard error does not contain "Shelf.Cli.ShelfApp"

    Examples:
      | arguments        | exit code |
      | --version        | 0         |
      | --version --json | 0         |
      | --help           | 0         |
      | --frobnicate     | 2         |

  Scenario: Log lines contain no color codes even where color is allowed
    Given standard output is a terminal
    And the log level is "Trace"
    When I run shelf with arguments "--version"
    Then the exit code is 0
    And standard error contains 2 log lines at level "debug"
    And standard error contains no ANSI escape sequences
