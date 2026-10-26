@ui @PRD-8.2 @F-LIB-6 @M4
Feature: A book and its details
  As a person who looks at one book
  I want to read it, download it, mark it and correct its details
  So that the library says what I know about the book

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

  Scenario: The details of a book
    When I open "/works/01J9Z3K0000000000000000001" in the browser
    Then the page shows the text "Kubernetes Patterns"
    And the page shows the text "Reusable Elements"
    And the page shows the text "Bilgin Ibryam"
    And the page shows the text "Work ID"
    And the page shows the text "01J9Z3K0000000000000000001"
    And the page shows the text "2019"
    And the page shows the text "Design patterns for containers"
    And the page shows the text "cloud/kubernetes"
    And the page shows the text "devops"
    And the page shows the text "kp.epub"

  Scenario: The table of contents leads into the book
    When I open "/works/01J9Z3K0000000000000000001" in the browser
    Then the link "Foundational Patterns" points to an address containing "/read/01J9Z3K0000000000000000001~kp.epub#cfi="

  Scenario Outline: Read opens the book in the format that suits a reader best
    When I open "/works/<id>" in the browser
    Then the link "Read" points to an address containing "<address>"

    Examples:
      | id                         | address                                         |
      | 01J9Z3K0000000000000000001 | /read/01J9Z3K0000000000000000001~kp.epub        |
      | 01J9Z3K0000000000000000002 | /read/01J9Z3K0000000000000000002~es.pdf         |

  Scenario: Every file can be downloaded
    When I open "/works/01J9Z3K0000000000000000001" in the browser
    Then the link "Download kp.epub" points to an address containing "/api/v1/files/01J9Z3K0000000000000000001~kp.epub?download=true"

  Scenario: The cover is shown
    When I open "/works/01J9Z3K0000000000000000002" in the browser
    Then the book "Event Sourcing in Practice" shows its cover

  Scenario: The reading status is set and kept
    Given I open "/works/01J9Z3K0000000000000000002" in the browser
    And the "Unread" button is pressed
    When I click the "Reading" button
    Then the "Reading" button is pressed
    And the "Unread" button is not pressed
    When I reload the page
    Then the "Reading" button is pressed
    And the file "library/Alander/Event Sourcing in Practice/book.json" contains "reading"

  Scenario: A rating is given, changed and kept
    Given I open "/works/01J9Z3K0000000000000000002" in the browser
    When I click the "4 stars" button
    Then the "4 stars" button is pressed
    When I reload the page
    Then the "4 stars" button is pressed
    When I click the "2 stars" button
    Then the "2 stars" button is pressed
    And the "4 stars" button is not pressed

  Scenario: A tag is added and removed
    Given I open "/works/01J9Z3K0000000000000000001" in the browser
    When I fill the "Add a tag" field with "to-read"
    And I press Enter in the "Add a tag" field
    Then the page shows the text "to-read"
    When I reload the page
    Then the page shows the text "to-read"
    When I click the "Remove tag to-read" button
    Then the page does not show the text "to-read"
    When I reload the page
    Then the page does not show the text "to-read"

  Scenario: A tag that is not valid is explained
    Given I open "/works/01J9Z3K0000000000000000001" in the browser
    When I fill the "Add a tag" field with "a//b"
    And I press Enter in the "Add a tag" field
    Then an alert says "tag 'a//b'"
    And the page does not show the text "Remove tag a//b"

  Scenario: The details are edited
    Given I open "/works/01J9Z3K0000000000000000001" in the browser
    When I click the "Edit details" button
    And I fill the "Title" field with "Kubernetes in Depth"
    And I fill the "Year" field with "2024"
    And I click the "Save" button
    Then the heading "Kubernetes in Depth" is shown
    And the page shows the text "2024"
    And the file "library/Ibryam/Kubernetes Patterns/book.json" contains "Kubernetes in Depth"

  Scenario: The cite key is edited with the other details
    Given I open "/works/01J9Z3K0000000000000000001" in the browser
    When I click the "Edit details" button
    And I fill the "Cite key" field with "ibryam2019"
    And I click the "Save" button
    Then the page shows the text "Cite key"
    And the page shows the text "ibryam2019"
    And the file "library/Ibryam/Kubernetes Patterns/book.json" contains "ibryam2019"

  Scenario: A cite key is generated from author, year and title
    Given I open "/works/01J9Z3K0000000000000000001" in the browser
    When I click the "Edit details" button
    And I click the "Generate cite key" button
    Then the "Cite key" field has the value "ibryam2019kubernetes"
    When I click the "Save" button
    Then the page shows the text "ibryam2019kubernetes"

  Scenario: A cite key that BibTeX cannot use is explained and nothing is lost
    Given I open "/works/01J9Z3K0000000000000000001" in the browser
    When I click the "Edit details" button
    And I fill the "Cite key" field with "ibryam 2019"
    And I click the "Save" button
    Then an alert says "citeKey: may only contain letters, digits and - _ : . + /"
    And the "Cite key" field has the value "ibryam 2019"

  Scenario: The authors are edited one to a line
    Given I open "/works/01J9Z3K0000000000000000001" in the browser
    When I click the "Edit details" button
    And I fill the "Authors (one to a line)" field with "Bilgin Ibryam\nRoland Huß"
    And I click the "Save" button
    Then the page shows the text "Roland Huß"

  Scenario: A detail can be removed
    Given I open "/works/01J9Z3K0000000000000000001" in the browser
    When I click the "Edit details" button
    And I fill the "Subtitle" field with ""
    And I click the "Save" button
    Then the page does not show the text "Reusable Elements"

  Scenario: Editing can be cancelled
    Given I open "/works/01J9Z3K0000000000000000001" in the browser
    When I click the "Edit details" button
    And I fill the "Title" field with "Something else"
    And I click the "Cancel" button
    Then the heading "Kubernetes Patterns" is shown
    And the page does not show the text "Something else"

  Scenario: A value that the server refuses is explained and nothing is lost
    Given I open "/works/01J9Z3K0000000000000000001" in the browser
    When I click the "Edit details" button
    And I fill the "Title" field with "Changed"
    And I fill the "Year" field with "0"
    And I click the "Save" button
    Then an alert says "year: must be between 1 and 9999"
    And the "Title" field has the value "Changed"

  Scenario: A book that is not in the library
    When I open "/works/01J9Z3K0000000000000000009" in the browser
    Then the page shows the text "This book is not in the library"
    And the link "Back to the library" points to an address containing "/library"
