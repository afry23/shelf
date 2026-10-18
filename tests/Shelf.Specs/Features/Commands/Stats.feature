@PRD-7.1 @M1
Feature: Statistics of the library
  As the owner of a library
  I want an overview of what it holds
  So that I know its size, its formats and what still needs my attention

  Background:
    Given an empty working directory
    And I have run shelf with arguments "init library"

  Scenario: An empty library has nothing but zeros
    When I run shelf with arguments "stats --library library"
    Then the exit code is 0
    And standard output matches the pattern "(?m)^Works +0$"
    And standard output matches the pattern "(?m)^Files +0$"
    And standard output matches the pattern "(?m)^Review queue +0$"

  Scenario: An empty library in JSON
    When I run shelf with arguments "stats --library library --json"
    Then the exit code is 0
    And the JSON of standard output has the number "works" equal to 0
    And the JSON of standard output has the number "files" equal to 0
    And the JSON property "formats" of standard output has 0 items

  Scenario: Without an index the command asks for a reindex
    Given I delete the file "library/.shelf/index.db"
    When I run shelf with arguments "stats --library library"
    Then the exit code is 1
    And standard error contains "run 'shelf reindex'"
