@ui @F-ANN-8 @PRD-8.3 @M11
Feature: Copy a passage as a quote while reading
  As a person who reads in the browser
  I want to copy a selected passage as a quote with its citation
  So that I paste it into my text without typing the cite key and the page

  Background:
    Given an empty working directory
    And I have run shelf with arguments "init library"
    And a sample PDF "library/Alander/Event Sourcing in Practice/es.pdf" with the title "Event Sourcing in Practice", the author "Jördis Ålander" and the pages:
      | text                                                                                              |
      | Event sourcing stores every change as an event. Aggregates replay events to rebuild their state.  |
      | A projection builds a read model from the event stream. Kubernetes is only mentioned once here.  |
    And the sidecar "library/Alander/Event Sourcing in Practice/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000002", "title": "Event Sourcing in Practice", "authors": ["Jördis Ålander"], "language": "en", "year": 2021, "citeKey": "alander2021event", "files": [{ "name": "es.pdf" }] }
      """
    And a sample EPUB "library/Ibryam/Kubernetes Patterns/kp.epub" with the title "Kubernetes Patterns", the authors "Bilgin Ibryam" and the chapters:
      | title                 | text                                              |
      | Foundational Patterns | The sidecar container extends the main container. |
      | Behavioral Patterns   | A batch job runs to completion.                   |
    And the sidecar "library/Ibryam/Kubernetes Patterns/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000001", "title": "Kubernetes Patterns", "authors": ["Bilgin Ibryam"], "language": "en", "year": 2019, "files": [{ "name": "kp.epub" }] }
      """
    And I have run shelf with arguments "reindex --library library"
    And the web server is running on the library "library"

  Scenario: A selected passage of a PDF is copied as a quote with the page
    Given I open "/read/01J9Z3K0000000000000000002~es.pdf#page=2" in the browser
    And the reader is at page 2 of 2
    When I select the text "builds a read model" on the PDF page
    And I click the "Copy as quote" button
    Then the page shows the text "Quote copied"
    And the clipboard holds "> builds a read model\n\n[@alander2021event, p. 2]"

  Scenario: Copying a quote does not make a highlight
    Given I open "/read/01J9Z3K0000000000000000002~es.pdf" in the browser
    And the reader is at page 1 of 2
    When I select the text "stores every change" on the PDF page
    And I click the "Copy as quote" button
    Then the page shows the text "Quote copied"
    And the PDF page shows 0 highlights

  Scenario: A book without a cite key cannot be quoted
    Given I open "/read/01J9Z3K0000000000000000001~kp.epub" in the browser
    When I select the text "sidecar container" in the book
    Then the "Copy as quote" button is disabled
