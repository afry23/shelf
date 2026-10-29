@ui @F-RDR-6 @F-ANN-4 @M10
Feature: Bookmarks while reading
  As a person who reads in the browser
  I want to set a bookmark on the page I am at and jump back to it
  So that I find my places in a book without marking text

  Background:
    Given an empty working directory
    And I have run shelf with arguments "init library"
    And a sample PDF "library/Alander/Event Sourcing in Practice/es.pdf" with the title "Event Sourcing in Practice", the author "Jördis Ålander" and the pages:
      | text                                                                                              |
      | Event sourcing stores every change as an event. Aggregates replay events to rebuild their state.  |
      | A projection builds a read model from the event stream. Kubernetes is only mentioned once here.  |
    And the sidecar "library/Alander/Event Sourcing in Practice/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000002", "title": "Event Sourcing in Practice", "authors": ["Jördis Ålander"], "language": "en", "year": 2021, "files": [{ "name": "es.pdf" }] }
      """
    And I have run shelf with arguments "reindex --library library"
    And the web server is running on the library "library"

  Scenario: A bookmark is set on the page and kept with its place
    Given I open "/read/01J9Z3K0000000000000000002~es.pdf#page=2" in the browser
    And the reader is at page 2 of 2
    When I click the "Add bookmark" button
    Then the file "library/Alander/Event Sourcing in Practice/annotations.json" soon contains "bookmark"
    And the file "library/Alander/Event Sourcing in Practice/annotations.json" soon contains "page=2"
    And the "Remove bookmark" button is shown

  Scenario: A page that has a bookmark offers to remove it
    Given I open "/read/01J9Z3K0000000000000000002~es.pdf#page=2" in the browser
    And the reader is at page 2 of 2
    And I click the "Add bookmark" button
    And the file "library/Alander/Event Sourcing in Practice/annotations.json" soon contains "page=2"
    When I click the "Previous page" button
    Then the "Add bookmark" button is shown
    When I click the "Next page" button
    Then the "Remove bookmark" button is shown
    When I click the "Remove bookmark" button
    Then the "Add bookmark" button is shown
    And the file "library/Alander/Event Sourcing in Practice/annotations.json" soon does not contain "page=2"

  Scenario: The panel lists the bookmarks and goes to their page
    Given I open "/read/01J9Z3K0000000000000000002~es.pdf#page=2" in the browser
    And the reader is at page 2 of 2
    And I click the "Add bookmark" button
    And the file "library/Alander/Event Sourcing in Practice/annotations.json" soon contains "page=2"
    And I click the "Previous page" button
    And the reader is at page 1 of 2
    When I click the "Notes" button
    Then the page shows the text "Page 2"
    When I click the entry "Page 2" in the panel "Notes"
    Then the reader is at page 2 of 2

  Scenario: The bookmarks of a book are on its page
    Given I send "POST /api/v1/files/01J9Z3K0000000000000000002~es.pdf/annotations" with the JSON:
      """
      { "kind": "bookmark", "locator": "page=2", "note": "Read model" }
      """
    When I open "/works/01J9Z3K0000000000000000002" in the browser
    And I open the "Highlights and notes" tab
    Then the page shows the text "Read model"
    And the link "Page 2" points to an address containing "/read/01J9Z3K0000000000000000002~es.pdf#page=2"
