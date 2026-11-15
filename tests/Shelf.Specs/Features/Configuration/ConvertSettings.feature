@F-CNV-2 @PRD-9.2 @M24
Feature: The converter for shelf convert in the config
  As a person who reads PDFs on a Kindle
  I want to name my own converter and its time limit in the config
  So that shelf convert knows which tool to run and when to give up

  Scenario: Without a config file no converter is set and the time limit is 600 seconds
    When shelf resolves the settings
    Then the setting "convert.command" is ""
    And the setting "convert.timeout" is "600"
    And there are no warnings

  Scenario Outline: The library config overrides the global config
    Given the global config contains the line "<global line>"
    And the library config contains the line "<library line>"
    When shelf resolves the settings
    Then the setting "convert.command" is "<expected>"
    And there are no warnings

    Examples:
      | global line                           | library line                          | expected       |
      | # nothing                             | # nothing                             |                |
      | convert.command = 'a {input} {output}' | # nothing                             | a {input} {output} |
      | # nothing                             | convert.command = 'b {input} {output}' | b {input} {output} |
      | convert.command = 'a {input} {output}' | convert.command = 'b {input} {output}' | b {input} {output} |

  Scenario Outline: An unusable value is rejected with the key and the file
    Given the library config contains the line "<line>"
    When shelf resolves the settings
    Then resolving the settings fails with an error containing "<message>"

    Examples:
      | line                              | message                                                              |
      | convert.command = ''              | library.toml: convert.command: must contain {input} and {output}     |
      | convert.command = 'tool {input}'  | library.toml: convert.command: must contain {input} and {output}     |
      | convert.command = 'tool {output}' | library.toml: convert.command: must contain {input} and {output}     |
      | convert.timeout = 0               | library.toml: convert.timeout: must be between 1 and 86400           |
      | convert.timeout = 90000           | library.toml: convert.timeout: must be between 1 and 86400           |
      | convert.timeout = 'long'          | library.toml: convert.timeout: expected integer, found string        |
