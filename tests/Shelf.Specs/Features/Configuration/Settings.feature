@F-GRP-6 @PRD-9.2 @M1
Feature: Settings from the global and the per-library config
  As a user of shelf
  I want global defaults that a library can override
  So that grouping thresholds can be tuned per library without editing code

  Scenario: Without any config file the built-in defaults apply
    When shelf resolves the settings
    Then the setting "grouping.auto_merge" is "0.9"
    And the setting "grouping.review" is "0.5"
    And the setting "grouping.max_hamming" is "5"
    And the setting "grouping.auto_hamming" is "3"
    And there are no warnings

  Scenario Outline: The library config overrides the global config, which overrides the default
    Given the global config contains the line "<global line>"
    And the library config contains the line "<library line>"
    When shelf resolves the settings
    Then the setting "<key>" is "<expected>"
    And there are no warnings

    Examples:
      | global line                | library line               | key                  | expected |
      | # nothing                  | # nothing                  | grouping.auto_merge  | 0.9      |
      | grouping.auto_merge = 0.8  | # nothing                  | grouping.auto_merge  | 0.8      |
      | # nothing                  | grouping.auto_merge = 0.95 | grouping.auto_merge  | 0.95     |
      | grouping.auto_merge = 0.8  | grouping.auto_merge = 0.95 | grouping.auto_merge  | 0.95     |
      | grouping.review = 0.4      | # nothing                  | grouping.review      | 0.4      |
      | grouping.max_hamming = 6   | grouping.max_hamming = 8   | grouping.max_hamming | 8        |
      | grouping.auto_hamming = 2  | # nothing                  | grouping.auto_hamming | 2       |
      | grouping.auto_hamming = 1  | grouping.auto_hamming = 4  | grouping.auto_hamming | 4       |
      | # nothing                  | # nothing                  | semantic.model       | multilingual-e5-small |
      | semantic.model = 'a'       | # nothing                  | semantic.model       | a        |
      | semantic.model = 'a'       | semantic.model = 'b'       | semantic.model       | b        |
      | # nothing                  | # nothing                  | mcp.max_chars        | 20000    |
      | mcp.max_chars = 8000       | # nothing                  | mcp.max_chars        | 8000     |
      | mcp.max_chars = 8000       | mcp.max_chars = 3000       | mcp.max_chars        | 3000     |
      | grouping.auto_merge = 1    | # nothing                  | grouping.auto_merge  | 1        |

  Scenario Outline: Invalid threshold values are rejected with the key and the file
    Given the library config contains the line "<line>"
    When shelf resolves the settings
    Then resolving the settings fails with an error containing "<message>"

    Examples:
      | line                      | message                                                  |
      | grouping.auto_merge = 1.5 | library.toml: grouping.auto_merge: must be between 0 and 1 |
      | grouping.auto_merge = -0.1 | library.toml: grouping.auto_merge: must be between 0 and 1 |
      | grouping.review = 0.95    | grouping.review: must not exceed grouping.auto_merge     |
      | grouping.auto_hamming = 11 | library.toml: grouping.auto_hamming: must not exceed grouping.max_hamming |
      | grouping.auto_hamming = -1 | library.toml: grouping.auto_hamming: must be between 0 and 64 |
      | grouping.max_hamming = 2   | library.toml: grouping.auto_hamming: must not exceed grouping.max_hamming |
      | grouping.max_hamming = 65 | library.toml: grouping.max_hamming: must be between 0 and 64 |
      | grouping.max_hamming = -1 | library.toml: grouping.max_hamming: must be between 0 and 64 |
      | grouping.max_hamming = 2.5 | library.toml: grouping.max_hamming: expected integer, found float |
      | grouping.auto_merge = 'high' | library.toml: grouping.auto_merge: expected float, found string |
      | semantic.model = ''       | library.toml: semantic.model: must not be empty          |
      | semantic.model = 3        | library.toml: semantic.model: expected string, found integer |
      | mcp.max_chars = 0         | library.toml: mcp.max_chars: must be between 1 and 1000000 |
      | mcp.max_chars = 1000001   | library.toml: mcp.max_chars: must be between 1 and 1000000 |
      | mcp.max_chars = 'many'    | library.toml: mcp.max_chars: expected integer, found string |

  Scenario Outline: Unknown keys produce a warning and are otherwise ignored
    Given the <layer> config contains the line "<line>"
    When shelf resolves the settings
    Then there is the warning "<warning>"
    And the setting "grouping.auto_merge" is "0.9"

    Examples:
      | layer   | line                      | warning                                        |
      | global  | colour = true             | global.toml: unknown key 'colour'              |
      | library | grouping.auto_mrge = 0.9  | library.toml: unknown key 'grouping.auto_mrge' |
      | library | default_library = 'main' | library.toml: unknown key 'default_library'    |
