@ui @F-WEB-5 @M5
Feature: Keyboard control
  As a person who keeps the hands on the keyboard
  I want "/" for the search, "j" and "k" to move and Enter to open
  So that I browse the library without the mouse

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

  Scenario: A slash goes to the search field
    Given I open "/library" in the browser
    When I press the key "/"
    Then the focus is in the search field

  Scenario: A slash typed in the search field is text
    Given I open "/library" in the browser
    And I press the key "/"
    When I press the key "/"
    Then the focus is in the search field

  Scenario: j and k move through the books
    Given I open "/library" in the browser
    Then the list "Books" has 3 items
    When I press the key "j"
    Then the focus is on the item "Event Sourcing in Practice"
    When I press the key "j"
    Then the focus is on the item "Kubernetes Patterns"
    When I press the key "k"
    Then the focus is on the item "Event Sourcing in Practice"

  Scenario: k from nowhere goes to the last book, and the ends hold
    Given I open "/library" in the browser
    Then the list "Books" has 3 items
    When I press the key "k"
    Then the focus is on the item "Verteilte Systeme verstehen"
    When I press the key "j"
    Then the focus is on the item "Verteilte Systeme verstehen"

  Scenario: Enter opens the book
    Given I open "/library" in the browser
    Then the list "Books" has 3 items
    When I press the key "j"
    And I press the key "j"
    And I press the key "Enter"
    Then the browser address contains "/works/01J9Z3K0000000000000000001"

  Scenario: The keys work on the search results
    Given I open "/search?q=kubernetes" in the browser
    Then the page shows the text "2 books"
    When I press the key "j"
    Then the focus is on the item "Kubernetes Patterns"
    When I press the key "Enter"
    Then the browser address contains "/works/"

  Scenario: Typing j in the search field does not move
    Given I open "/library" in the browser
    And I press the key "/"
    When I press the key "j"
    Then the focus is in the search field

  Scenario: Escape leaves the search field, then j moves
    Given I open "/library" in the browser
    Then the list "Books" has 3 items
    When I press the key "/"
    And I press the key "Escape"
    And I press the key "j"
    Then the focus is on the item "Event Sourcing in Practice"
