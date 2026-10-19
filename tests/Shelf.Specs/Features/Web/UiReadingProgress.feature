@ui @F-RDR-4 @F-RDR-5 @M5
Feature: A book opens where I stopped
  As a person who reads in the browser
  I want the reader to remember my place in each file
  So that I can close the book and come back later

  Background:
    Given an empty working directory
    And I have run shelf with arguments "init library"
    And a sample EPUB "library/Ibryam/Kubernetes Patterns/kp.epub" with the title "Kubernetes Patterns", the authors "Bilgin Ibryam" and the chapters:
      | title                 | text                                                                                                    |
      | Foundational Patterns | The sidecar container extends the main container. Health probes tell the platform when a pod is alive. |
      | Behavioral Patterns   | A batch job runs to completion. A daemon service runs on every node of the cluster.                     |
    And a sample PDF "library/Ibryam/Kubernetes Patterns/kp.pdf" with the title "Kubernetes Patterns", the author "Bilgin Ibryam" and the pages:
      | text                                                |
      | The sidecar container extends the main container.   |
      | A batch job runs to completion.                     |
    And the sidecar "library/Ibryam/Kubernetes Patterns/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000001", "title": "Kubernetes Patterns", "authors": ["Bilgin Ibryam"], "language": "en", "year": 2019, "files": [{ "name": "kp.epub" }, { "name": "kp.pdf" }] }
      """
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

  Scenario: A PDF is saved when the reader is left, and opens there next time
    Given I open "/read/01J9Z3K0000000000000000002~es.pdf" in the browser
    And the reader is at page 1 of 2
    When I click the "Next page" button
    Then the reader is at page 2 of 2
    When I click the "Back to the book" link
    Then the file "library/Alander/Event Sourcing in Practice/book.json" soon contains "page=2"
    When I open "/read/01J9Z3K0000000000000000002~es.pdf" in the browser
    Then the reader is at page 2 of 2
    And the reader shows the text "A projection builds a read model"

  Scenario: An EPUB is saved when the reader is left, and opens there next time
    Given I open "/read/01J9Z3K0000000000000000001~kp.epub" in the browser
    And the reader shows the text "The sidecar container extends the main container"
    When I click the "Next page" button
    Then the reader shows the text "A daemon service runs on every node"
    And the browser address contains "#cfi=epubcfi(/6/4"
    When I click the "Back to the book" link
    Then the file "library/Ibryam/Kubernetes Patterns/book.json" soon contains "epubcfi(/6/4"
    When I open "/read/01J9Z3K0000000000000000001~kp.epub" in the browser
    Then the reader shows the text "A daemon service runs on every node"

  Scenario: A location in the address wins over the saved position
    Given I open "/read/01J9Z3K0000000000000000002~es.pdf#page=2" in the browser
    And the reader is at page 2 of 2
    And I click the "Back to the book" link
    When I open "/read/01J9Z3K0000000000000000002~es.pdf#page=1" in the browser
    Then the reader is at page 1 of 2

  Scenario: The position is saved when the tab is reloaded
    Given I open "/read/01J9Z3K0000000000000000002~es.pdf#page=2" in the browser
    And the reader is at page 2 of 2
    When I reload the page
    Then the reader is at page 2 of 2
    And the file "library/Alander/Event Sourcing in Practice/book.json" soon contains "page=2"

  Scenario: Each file keeps its own position
    Given I open "/read/01J9Z3K0000000000000000001~kp.pdf#page=2" in the browser
    And the reader is at page 2 of 2
    And I click the "Back to the book" link
    When I open "/read/01J9Z3K0000000000000000001~kp.epub" in the browser
    Then the reader shows the text "The sidecar container extends the main container"

  Scenario: The book page offers to continue where a person stopped
    Given I open "/read/01J9Z3K0000000000000000002~es.pdf#page=2" in the browser
    And the reader is at page 2 of 2
    And I click the "Back to the book" link
    Then the link "Continue reading" points to an address containing "/read/01J9Z3K0000000000000000002~es.pdf"
    And the page shows the text "Page 2"

  Scenario: Without a position the book page says Read
    When I open "/works/01J9Z3K0000000000000000002" in the browser
    Then the link "Read" points to an address containing "/read/01J9Z3K0000000000000000002~es.pdf"

  Scenario: Read opens the preferred format
    When I open "/works/01J9Z3K0000000000000000001" in the browser
    Then the link "Read" points to an address containing "/read/01J9Z3K0000000000000000001~kp.epub"
