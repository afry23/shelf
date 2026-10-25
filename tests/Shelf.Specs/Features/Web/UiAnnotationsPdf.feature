@ui @F-ANN-1 @F-ANN-2 @F-ANN-4 @M6
Feature: Highlights and notes while reading a PDF
  As a person who reads in the browser
  I want to mark a passage on a page of a PDF and keep a note with it
  So that I find the passage again, in the reader and on the book page

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

  Scenario: A selected passage is highlighted on the page and kept with its place
    Given I open "/read/01J9Z3K0000000000000000002~es.pdf" in the browser
    And the reader is at page 1 of 2
    When I select the text "stores every change" on the PDF page
    And I click the "Highlight" button
    Then the PDF page shows 1 highlight
    And the file "library/Alander/Event Sourcing in Practice/annotations.json" soon contains "page=1"
    And the file "library/Alander/Event Sourcing in Practice/annotations.json" soon contains "stores every change"
    And the file "library/Alander/Event Sourcing in Practice/annotations.json" soon contains "rects"

  Scenario: A highlight is drawn again when the file is opened
    Given I open "/read/01J9Z3K0000000000000000002~es.pdf" in the browser
    And the reader is at page 1 of 2
    And I select the text "Aggregates replay" on the PDF page
    And I click the "Highlight" button
    And the file "library/Alander/Event Sourcing in Practice/annotations.json" soon contains "Aggregates replay"
    When I open "/read/01J9Z3K0000000000000000002~es.pdf" in the browser
    Then the PDF page shows 1 highlight

  Scenario: A note on a highlight is shown in the panel, and the panel goes to its page
    Given I open "/read/01J9Z3K0000000000000000002~es.pdf#page=2" in the browser
    And the reader is at page 2 of 2
    And I select the text "builds a read model" on the PDF page
    And I click the "Highlight" button
    And I click the "Notes" button
    And I click the "Add note" button
    And I fill the "Note" field with "A **projection** is a view."
    And I click the "Save note" button
    And the file "library/Alander/Event Sourcing in Practice/annotations.json" soon contains "A **projection** is a view."
    When I click the "Previous page" button
    Then the reader is at page 1 of 2
    When I click the entry "builds a read model" in the panel "Notes"
    Then the reader is at page 2 of 2

  Scenario: A highlight can be removed
    Given I open "/read/01J9Z3K0000000000000000002~es.pdf" in the browser
    And the reader is at page 1 of 2
    And I select the text "stores every change" on the PDF page
    And I click the "Highlight" button
    And the file "library/Alander/Event Sourcing in Practice/annotations.json" soon contains "stores every change"
    When I click the "Notes" button
    And I click the "Delete highlight" button
    Then the PDF page shows 0 highlights
    And the file "library/Alander/Event Sourcing in Practice/annotations.json" soon does not exist
