@ui @F-RDR-1 @F-RDR-3 @M4
Feature: Reading a PDF in the browser
  As a person who reads a PDF from my library
  I want to turn pages, zoom, search and use the outline
  So that I do not need another program to read it

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

  Scenario: A PDF opens at its first page
    When I open "/read/01J9Z3K0000000000000000002~es.pdf" in the browser
    Then the reader is at page 1 of 2
    And the reader shows the text "Event sourcing stores every change as an event"
    And the browser reported no errors

  Scenario: A PDF opens at the page of the address
    When I open "/read/01J9Z3K0000000000000000002~es.pdf#page=2" in the browser
    Then the reader is at page 2 of 2
    And the reader shows the text "A projection builds a read model"

  Scenario: The page can be turned, and the address follows
    Given I open "/read/01J9Z3K0000000000000000002~es.pdf" in the browser
    When I click the "Next page" button
    Then the reader is at page 2 of 2
    And the browser address contains "#page=2"
    When I click the "Previous page" button
    Then the reader is at page 1 of 2
    And the browser address contains "#page=1"

  Scenario: A page number can be typed
    Given I open "/read/01J9Z3K0000000000000000002~es.pdf" in the browser
    And the reader is at page 1 of 2
    When I fill the "Page" field with "2"
    And I press Enter in the "Page" field
    Then the reader is at page 2 of 2
    And the reader shows the text "A projection builds a read model"

  Scenario: A page that does not exist is not opened
    When I open "/read/01J9Z3K0000000000000000002~es.pdf#page=99" in the browser
    Then the reader is at page 2 of 2

  Scenario: The address of the page can be opened again and again
    Given I open "/read/01J9Z3K0000000000000000002~es.pdf" in the browser
    And I click the "Next page" button
    When I open the current address in a new browser
    Then the reader is at page 2 of 2

  Scenario: The zoom can be changed and reset
    Given I open "/read/01J9Z3K0000000000000000002~es.pdf" in the browser
    And the reader is at page 1 of 2
    And I note the width of the PDF page
    When I click the "Zoom in" button
    Then the PDF page is wider than noted
    And the page shows the text "Zoom"
    When I click the "Fit width" button
    Then the page shows the text "Fit width"
    When I click the "Zoom out" button
    Then the PDF page is narrower than the width of the window

  Scenario: A word is searched in the text
    Given I open "/read/01J9Z3K0000000000000000002~es.pdf" in the browser
    When I fill the "Search in this book" field with "projection"
    And I press Enter in the "Search in this book" field
    Then the page shows the text "1 match"
    And the reader highlights "projection"
    And the reader is at page 2 of 2

  Scenario: The matches can be stepped through
    Given I open "/read/01J9Z3K0000000000000000002~es.pdf" in the browser
    When I fill the "Search in this book" field with "event"
    And I press Enter in the "Search in this book" field
    Then the page shows the text "1 of 4 matches"
    When I click the "Next match" button
    Then the page shows the text "2 of 4 matches"
    When I click the "Next match" button
    Then the page shows the text "3 of 4 matches"
    When I click the "Next match" button
    Then the page shows the text "4 of 4 matches"
    And the reader is at page 2 of 2
    When I click the "Previous match" button
    Then the page shows the text "3 of 4 matches"
    And the reader is at page 1 of 2

  Scenario: A word that is not in the text
    Given I open "/read/01J9Z3K0000000000000000002~es.pdf" in the browser
    When I fill the "Search in this book" field with "zzzzzzzz"
    And I press Enter in the "Search in this book" field
    Then the page shows the text "Nothing found for zzzzzzzz"

  Scenario: The outline of a paper leads to its pages
    Given I open "/read/01J9Z3K0000000000000000003~paper.pdf" in the browser
    And the reader is at page 1 of 7
    When I click the "Contents" button
    Then the panel "Contents" shows the entry "1. Introduction"
    And the panel "Contents" shows the entry "3. Polynomial Dynamical Systems in the Plane"
    When I click the entry "3. Polynomial Dynamical Systems in the Plane" in the panel "Contents"
    Then the reader is at page 4 of 7
    And the browser address contains "#page=4"

  Scenario: A PDF without an outline has no contents
    Given I open "/read/01J9Z3K0000000000000000002~es.pdf" in the browser
    Then the "Contents" button is not shown

  Scenario: The way back to the book
    Given I open "/read/01J9Z3K0000000000000000002~es.pdf" in the browser
    When I click the "Back to the book" link
    Then the browser address contains "/works/01J9Z3K0000000000000000002"
    And the page shows the text "Event Sourcing in Practice"

  Scenario: A file that is not in the library
    When I open "/read/01J9Z3K0000000000000000002~nothing.pdf" in the browser
    Then the page shows the text "This file is not in the library"
    And the link "Back to the library" points to an address containing "/library"

  Scenario: A book that is not in the library
    When I open "/read/01J9Z3K0000000000000000009~es.pdf" in the browser
    Then the page shows the text "This file is not in the library"
