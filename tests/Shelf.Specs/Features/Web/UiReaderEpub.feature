@ui @F-RDR-2 @F-RDR-3 @M4
Feature: Reading an EPUB in the browser
  As a person who reads an EPUB from my library
  I want the text to reflow, to choose its size and colors, and to find my way in it
  So that the book is pleasant to read on any screen

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

  Scenario: An EPUB opens at its beginning
    When I open "/read/01J9Z3K0000000000000000001~kp.epub" in the browser
    Then the reader shows the text "The sidecar container extends the main container"
    And the browser reported no errors

  Scenario: The contents lead to a chapter
    Given I open "/read/01J9Z3K0000000000000000001~kp.epub" in the browser
    When I click the "Contents" button
    Then the panel "Contents" shows the entry "Behavioral Patterns"
    When I click the entry "Behavioral Patterns" in the panel "Contents"
    Then the reader shows the text "A daemon service runs on every node"
    And the browser address contains "#cfi=epubcfi("

  Scenario: The pages can be turned
    Given I open "/read/01J9Z3K0000000000000000001~kp.epub" in the browser
    And the reader shows the text "The sidecar container extends the main container"
    When I click the "Next page" button
    Then the reader shows the text "A daemon service runs on every node"
    When I click the "Previous page" button
    Then the reader shows the text "The sidecar container extends the main container"

  Scenario: The position in the book is shown
    Given I open "/read/01J9Z3K0000000000000000001~kp.epub" in the browser
    Then the page shows the text "%"

  Scenario: The text can be made larger and smaller
    Given I open "/read/01J9Z3K0000000000000000001~kp.epub" in the browser
    And the reader shows the text "The sidecar container extends the main container"
    And the text in the book is 100 percent
    When I click the "Larger text" button
    Then the page shows the text "Text 110%"
    And the text in the book is 110 percent
    When I click the "Smaller text" button
    And I click the "Smaller text" button
    Then the page shows the text "Text 90%"
    And the text in the book is 90 percent

  Scenario: The size of the text has limits
    Given I open "/read/01J9Z3K0000000000000000001~kp.epub" in the browser
    When I click the "Larger text" button 20 times
    Then the page shows the text "Text 200%"
    And the "Larger text" button is disabled

  Scenario Outline: The colors of the page can be chosen
    Given I open "/read/01J9Z3K0000000000000000001~kp.epub" in the browser
    And the reader shows the text "The sidecar container extends the main container"
    When I click the "<theme>" button
    Then the "<theme>" button is pressed
    And the page of the book is <tone>

    Examples:
      | theme | tone  |
      | Light | light |
      | Dark  | dark  |
      | Sepia | light |

  Scenario: The theme starts from the system
    Given the browser prefers a dark color scheme
    When I open "/read/01J9Z3K0000000000000000001~kp.epub" in the browser
    Then the "Dark" button is pressed
    And the page of the book is dark

  Scenario: A word is searched in the book
    Given I open "/read/01J9Z3K0000000000000000001~kp.epub" in the browser
    When I fill the "Search in this book" field with "daemon"
    And I press Enter in the "Search in this book" field
    Then the page shows the text "1 match"
    And the panel "Search results" shows the entry "daemon service"
    When I click the entry "daemon service" in the panel "Search results"
    Then the reader shows the text "A daemon service runs on every node"

  Scenario: A word that is not in the book
    Given I open "/read/01J9Z3K0000000000000000001~kp.epub" in the browser
    When I fill the "Search in this book" field with "zzzzzzzz"
    And I press Enter in the "Search in this book" field
    Then the page shows the text "Nothing found for zzzzzzzz"

  Scenario: A script in a book does not run
    Given an EPUB "library/Evil/Book/evil.epub" whose chapter runs a script that marks the page
    And the sidecar "library/Evil/Book/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000008", "title": "Evil", "authors": ["Eve"], "files": [{ "name": "evil.epub" }] }
      """
    And I have run shelf with arguments "reindex --library library"
    When I open "/read/01J9Z3K0000000000000000008~evil.epub" in the browser
    Then the reader shows the text "The chapter is shown"
    And the script of the book did not run
    And the browser reported "Content Security Policy"

  Scenario: A book whose file is not an EPUB is explained
    Given a work folder "library/Broken/Book" with the files "broken.epub" and the sidecar:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000007", "title": "Broken", "authors": ["Bea"], "files": [{ "name": "broken.epub" }] }
      """
    And I have tried shelf with arguments "reindex --library library"
    When I open "/read/01J9Z3K0000000000000000007~broken.epub" in the browser
    Then an alert says "could not be opened"
    And the link "Download broken.epub" points to an address containing "/api/v1/files/"
