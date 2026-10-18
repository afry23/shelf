@F-ORG-6 @M2
Feature: Undoing the last organize run
  As a user who organized the library and regrets it
  I want shelf undo to put everything back exactly as it was
  So that organizing is safe to try

  Background:
    Given an empty working directory
    And I have run shelf with arguments "init library"
    And a sample EPUB "library/inbox/pea.epub" with the title "Patterns of Enterprise Application Architecture", the authors "Martin Fowler" and the text seed 1
    And a sample PDF "library/inbox/pea.pdf" with the title "Patterns of Enterprise Application Architecture", the author "Martin Fowler" and the text seed 1
    And the sidecar "library/inbox/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K1000000000000000001", "title": "Patterns of Enterprise Application Architecture", "authors": ["Martin Fowler"], "year": 2002, "tags": ["architecture"], "files": [{ "name": "pea.epub" }, { "name": "pea.pdf" }] }
      """
    And a sample EPUB "library/misc/ddia.epub" with the title "Designing Data-Intensive Applications", the authors "Martin Kleppmann" and the text seed 2
    And the sidecar "library/misc/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K2000000000000000002", "title": "Designing Data-Intensive Applications", "authors": ["Martin Kleppmann"], "year": 2017, "files": [{ "name": "ddia.epub" }] }
      """
    And I have run shelf with arguments "reindex --library library"
    And I take a snapshot of the folder "library"

  Scenario: Undo restores the exact initial state
    Given I have run shelf with arguments "organize --apply --library library"
    When I run shelf with arguments "undo --library library"
    Then the exit code is 0
    And standard output contains "Reverted the last organize run"
    And the folder "library" equals the snapshot

  Scenario: The index follows the undo
    Given I have run shelf with arguments "organize --apply --library library"
    When I run shelf with arguments "undo --library library"
    Then the file "pea.epub" in the index has the path "inbox/pea.epub"
    And the table "works" of the index has 2 rows
    And the table "files" of the index has 3 rows

  Scenario: Undo only reverts the last run
    Given I have run shelf with arguments "organize --apply --library library"
    And I have run shelf with arguments "organize --apply --pattern '{author}/{title}.{ext}' --library library"
    When I run shelf with arguments "undo --library library"
    Then the file "library/Fowler, Martin/Patterns of Enterprise Application Architecture (2002)/Patterns of Enterprise Application Architecture.epub" exists
    And the file "library/Martin Fowler/Patterns of Enterprise Application Architecture.epub" does not exist
    When I run shelf with arguments "undo --library library"
    Then the folder "library" equals the snapshot

  Scenario: Undoing twice has nothing left to undo
    Given I have run shelf with arguments "organize --apply --library library"
    And I have run shelf with arguments "undo --library library"
    When I run shelf with arguments "undo --library library"
    Then the exit code is 1
    And standard error contains "error: nothing to undo"

  Scenario: Without a run there is nothing to undo
    When I run shelf with arguments "undo --library library"
    Then the exit code is 1
    And standard error contains "error: nothing to undo"

  Scenario: A dry run leaves nothing to undo
    Given I have run shelf with arguments "organize --library library"
    When I run shelf with arguments "undo --library library"
    Then the exit code is 1
    And standard error contains "error: nothing to undo"

  Scenario: Merge is not undone by shelf undo
    Given I have run shelf with arguments "merge 01J9Z3K1 01J9Z3K2 --library library"
    When I run shelf with arguments "undo --library library"
    Then the exit code is 1
    And standard error contains "error: nothing to undo"

  Scenario: A file that changed since the run is kept, the rest is restored
    Given I have run shelf with arguments "organize --apply --library library"
    And the file "library/Fowler, Martin/Patterns of Enterprise Application Architecture (2002)/Patterns of Enterprise Application Architecture.pdf" has the content "edited"
    When I run shelf with arguments "undo --library library"
    Then the exit code is 1
    And standard error contains "kept Fowler, Martin/Patterns of Enterprise Application Architecture (2002)/Patterns of Enterprise Application Architecture.pdf: changed since"
    And the file "library/Fowler, Martin/Patterns of Enterprise Application Architecture (2002)/Patterns of Enterprise Application Architecture.pdf" contains "edited"
    And the file "library/inbox/pea.epub" exists
    And the file "library/misc/ddia.epub" exists

  Scenario: The undo as JSON
    Given I have run shelf with arguments "organize --apply --library library"
    When I run shelf with arguments "undo --library library --json"
    Then the exit code is 0
    And standard output is a JSON object with the integer properties "skipped=0"
