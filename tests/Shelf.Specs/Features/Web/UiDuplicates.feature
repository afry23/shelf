@ui @F-DUP-5 @F-DUP-4 @F-DUP-6 @M4
Feature: Duplicates in the browser
  As a person whose library collected copies
  I want to see the candidates next to each other and decide
  So that the copies go to the trash and nothing is lost for good

  Background:
    Given an empty working directory
    And I have run shelf with arguments "init library"
    And a sample EPUB "library/Evans, Eric/Domain-Driven Design (2003)/ddd.epub" with the title "Domain-Driven Design", the authors "Eric Evans" and the text seed 1
    And a sample EPUB "library/Evans, Eric/Domain-Driven Design (2003)/ddd (1).epub" with the title "Domain-Driven Design", the authors "Eric Evans" and the text seed 1
    And the sidecar "library/Evans, Eric/Domain-Driven Design (2003)/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K1000000000000000001", "title": "Domain-Driven Design", "authors": ["Eric Evans"], "year": 2003, "files": [{ "name": "ddd.epub" }, { "name": "ddd (1).epub" }] }
      """
    And a sample EPUB "library/Ibryam, Bilgin/Kubernetes Patterns (2019)/kp.epub" with the title "Kubernetes Patterns", the authors "Bilgin Ibryam" and the text seed 3
    And the sidecar "library/Ibryam, Bilgin/Kubernetes Patterns (2019)/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K2000000000000000002", "title": "Kubernetes Patterns", "authors": ["Bilgin Ibryam"], "year": 2019, "tags": ["devops"], "files": [{ "name": "kp.epub" }] }
      """
    And a sample EPUB "library/Imports/kp/kp-copy.epub" with the title "Kubernetes Patterns", the authors "Bilgin Ibryam" and the text seed 3
    And the sidecar "library/Imports/kp/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K3000000000000000003", "title": "Kubernetes Patterns (copy)", "authors": ["Bilgin Ibryam"], "files": [{ "name": "kp-copy.epub" }] }
      """
    And a sample EPUB "library/Davis, Cornelia/Cloud Native Patterns (2019)/a.epub" with the title "Cloud Native Patterns", the authors "Cornelia Davis" and the text seed 2
    And a sample EPUB "library/Davis, Cornelia/Cloud Native Patterns (2019)/b.epub" with the title "Cloud Native Patterns", the authors "Cornelia Davis", the ISBN "9781617294297" and the text seed 5
    And the sidecar "library/Davis, Cornelia/Cloud Native Patterns (2019)/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K4000000000000000004", "title": "Cloud Native Patterns", "authors": ["Cornelia Davis"], "year": 2019, "files": [{ "name": "a.epub" }, { "name": "b.epub" }] }
      """
    And I have run shelf with arguments "reindex --library library"
    And I take a snapshot of the folder "library"
    And the web server is running on the library "library"

  Scenario: The groups are shown with their files side by side
    When I open "/duplicates" in the browser
    Then the page shows the text "3 groups of duplicates"
    And the page shows the text "Identical files"
    And the page shows the text "Different files of one book"
    And the page shows the text "ddd (1).epub"
    And the page shows the text "ddd.epub"
    And the page shows the text "kp-copy.epub"

  Scenario: The suggestion is marked
    When I open "/duplicates" in the browser
    Then the file card "ddd.epub" is marked as the suggestion to keep
    And the file card "ddd (1).epub" is not marked as the suggestion to keep

  Scenario: The header says how many groups there are
    When I open "/library" in the browser
    Then the link "Duplicates" shows the number 3

  Scenario: A file is kept, the others go to the trash
    Given I open "/duplicates" in the browser
    When I click the "Keep this file" button of the file card "ddd.epub"
    Then the file card "ddd.epub" asks to confirm
    When I click the "Confirm" button of the file card "ddd.epub"
    Then the page shows the text "Moved 1 file to the trash"
    And the page shows the text "2 groups of duplicates"
    And the file "library/Evans, Eric/Domain-Driven Design (2003)/ddd (1).epub" does not exist
    And the file "library/Evans, Eric/Domain-Driven Design (2003)/ddd.epub" exists
    And the directory "library/.shelf/trash" exists

  Scenario: A file is moved to the trash
    Given I open "/duplicates" in the browser
    When I click the "Move to trash" button of the file card "ddd (1).epub"
    And I click the "Confirm" button of the file card "ddd (1).epub"
    Then the page shows the text "Moved 1 file to the trash"
    And the file "library/Evans, Eric/Domain-Driven Design (2003)/ddd (1).epub" does not exist

  Scenario: Nothing happens without the confirmation
    Given I open "/duplicates" in the browser
    When I click the "Keep this file" button of the file card "ddd.epub"
    And I press Escape
    Then the file card "ddd.epub" does not ask to confirm
    And the folder "library" equals the snapshot

  Scenario: A work that is left without files goes along
    Given I open "/duplicates" in the browser
    When I click the "Keep this file" button of the file card "kp.epub"
    And I click the "Confirm" button of the file card "kp.epub"
    Then the page shows the text "Moved 1 file to the trash"
    And the page shows the text "1 work was removed"
    And the directory "library/Imports/kp" does not exist

  Scenario: A group can be marked as not duplicates
    Given I open "/duplicates" in the browser
    When I click the "These are not duplicates" button of the group "Identical files" with the file "ddd.epub"
    Then the page shows the text "2 groups of duplicates"
    And the file "library/Evans, Eric/Domain-Driven Design (2003)/ddd.epub" exists
    And the file "library/Evans, Eric/Domain-Driven Design (2003)/ddd (1).epub" exists
    And the directory "library/.shelf/trash" does not exist

  Scenario: Without duplicates the page says so
    Given I open "/duplicates" in the browser
    When I click the "These are not duplicates" button of the group "Identical files" with the file "ddd.epub"
    And I click the "These are not duplicates" button of the group "Identical files" with the file "kp.epub"
    And I click the "These are not duplicates" button of the group "Different files of one book" with the file "a.epub"
    Then the page shows the text "No duplicates"

  @F-WEB-5 @M5
  Scenario: j moves through the files and Enter opens the book
    Given I open "/duplicates" in the browser
    Then the page shows the text "3 groups of duplicates"
    When I press the key "j"
    Then the focus is on the item "Cloud Native Patterns"
    When I press the key "Enter"
    Then the browser address contains "/works/01J9Z3K4000000000000000004"
