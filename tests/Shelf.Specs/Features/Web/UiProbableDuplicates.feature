@ui @F-DUP-3 @F-DUP-6 @M5
Feature: Probable duplicates in the browser
  As a person whose library holds one book under two titles
  I want to see the works that probably are one book and say that they are not
  So that I can check them and the answer sticks

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
    And the web server is running on the library "library"

  Scenario: The probable pair is shown with both works and the reason
    When I open "/duplicates" in the browser
    Then the page shows the text "Probable duplicates"
    And the page shows the text "1 pair of works that probably are one book"
    And the page shows the text "Same text (distance 0)"
    And the page shows the text "Domain-Driven Design"
    And the page shows the text "DDD Quickly"
    And the page does not show the text "Kubernetes Patterns"

  Scenario: There are no duplicates of files, so that is not said next to the pair
    When I open "/duplicates" in the browser
    Then the page does not show the text "No duplicates"

  Scenario: The works can be opened to check them
    When I open "/duplicates" in the browser
    Then the link "Domain-Driven Design" points to an address containing "/works/01J9Z3K1000000000000000001"
    And the link "DDD Quickly" points to an address containing "/works/01J9Z3K2000000000000000002"

  Scenario: A pair is marked as not a duplicate; nothing moves and the pair is gone
    Given I open "/duplicates" in the browser
    When I click the "Not a duplicate" button
    Then the page shows the text "Marked as not duplicates."
    And the page does not show the text "Probable duplicates"
    And the page shows the text "No duplicates"
    And the file "library/Evans, Eric/Domain-Driven Design (2003)/ddd.epub" exists
    And the file "library/Avram, Abel/DDD Quickly/ddd-quickly.epub" exists

  Scenario: The mark is still there after a reload
    Given I open "/duplicates" in the browser
    And I click the "Not a duplicate" button
    Then the page shows the text "Marked as not duplicates."
    When I reload the page
    Then the page does not show the text "Probable duplicates"
