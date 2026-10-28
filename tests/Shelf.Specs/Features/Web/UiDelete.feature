@ui @F-DUP-7 @F-DUP-4 @M5
Feature: Deleting files and works in the browser
  As a person who wants a book or one of its files gone
  I want to delete it in the work view after a confirmation
  So that I do not need the command line and nothing goes by accident

  Background:
    Given an empty working directory
    And I have run shelf with arguments "init library"
    And a sample EPUB "library/Ibryam, Bilgin/Kubernetes Patterns (2019)/kp.epub" with the title "Kubernetes Patterns", the authors "Bilgin Ibryam" and the text seed 1
    And a sample PDF "library/Ibryam, Bilgin/Kubernetes Patterns (2019)/kp.pdf" with the title "Kubernetes Patterns", the author "Bilgin Ibryam" and the text seed 2
    And the sidecar "library/Ibryam, Bilgin/Kubernetes Patterns (2019)/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K1000000000000000001", "title": "Kubernetes Patterns", "authors": ["Bilgin Ibryam"], "year": 2019, "files": [{ "name": "kp.epub" }, { "name": "kp.pdf" }] }
      """
    And a sample EPUB "library/Davis, Cornelia/Cloud Native Patterns (2019)/cnp.epub" with the title "Cloud Native Patterns", the authors "Cornelia Davis" and the text seed 3
    And the sidecar "library/Davis, Cornelia/Cloud Native Patterns (2019)/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K2000000000000000002", "title": "Cloud Native Patterns", "authors": ["Cornelia Davis"], "year": 2019, "files": [{ "name": "cnp.epub" }] }
      """
    And I have run shelf with arguments "reindex --library library"
    And I take a snapshot of the folder "library"
    And the web server is running on the library "library"

  Scenario: A file is deleted after a confirmation
    Given I open "/works/01J9Z3K1000000000000000001" in the browser
    When I click the "Delete kp.pdf" button
    Then the page shows the text "Move kp.pdf to the trash?"
    When I click the "Confirm" button
    Then the page shows the text "Moved kp.pdf to the trash"
    And the file "library/Ibryam, Bilgin/Kubernetes Patterns (2019)/kp.pdf" does not exist
    And the file "library/Ibryam, Bilgin/Kubernetes Patterns (2019)/kp.epub" exists

  Scenario: Nothing happens without the confirmation
    Given I open "/works/01J9Z3K1000000000000000001" in the browser
    When I click the "Delete kp.pdf" button
    And I click the "Cancel" button
    Then the page does not show the text "Move kp.pdf to the trash?"
    And the folder "library" equals the snapshot

  Scenario: A work is deleted after a confirmation and the library is shown
    Given I open "/works/01J9Z3K1000000000000000001" in the browser
    When I click the "Delete this work" button
    Then the page shows the text "Move Kubernetes Patterns and its 2 files to the trash?"
    And the page shows the text "shelf undo"
    When I click the "Confirm" button
    Then the browser address does not contain "01J9Z3K1000000000000000001"
    And the page shows the text "Cloud Native Patterns"
    And the page does not show the text "Kubernetes Patterns"
    And the directory "library/Ibryam, Bilgin/Kubernetes Patterns (2019)" does not exist
