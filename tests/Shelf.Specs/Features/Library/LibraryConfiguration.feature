@F-LIB-1 @PRD-9.2 @M1
Feature: Several libraries in the global config
  As a user with more than one library
  I want to name my libraries in the global config
  So that I can pick one by name or set a default

  Scenario: Libraries and the default are read from the global config
    Given the global config is:
      """
      default_library = "work"

      [[libraries]]
      name = "main"
      path = "/books/main"

      [[libraries]]
      name = "work"
      path = "/books/work/"
      """
    When shelf resolves the settings
    Then the library "main" is at "/books/main"
    And the library "work" is at "/books/work"
    And the default library is "work"
    And there are no warnings

  Scenario: Without a libraries list there is no default library
    When shelf resolves the settings
    Then there are no libraries
    And there is no default library

  Scenario Outline: Invalid library entries are rejected
    Given the global config is:
      """
      <config>
      """
    When shelf resolves the settings
    Then resolving the settings fails with an error containing "<message>"

    Examples:
      | config                                                                                                             | message                                                  |
      | [[libraries]]\npath = "/books"                                                                                     | global.toml: libraries[1].name: missing                  |
      | [[libraries]]\nname = "main"                                                                                       | global.toml: libraries[1].path: missing                  |
      | [[libraries]]\nname = "main"\npath = "books"                                                                       | global.toml: libraries[1].path: must be an absolute path |
      | [[libraries]]\nname = ""\npath = "/books"                                                                          | global.toml: libraries[1].name: must not be empty        |
      | [[libraries]]\nname = "a"\npath = "/a"\n[[libraries]]\nname = "a"\npath = "/b"                                      | global.toml: libraries[2].name: duplicate library name 'a' |
      | default_library = "ghost"\n[[libraries]]\nname = "a"\npath = "/a"                                                  | global.toml: default_library: no library named 'ghost'   |
      | libraries = "x"                                                                                                    | global.toml: libraries: expected array, found string     |
