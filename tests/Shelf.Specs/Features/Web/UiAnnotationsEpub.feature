@ui @F-ANN-1 @F-ANN-2 @F-ANN-4 @M6
Feature: Highlights and notes while reading an EPUB
  As a person who reads in the browser
  I want to mark a passage, write a note in Markdown and find both again
  So that I keep what I learned from a book

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
    And I have run shelf with arguments "reindex --library library"
    And the web server is running on the library "library"

  Scenario: A selected passage is highlighted and kept in annotations.json
    Given I open "/read/01J9Z3K0000000000000000001~kp.epub" in the browser
    And the reader shows the text "The sidecar container extends the main container"
    When I select the text "sidecar container extends" in the book
    And I click the "Highlight" button
    Then the book shows 1 highlight
    And the file "library/Ibryam/Kubernetes Patterns/annotations.json" soon contains "sidecar container extends"
    And the file "library/Ibryam/Kubernetes Patterns/annotations.json" soon contains "yellow"

  Scenario: A highlight is drawn again when the book is opened
    Given I open "/read/01J9Z3K0000000000000000001~kp.epub" in the browser
    And the reader shows the text "The sidecar container extends the main container"
    And I select the text "Health probes" in the book
    And I click the "Highlight" button
    And the file "library/Ibryam/Kubernetes Patterns/annotations.json" soon contains "Health probes"
    When I open "/read/01J9Z3K0000000000000000001~kp.epub" in the browser
    Then the book shows 1 highlight

  Scenario: A note in Markdown is saved and shown rendered in the notes panel
    Given I open "/read/01J9Z3K0000000000000000001~kp.epub" in the browser
    And the reader shows the text "The sidecar container extends the main container"
    And I select the text "sidecar container extends" in the book
    And I click the "Highlight" button
    When I click the "Notes" button
    And I click the "Add note" button
    And I fill the "Note" field with "A **sidecar** shares the pod."
    And I click the "Save note" button
    Then the panel "Notes" shows the entry "sidecar"
    And the rendered note shows "sidecar" in bold
    And the file "library/Ibryam/Kubernetes Patterns/annotations.json" soon contains "A **sidecar** shares the pod."

  Scenario: A note for the whole file needs no selection
    Given I open "/read/01J9Z3K0000000000000000001~kp.epub" in the browser
    And the reader shows the text "The sidecar container extends the main container"
    When I click the "Notes" button
    And I click the "Add a note for this file" button
    And I fill the "Note" field with "Read again before the workshop."
    And I click the "Save note" button
    Then the panel "Notes" shows the entry "Read again before the workshop."
    And the file "library/Ibryam/Kubernetes Patterns/annotations.json" soon contains "Read again before the workshop."

  Scenario: Markdown in a note cannot run a script
    Given I open "/read/01J9Z3K0000000000000000001~kp.epub" in the browser
    And the reader shows the text "The sidecar container extends the main container"
    When I click the "Notes" button
    And I click the "Add a note for this file" button
    And I fill the "Note" field with "<img src=x onerror=document.title='pwned'> **fine**"
    And I click the "Save note" button
    Then the panel "Notes" shows the entry "fine"
    And the title of the page is not "pwned"
    And the browser reported no errors

  Scenario: A highlight can be removed
    Given I open "/read/01J9Z3K0000000000000000001~kp.epub" in the browser
    And the reader shows the text "The sidecar container extends the main container"
    And I select the text "sidecar container extends" in the book
    And I click the "Highlight" button
    And the file "library/Ibryam/Kubernetes Patterns/annotations.json" soon contains "sidecar container extends"
    When I click the "Notes" button
    And I click the "Delete highlight" button
    Then the book shows 0 highlights
    And the file "library/Ibryam/Kubernetes Patterns/annotations.json" soon does not exist

  Scenario: The highlights and notes are listed on the page of the book
    Given I send "POST /api/v1/files/01J9Z3K0000000000000000001~kp.epub/annotations" with the JSON:
      """
      { "kind": "note", "note": "Mind the workshop" }
      """
    And I open "/works/01J9Z3K0000000000000000001" in the browser
    When I open the "Highlights and notes" tab
    Then the page shows the text "Highlights and notes"
    And the page shows the text "Mind the workshop"
