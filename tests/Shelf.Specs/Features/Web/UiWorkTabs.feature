@ui @PRD-8.2 @F-LIB-6 @F-ANN-4 @M4
Feature: The page of a book in tabs
  As a person who looks at one book
  I want its contents, notes, files and tools in tabs under the details
  So that I find what I look for without scrolling past the rest

  Background:
    Given an empty working directory
    And I have run shelf with arguments "init library"
    And a sample EPUB "library/Ibryam/Kubernetes Patterns/kp.epub" with the title "Kubernetes Patterns", the authors "Bilgin Ibryam" and the chapters:
      | title                 | text                                                                                                    |
      | Foundational Patterns | The sidecar container extends the main container. Health probes tell the platform when a pod is alive. |
      | Behavioral Patterns   | A batch job runs to completion. A daemon service runs on every node of the cluster.                     |
    And the sidecar "library/Ibryam/Kubernetes Patterns/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000001", "title": "Kubernetes Patterns", "subtitle": "Reusable Elements", "authors": ["Bilgin Ibryam"], "language": "en", "year": 2019, "description": "Design patterns for containers", "tags": ["cloud/kubernetes", "devops"], "readStatus": "read", "files": [{ "name": "kp.epub" }] }
      """
    And a sample PDF "library/Alander/Event Sourcing in Practice/es.pdf" with the title "Event Sourcing in Practice", the author "Jördis Ålander" and the pages:
      | text                                                                                              |
      | Event sourcing stores every change as an event. Aggregates replay events to rebuild their state.  |
      | A projection builds a read model from the event stream. Kubernetes is only mentioned once here.  |
    And the sidecar "library/Alander/Event Sourcing in Practice/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000002", "title": "Event Sourcing in Practice", "authors": ["Jördis Ålander"], "language": "en", "year": 2021, "tags": ["architektur/ddd", "cloud/azure"], "files": [{ "name": "es.pdf" }] }
      """
    And a sample EPUB "library/Weiss/Verteilte Systeme/vs.epub" with the title "Verteilte Systeme verstehen", the authors "Lukas Weiß" and the chapters:
      | title   | text                                                                        |
      | Konsens | Ein Konsensprotokoll sorgt dafür, dass alle Knoten denselben Zustand sehen. |
    And the sidecar "library/Weiss/Verteilte Systeme/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000003", "title": "Verteilte Systeme verstehen", "authors": ["Lukas Weiß"], "language": "de", "year": 2015, "tags": ["architektur/verteilte-systeme", "architektur/ddd"], "files": [{ "name": "vs.epub" }] }
      """
    And I have run shelf with arguments "reindex --library library"
    And the web server is running on the library "library"

  Scenario: The book opens on its contents
    When I open "/works/01J9Z3K0000000000000000001" in the browser
    Then the "Contents" tab is selected
    And the link "Foundational Patterns" points to an address containing "/read/01J9Z3K0000000000000000001~kp.epub#cfi="

  Scenario: The details of the book stay in view whichever tab is open
    Given I open "/works/01J9Z3K0000000000000000001" in the browser
    When I open the "Manage" tab
    Then the page shows the text "Design patterns for containers"
    And the page shows the text "cloud/kubernetes"
    And the link "Read" points to an address containing "/read/01J9Z3K0000000000000000001~kp.epub"

  Scenario: The tabs of a book are named after what they hold
    When I open "/works/01J9Z3K0000000000000000001" in the browser
    Then the "Contents" tab is shown
    And the "Files" tab is shown
    And the "Manage" tab is shown

  Scenario: The number of files is shown on the tab
    When I open "/works/01J9Z3K0000000000000000001" in the browser
    Then the tab "Files" shows the number 1

  Scenario: A book without a table of contents opens on its files
    When I open "/works/01J9Z3K0000000000000000002" in the browser
    Then the "Contents" tab is not shown
    And the "Files" tab is selected
    And the page shows the text "es.pdf"

  Scenario: A book without notes has no tab for them
    When I open "/works/01J9Z3K0000000000000000001" in the browser
    Then the "Highlights and notes" tab is not shown

  Scenario: The notes of a book have a tab of their own
    Given I send "POST /api/v1/files/01J9Z3K0000000000000000001~kp.epub/annotations" with the JSON:
      """
      { "kind": "note", "note": "Mind the workshop" }
      """
    When I open "/works/01J9Z3K0000000000000000001" in the browser
    Then the page does not show the text "Mind the workshop"
    When I open the "Highlights and notes" tab
    Then the page shows the text "Mind the workshop"
    And the tab "Highlights and notes" shows the number 1

  Scenario: The tools are in the Manage tab
    Given I open "/works/01J9Z3K0000000000000000001" in the browser
    When I open the "Manage" tab
    Then the page shows the text "Merge another work"
    And the page shows the text "Export"
    And the "Delete this work" button is shown

  Scenario: Loading details from the internet sits with editing the details
    When I open "/works/01J9Z3K0000000000000000001" in the browser
    Then "Edit details" stands beside "Load details from the internet"

  Scenario: The tab is kept in the address
    Given I open "/works/01J9Z3K0000000000000000001" in the browser
    When I open the "Files" tab
    Then the browser address contains "#files"
    When I reload the page
    Then the "Files" tab is selected

  Scenario: An address with a tab opens that tab
    When I open "/works/01J9Z3K0000000000000000001#manage" in the browser
    Then the "Manage" tab is selected

  Scenario: The arrow keys move between the tabs
    Given I open "/works/01J9Z3K0000000000000000001" in the browser
    When I press the key "ArrowRight" on the "Contents" tab
    Then the "Files" tab is selected
    When I press the key "ArrowRight" on the "Files" tab
    Then the "Manage" tab is selected

  Scenario: The tab stays when the details change
    Given I open "/works/01J9Z3K0000000000000000001" in the browser
    And I open the "Files" tab
    When I click the "Edit details" button
    And I fill the "Year" field with "2024"
    And I click the "Save" button
    Then the "Files" tab is selected

  Scenario: The tabs stay within a phone
    Given the browser window is 390 x 844 pixels
    When I open "/works/01J9Z3K0000000000000000001" in the browser
    Then the page has no sideways scroll
    When I open the "Manage" tab
    Then the page has no sideways scroll
