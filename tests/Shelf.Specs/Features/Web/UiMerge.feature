@ui @F-GRP-4 @F-DUP-3 @M5
Feature: Merging works in the browser
  As a person whose library holds one book as two works
  I want to merge the works in the duplicate view and on the work page
  So that I can join them without the command line

  Background:
    Given an empty working directory
    And I have run shelf with arguments "init library"
    And a sample EPUB "library/Evans, Eric/Domain-Driven Design (2003)/ddd.epub" with the title "Domain-Driven Design", the authors "Eric Evans" and the text seed 1
    And the sidecar "library/Evans, Eric/Domain-Driven Design (2003)/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K1000000000000000001", "title": "Domain-Driven Design", "authors": ["Eric Evans"], "year": 2003, "files": [{ "name": "ddd.epub" }] }
      """
    And a sample EPUB "library/Avram, Abel/DDD Quickly/ddd-quickly.epub" with the title "DDD Quickly", the authors "Abel Avram" and the text seed 1
    And the sidecar "library/Avram, Abel/DDD Quickly/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K2000000000000000002", "title": "DDD Quickly", "authors": ["Abel Avram"], "files": [{ "name": "ddd-quickly.epub" }] }
      """
    And a sample EPUB "library/Ibryam, Bilgin/Kubernetes Patterns (2019)/kp.epub" with the title "Kubernetes Patterns", the authors "Bilgin Ibryam" and the text seed 3
    And the sidecar "library/Ibryam, Bilgin/Kubernetes Patterns (2019)/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K3000000000000000003", "title": "Kubernetes Patterns", "authors": ["Bilgin Ibryam"], "year": 2019, "files": [{ "name": "kp.epub" }] }
      """
    And I have run shelf with arguments "reindex --library library"
    And I take a snapshot of the folder "library"
    And the web server is running on the library "library"

  Scenario: Each work of a probable pair offers to take the other one in
    When I open "/duplicates" in the browser
    Then the file card "Domain-Driven Design" has a "Merge the other into this one" button
    And the file card "DDD Quickly" has a "Merge the other into this one" button

  Scenario Outline: The work the person picks survives
    Given I open "/duplicates" in the browser
    When I click the "Merge the other into this one" button of the file card "<kept>"
    Then the file card "<kept>" asks to confirm
    When I click the "Confirm" button of the file card "<kept>"
    Then the page shows the text "Merged <gone> into <kept>"
    And the page shows the text "cannot be undone"
    And the page does not show the text "Probable duplicates"
    And the file "<kept folder>/book.json" exists
    And the file "<gone folder>/book.json" does not exist
    And the file "<kept folder>/<gone file>" exists

    Examples:
      | kept                 | gone                 | kept folder                                | gone folder                  | gone file          |
      | Domain-Driven Design | DDD Quickly          | library/Evans, Eric/Domain-Driven Design (2003) | library/Avram, Abel/DDD Quickly | ddd-quickly.epub   |
      | DDD Quickly          | Domain-Driven Design | library/Avram, Abel/DDD Quickly            | library/Evans, Eric/Domain-Driven Design (2003) | ddd.epub |

  Scenario: Nothing happens without the confirmation
    Given I open "/duplicates" in the browser
    When I click the "Merge the other into this one" button of the file card "Domain-Driven Design"
    And I press Escape
    Then the file card "Domain-Driven Design" does not ask to confirm
    And the folder "library" equals the snapshot

  Scenario: A work is merged into the one that is open
    Given I open "/works/01J9Z3K1000000000000000001" in the browser
    When I fill the "Find a work to merge in" field with "Quickly"
    And I click the "Merge into this book" button
    Then the page shows the text "Merge DDD Quickly into Domain-Driven Design?"
    And the page shows the text "cannot be undone"
    When I click the "Confirm" button
    Then the page shows the text "Merged DDD Quickly into this book: 1 file moved"
    And the page shows the text "ddd-quickly.epub"
    And the file "library/Evans, Eric/Domain-Driven Design (2003)/ddd-quickly.epub" exists
    And the file "library/Avram, Abel/DDD Quickly/book.json" does not exist

  Scenario: The open work is not offered to be merged into itself
    Given I open "/works/01J9Z3K1000000000000000001" in the browser
    When I fill the "Find a work to merge in" field with "Domain"
    Then the page shows the text "No other work found"
    And the page does not show the text "Merge into this book"
