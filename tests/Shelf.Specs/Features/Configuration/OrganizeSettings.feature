@F-ORG-1 @F-ORG-2 @PRD-9.2 @M2
Feature: The organize pattern in the global and the per-library config
  As a user of shelf
  I want to choose how my library folders and files are named
  So that shelf organize sorts the books the way I like them

  Scenario: Without any config file the default pattern applies
    When shelf resolves the settings
    Then the setting "organize.pattern" is "{author_sort}/{title} ({year})/{title}.{ext}"
    And there are no warnings

  Scenario Outline: The library config overrides the global config, which overrides the default
    Given the global config contains the line "<global line>"
    And the library config contains the line "<library line>"
    When shelf resolves the settings
    Then the setting "organize.pattern" is "<expected>"
    And there are no warnings

    Examples:
      | global line                                  | library line                                 | expected                                  |
      | # nothing                                    | # nothing                                    | {author_sort}/{title} ({year})/{title}.{ext} |
      | organize.pattern = '{author}/{title}.{ext}'  | # nothing                                    | {author}/{title}.{ext}                    |
      | # nothing                                    | organize.pattern = '{language}/{title}.{ext}' | {language}/{title}.{ext}                  |
      | organize.pattern = '{author}/{title}.{ext}'  | organize.pattern = '{title}.{ext}'           | {title}.{ext}                             |

  Scenario Outline: An invalid pattern is rejected with the key and the file
    Given the library config contains the line "<line>"
    When shelf resolves the settings
    Then resolving the settings fails with an error containing "<message>"

    Examples:
      | line                                      | message                                                                  |
      | organize.pattern = ''                     | library.toml: organize.pattern: must not be empty                        |
      | organize.pattern = '{title}'              | library.toml: organize.pattern: the last part must end with '.{ext}'     |
      | organize.pattern = '{author}/{title}.pdf' | library.toml: organize.pattern: the last part must end with '.{ext}'     |
      | organize.pattern = '{nope}/{title}.{ext}' | library.toml: organize.pattern: unknown placeholder '{nope}'             |
      | organize.pattern = '{title.{ext}'         | library.toml: organize.pattern: unbalanced braces                        |
      | organize.pattern = '/{title}.{ext}'       | library.toml: organize.pattern: must be a relative path                  |
      | organize.pattern = '../{title}.{ext}'     | library.toml: organize.pattern: must not contain '..'                    |
      | organize.pattern = 5                      | library.toml: organize.pattern: expected string, found integer           |
