@PRD-9.2 @M0
Feature: Reading a config.toml file
  As a user of shelf
  I want shelf to read settings from a TOML config file
  So that behavior such as grouping thresholds can be configured without code changes

  Background:
    Given an empty working directory

  Scenario Outline: Values of each scalar type are read by dotted key
    Given a config file "config.toml" with content:
      """
      title = "My Library"

      [grouping]
      auto_merge = 0.9
      review = 0.5
      max_hamming = 10

      [mcp]
      readOnly = true
      """
    When shelf loads the config file "config.toml"
    Then loading succeeds
    And the <type> value at "<key>" is "<value>"

    Examples:
      | key                  | type    | value      |
      | title                | string  | My Library |
      | grouping.auto_merge  | float   | 0.9        |
      | grouping.review      | float   | 0.5        |
      | grouping.max_hamming | integer | 10         |
      | mcp.readOnly         | boolean | true       |

  Scenario Outline: TOML syntax variants yield the same value
    Given a config file "config.toml" with content:
      """
      <line 1>
      <line 2>
      """
    When shelf loads the config file "config.toml"
    Then loading succeeds
    And the <type> value at "<key>" is "<value>"

    Examples:
      | variant              | line 1                          | line 2            | key                 | type    | value         |
      | literal string       | name = 'single quoted'          |                   | name                | string  | single quoted |
      | unicode escape       | name = "caf\\u00e9"             |                   | name                | string  | café          |
      | table header         | [grouping]                      | auto_merge = 0.9  | grouping.auto_merge | float   | 0.9           |
      | dotted key           | grouping.auto_merge = 0.9       |                   | grouping.auto_merge | float   | 0.9           |
      | inline table         | grouping = { auto_merge = 0.9 } |                   | grouping.auto_merge | float   | 0.9           |
      | exponent             | [grouping]                      | auto_merge = 9e-1 | grouping.auto_merge | float   | 0.9           |
      | digit separators     | limit = 20_000                  |                   | limit               | integer | 20000         |
      | hexadecimal          | limit = 0x4E20                  |                   | limit               | integer | 20000         |
      | trailing comment     | flag = true # enabled           |                   | flag                | boolean | true          |
      | comment line         | # flag = true                   | flag = false      | flag                | boolean | false         |
      | quoted key           | "max chars" = 4000              |                   | max chars           | integer | 4000          |
      # Q5: an integer is accepted where a float is requested
      | integer as float     | threshold = 1                   |                   | threshold           | float   | 1             |

  Scenario Outline: A key that is not in the file has no value
    Given a config file "config.toml" with content:
      """
      [grouping]
      auto_merge = 0.9
      """
    When shelf loads the config file "config.toml"
    Then loading succeeds
    And there is no value at "<key>"

    Examples:
      | key                 |
      | grouping.review     |
      | search              |
      | search.limit        |

  Scenario Outline: An empty or comment-only file is a valid empty configuration
    Given a config file "config.toml" with content:
      """
      <line 1>
      """
    When shelf loads the config file "config.toml"
    Then loading succeeds
    And there is no value at "grouping.auto_merge"

    Examples:
      | line 1             |
      |                    |
      | # nothing set here |
      | # grouping.auto_merge = 0.5 |
      |    # indented comment |

  # Q4: a missing file is an empty configuration, not an error
  Scenario: A missing config file is an empty configuration
    When shelf loads the config file "config.toml"
    Then loading succeeds
    And there is no value at "grouping.auto_merge"

  Scenario Outline: Invalid TOML is rejected with file and line
    Given a config file "config.toml" with content:
      """
      <line 1>
      <line 2>
      <line 3>
      """
    When shelf loads the config file "config.toml"
    Then loading fails with an error at line <line>
    And the error message contains "config.toml:<line>:"

    Examples:
      | problem              | line 1          | line 2     | line 3 | line |
      | missing value        | a = 1           | b =        |        | 2    |
      | duplicate key        | [grouping]      | x = 1      | x = 2  | 3    |
      | duplicate table      | [grouping]      | [grouping] |        | 2    |
      | unterminated string  | name = "open    |            |        | 1    |
      | leading zero         | n = 01          |            |        | 1    |
      | two pairs on a line  | a = 1 b = 2     |            |        | 1    |

  Scenario Outline: Reading a value with the wrong type fails with the key and both types
    Given a config file "config.toml" with content:
      """
      [grouping]
      auto_merge = "high"
      max_hamming = 10
      enabled = true
      """
    When shelf loads the config file "config.toml"
    And shelf reads the <requested> value at "<key>"
    Then reading fails with an error containing "<key>: expected <requested>, found <actual>"

    Examples:
      | key                  | requested | actual  |
      | grouping.auto_merge  | float     | string  |
      | grouping.enabled     | integer   | boolean |
      | grouping.max_hamming | string    | integer |
      | grouping             | string    | table   |

  Scenario Outline: Comments do not hide the settings around them
    Given a config file "config.toml" with content:
      """
      # a comment
      <setting> # and another
      """
    When shelf loads the config file "config.toml"
    Then loading succeeds
    And the float value at "grouping.auto_merge" is "0.5"

    Examples:
      | setting                  |
      | grouping.auto_merge = 0.5 |
      | grouping = { auto_merge = 0.5 } |
