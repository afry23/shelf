@ui @F-GRP-7 @F-GRP-8 @F-GRP-9 @M7
Feature: Adding, moving and creating in the browser
  As a person who follows a book that is still being written, or whose file landed in the wrong work
  I want to add a file, move a file and create a work in the web interface
  So that I do not need the command line to correct my library

  Background:
    Given an empty working directory
    And I have run shelf with arguments "init library"
    And a sample EPUB "library/Ibryam, Bilgin/Kubernetes Patterns (2019)/kp-v1.epub" with the title "Kubernetes Patterns", the authors "Bilgin Ibryam" and the text seed 1
    And a sample PDF "library/Ibryam, Bilgin/Kubernetes Patterns (2019)/cnp.pdf" with the title "Kubernetes Patterns", the author "Bilgin Ibryam" and the text seed 3
    And the sidecar "library/Ibryam, Bilgin/Kubernetes Patterns (2019)/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K1000000000000000001", "title": "Kubernetes Patterns", "authors": ["Bilgin Ibryam"], "year": 2019, "files": [{ "name": "kp-v1.epub" }, { "name": "cnp.pdf" }] }
      """
    And a sample EPUB "library/Davis, Cornelia/Cloud Native Patterns (2019)/cnp.epub" with the title "Cloud Native Patterns", the authors "Cornelia Davis" and the text seed 2
    And the sidecar "library/Davis, Cornelia/Cloud Native Patterns (2019)/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K2000000000000000002", "title": "Cloud Native Patterns", "authors": ["Cornelia Davis"], "year": 2019, "files": [{ "name": "cnp.epub" }] }
      """
    And a sample EPUB "upload/kp-v2.epub" with the title "Kubernetes Patterns", the authors "Bilgin Ibryam" and the text seed 4
    And a sample EPUB "upload/draft.epub" with the title "Wrong Title", the authors "Wrong Author" and the text seed 7
    And I have run shelf with arguments "reindex --library library"
    And the web server is running on the library "library"

  Scenario: A new version replaces the old file
    Given I open "/works/01J9Z3K1000000000000000001" in the browser
    And I open the "Files" tab
    When I choose the file "upload/kp-v2.epub" in the "File to add" field
    And I choose "kp-v1.epub" in the "Replaces" list
    And I click the "Add file" button
    Then the page shows the text "Added kp-v2.epub and moved kp-v1.epub to the trash."
    And the file "library/Ibryam, Bilgin/Kubernetes Patterns (2019)/kp-v2.epub" exists
    And the file "library/Ibryam, Bilgin/Kubernetes Patterns (2019)/kp-v1.epub" does not exist

  Scenario: A file moves to another work
    Given I open "/works/01J9Z3K1000000000000000001" in the browser
    And I open the "Files" tab
    When I click the "Move cnp.pdf" button
    And I fill the "Find the work to move the file to" field with "Cloud Native"
    And I click the "Move here" button
    Then the page shows the text "Moved cnp.pdf to Cloud Native Patterns."
    And the file "library/Davis, Cornelia/Cloud Native Patterns (2019)/cnp.pdf" exists
    And the file "library/Ibryam, Bilgin/Kubernetes Patterns (2019)/cnp.pdf" does not exist

  Scenario: A file becomes a work of its own
    Given I open "/works/01J9Z3K1000000000000000001" in the browser
    And I open the "Files" tab
    When I click the "Move cnp.pdf" button
    And I click the "Make it a work of its own" button
    Then the page shows the text "Made cnp.pdf a work of its own."
    And the file "library/Ibryam, Bilgin/Kubernetes Patterns (2019)/cnp.pdf" does not exist

  Scenario: Nothing moves when the person cancels
    Given I open "/works/01J9Z3K1000000000000000001" in the browser
    And I open the "Files" tab
    When I click the "Move cnp.pdf" button
    And I click the "Cancel" button
    Then the page does not show the text "Move cnp.pdf to which work?"
    And the file "library/Ibryam, Bilgin/Kubernetes Patterns (2019)/cnp.pdf" exists

  Scenario: A work is created from a file with the given title
    Given I open "/library" in the browser
    When I click the "New work" button
    And I choose the file "upload/draft.epub" in the "File of the new work" field
    And I fill the "Title" field with "Real Title"
    And I fill the "Author" field with "Jane Doe"
    And I click the "Create work" button
    Then the page shows the text "Real Title"
    And the file "library/Doe, Jane/Real Title/draft.epub" exists
