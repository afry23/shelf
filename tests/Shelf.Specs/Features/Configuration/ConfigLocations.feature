@PRD-9.2 @M0
Feature: Locations of the config files
  As a user of shelf
  I want shelf to look for its config files in well-known places
  So that I know where to put global and per-library settings

  Background:
    Given the home directory is "/home/ada"

  Scenario: Without XDG_CONFIG_HOME the global config lives in ~/.config
    When shelf resolves the global config file path
    Then the path is "/home/ada/.config/shelf/config.toml"

  Scenario Outline: XDG_CONFIG_HOME is honored only when it is an absolute path
    Given the environment variable "XDG_CONFIG_HOME" is set to "<value>"
    When shelf resolves the global config file path
    Then the path is "<path>"

    Examples:
      | value        | path                                |
      | /xdg         | /xdg/shelf/config.toml              |
      | /xdg/        | /xdg/shelf/config.toml              |
      |              | /home/ada/.config/shelf/config.toml |
      | relative/dir | /home/ada/.config/shelf/config.toml |

  Scenario Outline: The per-library config lives in the library's .shelf folder
    When shelf resolves the library config file path for the root "<root>"
    Then the path is "<path>"

    Examples:
      | root              | path                                 |
      | /books            | /books/.shelf/config.toml            |
      | /books/           | /books/.shelf/config.toml            |
      | /Users/ada/Ebooks | /Users/ada/Ebooks/.shelf/config.toml |
