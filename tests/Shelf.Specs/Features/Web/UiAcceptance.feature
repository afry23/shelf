@ui @AC-5 @F-RDR-3 @M4
Feature: From a search hit to the place in the book
  As a person who remembers a passage from a book
  I want a click on a search hit to open the book at that place
  So that I do not have to find the page myself

  Background:
    Given an empty working directory
    And I have run shelf with arguments "init library"
    And a sample EPUB "library/Ibryam/Kubernetes Patterns/kp.epub" with the title "Kubernetes Patterns", the authors "Bilgin Ibryam" and the chapters:
      | title                 | text                                                                                                    |
      | Foundational Patterns | The sidecar container extends the main container. Health probes tell the platform when a pod is alive. |
      | Behavioral Patterns   | A batch job runs to completion. A daemon service runs on every node of the cluster.                     |
    And the sidecar "library/Ibryam/Kubernetes Patterns/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000001", "title": "Kubernetes Patterns", "authors": ["Bilgin Ibryam"], "language": "en", "year": 2019, "files": [{ "name": "kp.epub" }] }
      """
    And a sample PDF "library/Alander/Event Sourcing in Practice/es.pdf" with the title "Event Sourcing in Practice", the author "Jördis Ålander" and the pages:
      | text                                                                                              |
      | Event sourcing stores every change as an event. Aggregates replay events to rebuild their state.  |
      | A projection builds a read model from the event stream. Kubernetes is only mentioned once here.  |
    And the sidecar "library/Alander/Event Sourcing in Practice/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000002", "title": "Event Sourcing in Practice", "authors": ["Jördis Ålander"], "language": "en", "year": 2021, "files": [{ "name": "es.pdf" }] }
      """
    And a work folder "library/Andersen/Hilbert" with the fixture files "paper.pdf=upstream/pdf/arxiv-2403.02174v1.pdf" and the sidecar:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000003", "title": "On Hilbert's 16th Problem", "authors": ["Lars Andersen"], "language": "en", "year": 2024, "files": [{ "name": "paper.pdf" }] }
      """
    And I have run shelf with arguments "reindex --library library"
    And the web server is running on the library "library"

  Scenario: A hit in a PDF opens the reader at its page
    Given I open "/search?q=projection" in the browser
    When I click the "Open at page 2" link
    Then the browser address contains "/read/01J9Z3K0000000000000000002~es.pdf#page=2"
    And the reader is at page 2 of 2
    And the reader shows the text "A projection builds a read model"

  Scenario: A hit on the first page of a PDF opens the first page
    Given I open "/search?q=aggregates" in the browser
    When I click the "Open at page 1" link
    Then the reader is at page 1 of 2
    And the reader shows the text "Aggregates replay events"

  Scenario: A hit in an EPUB opens the reader at its section
    Given I open "/search?q=daemon" in the browser
    When I click the "Open at section" link
    Then the browser address contains "/read/01J9Z3K0000000000000000001~kp.epub#cfi=epubcfi("
    And the reader shows the text "A daemon service runs on every node"

  Scenario: The address of the place can be opened in another browser
    Given I open "/search?q=daemon" in the browser
    And I click the "Open at section" link
    And the reader shows the text "A daemon service runs on every node"
    When I open the current address in a new browser
    Then the reader shows the text "A daemon service runs on every node"

  Scenario: The contents of the book page lead into the reader
    Given I open "/works/01J9Z3K0000000000000000001" in the browser
    When I click the "Behavioral Patterns" link
    Then the browser address contains "/read/01J9Z3K0000000000000000001~kp.epub#cfi="
    And the reader shows the text "A daemon service runs on every node"

  Scenario: The outline of a paper on the book page leads to its page
    Given I open "/works/01J9Z3K0000000000000000003" in the browser
    When I click the "3. Polynomial Dynamical Systems in the Plane" link
    Then the reader is at page 4 of 7

  Scenario: Back from the reader to the search
    Given I open "/search?q=projection" in the browser
    And I click the "Open at page 2" link
    And the reader is at page 2 of 2
    When I go back in the browser
    Then the list "Results" has 2 items
    And the first item of the list "Results" is "Event Sourcing in Practice"

  Scenario: Read opens the reader at the beginning
    Given I open "/works/01J9Z3K0000000000000000002" in the browser
    When I click the "Read" link
    Then the reader is at page 1 of 2
