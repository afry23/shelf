@ui @PRD-8.2 @F-ORG-5 @F-ORG-6 @M5
Feature: Organizing the library in the browser
  As a person who wants tidy folders
  I want to see the diff first, run it, and take it back
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
    And the web server is running on the library "library"

  Scenario: The header leads to the organize view
    Given I open "/library" in the browser
    When I click the "Organize" link
    Then the browser address contains "/organize"

  Scenario: The preview shows old and new place and moves nothing
    When I open "/organize" in the browser
    Then the page shows the text "3 files of 2 works would move"
    And the page shows the text "inbox/pea.epub"
    And the page shows the text "Fowler, Martin/Patterns of Enterprise Application Architecture (2002)/Patterns of Enterprise Application Architecture.epub"
    And the folder "library" equals the snapshot

  Scenario: Moving needs a second click
    Given I open "/organize" in the browser
    When I click the "Move the files" button
    Then the page shows the text "Confirm: move 3 files"
    And the folder "library" equals the snapshot
    When I click the "Cancel" button
    Then the page does not show the text "Confirm: move 3 files"
    And the folder "library" equals the snapshot

  Scenario: The files move, then the view says everything is in place
    Given I open "/organize" in the browser
    And I click the "Move the files" button
    When I click the "Confirm: move 3 files" button
    Then the page shows the text "Moved 3 files of 2 works"
    And the page shows the text "Everything is where the pattern puts it"
    And the file "library/Fowler, Martin/Patterns of Enterprise Application Architecture (2002)/Patterns of Enterprise Application Architecture.epub" exists
    And the file "library/inbox/pea.epub" does not exist

  Scenario: Undo puts the files back
    Given I open "/organize" in the browser
    And I click the "Move the files" button
    When I click the "Confirm: move 3 files" button
    Then the page shows the text "Moved 3 files"
    When I click the "Undo organize" button
    Then the page shows the text "Reverted the last organize run"
    And the page shows the text "3 files of 2 works would move"
    And the folder "library" equals the snapshot

  Scenario: Another pattern can be previewed
    Given I open "/organize" in the browser
    When I fill the "Pattern" field with "{author}/{title}.{ext}"
    And I click the "Preview" button
    Then the page shows the text "Martin Fowler/Patterns of Enterprise Application Architecture.epub"
    And the folder "library" equals the snapshot

  Scenario: A pattern that is not valid says why
    Given I open "/organize" in the browser
    When I fill the "Pattern" field with "{title}"
    And I click the "Preview" button
    Then the page shows the text "pattern"
    And the folder "library" equals the snapshot
