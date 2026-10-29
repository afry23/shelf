@ui @F-GRP-4 @F-DUP-5 @M5
Feature: Splitting a file off a work in the browser
  As a person whose library put two different books into one work
  I want to split the wrong file off in the duplicate view and correct its title
  So that I can fix the grouping without the command line

  Background:
    Given an empty working directory
    And I have run shelf with arguments "init library"
    And a sample PDF "library/Love, Steve/C# Brain Teasers/c-brain-teasers_P1.0.pdf" with the title "C# Brain Teasers", the author "Steve Love" and the text seed 1
    And a sample PDF "library/Love, Steve/C# Brain Teasers/javascript-brain-teasers_P1.0.pdf" with the title "C# Brain Teasers", the author "Steve Love" and the text seed 5
    And the sidecar "library/Love, Steve/C# Brain Teasers/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K1000000000000000001", "title": "C# Brain Teasers", "authors": ["Steve Love"], "files": [{ "name": "c-brain-teasers_P1.0.pdf" }, { "name": "javascript-brain-teasers_P1.0.pdf" }] }
      """
    And a sample EPUB "library/Evans, Eric/Domain-Driven Design (2003)/ddd.epub" with the title "Domain-Driven Design", the authors "Eric Evans" and the text seed 1
    And a sample EPUB "library/Evans, Eric/Domain-Driven Design (2003)/ddd (1).epub" with the title "Domain-Driven Design", the authors "Eric Evans" and the text seed 1
    And the sidecar "library/Evans, Eric/Domain-Driven Design (2003)/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K2000000000000000002", "title": "Domain-Driven Design", "authors": ["Eric Evans"], "year": 2003, "files": [{ "name": "ddd.epub" }, { "name": "ddd (1).epub" }] }
      """
    And I have run shelf with arguments "reindex --library library"
    And I take a snapshot of the folder "library"
    And the web server is running on the library "library"

  Scenario: Only files of different content of one book can be split off
    When I open "/duplicates" in the browser
    Then the page shows the text "2 groups of duplicates"
    And the file card "javascript-brain-teasers_P1.0.pdf" has a "Split into own work" button
    And the file card "c-brain-teasers_P1.0.pdf" has a "Split into own work" button
    And the file card "ddd.epub" has no "Split into own work" button

  Scenario: A file is split off after a confirmation
    Given I open "/duplicates" in the browser
    When I click the "Split into own work" button of the file card "javascript-brain-teasers_P1.0.pdf"
    Then the file card "javascript-brain-teasers_P1.0.pdf" asks to confirm
    When I click the "Confirm" button of the file card "javascript-brain-teasers_P1.0.pdf"
    Then the page shows the text "Split javascript-brain-teasers_P1.0.pdf off into a work of its own"
    And the page shows the text "cannot be undone"
    And the page shows the text "1 group of duplicates"
    And the file "library/Love, Steve/C# Brain Teasers (2)/javascript-brain-teasers_P1.0.pdf" exists
    And the file "library/Love, Steve/C# Brain Teasers/javascript-brain-teasers_P1.0.pdf" does not exist

  Scenario: Nothing happens without the confirmation
    Given I open "/duplicates" in the browser
    When I click the "Split into own work" button of the file card "javascript-brain-teasers_P1.0.pdf"
    And I press Escape
    Then the file card "javascript-brain-teasers_P1.0.pdf" does not ask to confirm
    And the folder "library" equals the snapshot

  Scenario: The new work is opened and gets the right title
    Given I open "/duplicates" in the browser
    And I click the "Split into own work" button of the file card "javascript-brain-teasers_P1.0.pdf"
    And I click the "Confirm" button of the file card "javascript-brain-teasers_P1.0.pdf"
    When I click the "Open the new work" link
    And I open the "Files" tab
    Then the page shows the text "javascript-brain-teasers_P1.0.pdf"
    And the page does not show the text "c-brain-teasers_P1.0.pdf"
    When I click the "Edit details" button
    And I fill the "Title" field with "JavaScript Brain Teasers"
    And I click the "Save" button
    Then the heading "JavaScript Brain Teasers" is shown
    And the file "library/Love, Steve/C# Brain Teasers (2)/book.json" contains "JavaScript Brain Teasers"
