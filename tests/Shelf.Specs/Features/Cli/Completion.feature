@PRD-7.1 @M5
Feature: Shell completion
  As a user of the command line
  I want shelf to complete its commands and options in my shell
  So that I do not have to remember or type them in full

  Background:
    Given an empty working directory

  Scenario Outline: The script for a shell is written to standard output
    When I run shelf with arguments "completion <shell>"
    Then the exit code is 0
    And standard output contains "<marker>"
    And standard error is empty

    Examples:
      | shell | marker                |
      | bash  | complete -F _shelf shelf |
      | zsh   | #compdef shelf        |
      | fish  | complete -c shelf     |

  Scenario Outline: Every shell knows the commands, the subcommands and the options of the model
    When I run shelf with arguments "completion <shell>"
    Then standard output contains "<command>"
    And standard output contains "<subcommand>"
    And standard output contains "<option>"

    Examples:
      | shell | command    | subcommand | option     |
      | bash  | organize   | rename     | --bind     |
      | zsh   | organize   | rename     | --bind     |
      | fish  | organize   | rename     | -l bind    |

  Scenario Outline: The commands of the interface are in the script
    When I run shelf with arguments "completion bash"
    Then standard output contains "<name>"

    Examples:
      | name       |
      | completion |
      | mcp        |
      | serve      |
      | similar    |
      | undo       |

  Scenario: The script is the same on every run
    Given I run shelf with arguments "completion bash"
    And I note the standard output
    When I run shelf with arguments "completion bash"
    Then standard output is the noted output

  Scenario Outline: A shell that is not supported is a usage error
    When I run shelf with arguments "completion <shell>"
    Then the exit code is 2
    And standard output is empty

    Examples:
      | shell      |
      | powershell |
      | Bash       |
      | sh         |

  Scenario: Without a shell it is a usage error
    When I run shelf with arguments "completion"
    Then the exit code is 2
    And standard output is empty

  Scenario Outline: In bash the commands are completed from what has been typed
    Given I run shelf with arguments "completion bash"
    When I complete the line "<line>" in bash
    Then the completions are "<completions>"

    Examples:
      | line                | completions                                                         |
      | shelf ta            | tag                                                                 |
      | shelf tag           | tag                                                                 |
      | shelf tag a         | add                                                                 |
      | shelf se            | search semantic serve                                               |
      | shelf serve --b     | --bind                                                              |
      | shelf serve --      | --bind --help --json --library --port --token                       |
      | shelf mcp --h       | --help --http                                                       |
      | shelf tag add --li  | --library                                                           |
      | shelf --j           | --json                                                              |
      | shelf nope --       | --help --json --version                                             |

  Scenario: In bash a command offers its subcommands after a space
    Given I run shelf with arguments "completion bash"
    When I complete the line "shelf tag " in bash
    Then the completions include "add"
    And the completions include "rename"
    And the completions do not include "serve"

  Scenario: In bash the value of --library is a path
    Given I run shelf with arguments "completion bash"
    And a folder "books"
    When I complete the line "shelf search --library boo" in bash
    Then the completions are "books"

  Scenario Outline: The script is valid for the shell
    When I run shelf with arguments "completion <shell>"
    Then the script is valid for <shell>

    Examples:
      | shell |
      | bash  |
      | zsh   |
      | fish  |
