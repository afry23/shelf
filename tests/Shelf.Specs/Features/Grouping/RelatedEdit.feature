@F-GRP-5 @M2
Feature: Marking works as related by hand
  As a user who knows that two works are editions of one book
  I want to mark them as related, and to take the mark away
  So that the editions that shelf did not find are connected too

  Background:
    Given an empty working directory
    And I have run shelf with arguments "init library"
    And a sample EPUB "library/Evans, Eric/Domain-Driven Design (2003)/ddd.epub" with the title "Domain-Driven Design", the authors "Eric Evans" and the text seed 1
    And the sidecar "library/Evans, Eric/Domain-Driven Design (2003)/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K1000000000000000001", "title": "Domain-Driven Design", "authors": ["Eric Evans"], "year": 2003, "files": [{ "name": "ddd.epub" }] }
      """
    And a sample EPUB "library/Evans, Eric/Domain-Driven Design, 2nd Ed (2015)/ddd2.epub" with the title "Domain-Driven Design 2nd", the authors "Eric Evans" and the text seed 2
    And the sidecar "library/Evans, Eric/Domain-Driven Design, 2nd Ed (2015)/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K2000000000000000002", "title": "Domain-Driven Design, Second Edition", "authors": ["Eric Evans"], "year": 2015, "files": [{ "name": "ddd2.epub" }] }
      """
    And I have run shelf with arguments "reindex --library library"

  Scenario: A mark is written to both works
    When I run shelf with arguments "edit 01J9Z3K1 --add-related 01J9Z3K2 --library library"
    Then the exit code is 0
    And standard output is "Updated 01J9Z3K1000000000000000001: related"
    And the file "library/Evans, Eric/Domain-Driven Design (2003)/book.json" contains "01J9Z3K2000000000000000002"
    And the file "library/Evans, Eric/Domain-Driven Design, 2nd Ed (2015)/book.json" contains "01J9Z3K1000000000000000001"

  Scenario: Show names the other edition with its year
    Given I have run shelf with arguments "edit 01J9Z3K1 --add-related 01J9Z3K2 --library library"
    When I run shelf with arguments "show 01J9Z3K2 --library library"
    Then standard output matches the pattern "(?m)^Related\s+Domain-Driven Design \(2003\)\s+01J9Z3K1000000000000000001$"

  Scenario: Marking the other side as well is fine
    Given I have run shelf with arguments "edit 01J9Z3K1 --add-related 01J9Z3K2 --library library"
    When I run shelf with arguments "edit 01J9Z3K2 --add-related 01J9Z3K1 --library library"
    Then the exit code is 0

  Scenario: The mark can be taken away from either side
    Given I have run shelf with arguments "edit 01J9Z3K1 --add-related 01J9Z3K2 --library library"
    When I run shelf with arguments "edit 01J9Z3K2 --remove-related 01J9Z3K1 --library library"
    Then the exit code is 0
    And the file "library/Evans, Eric/Domain-Driven Design (2003)/book.json" does not contain "related"
    And the file "library/Evans, Eric/Domain-Driven Design, 2nd Ed (2015)/book.json" does not contain "related"

  Scenario: A work cannot be related to itself
    When I run shelf with arguments "edit 01J9Z3K1 --add-related 01J9Z3K1 --library library"
    Then the exit code is 1
    And standard error contains "error: a work cannot be related to itself"

  Scenario: A work that does not exist cannot be related
    When I run shelf with arguments "edit 01J9Z3K1 --add-related 01ZZZZZZ --library library"
    Then the exit code is 1
    And standard error contains "error:"

  Scenario: The mark stays after reindex
    Given I have run shelf with arguments "edit 01J9Z3K1 --add-related 01J9Z3K2 --library library"
    And I delete the file "library/.shelf/index.db"
    And I have run shelf with arguments "reindex --library library"
    When I run shelf with arguments "show 01J9Z3K1 --library library --json"
    Then the JSON property "related" of standard output has 1 items
