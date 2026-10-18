@ui @PRD-8.2 @M4
Feature: The library as a shelf
  As a person with a library of books
  I want to see the books as covers, narrow them down and sort them
  So that I find a book by looking for it

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

  Scenario: The library shows every book, ordered by title
    When I open "/library" in the browser
    Then the page shows the text "3 books"
    And the list "Books" has 3 items
    And the first item of the list "Books" is "Event Sourcing in Practice"

  Scenario: The root leads to the library
    When I open "/" in the browser
    Then the page shows the text "3 books"
    And the browser address contains "/library"

  Scenario: A book with a cover shows it, a book without one shows a spine
    When I open "/library" in the browser
    Then the book "Event Sourcing in Practice" shows its cover
    And the book "Kubernetes Patterns" shows a spine instead of a cover
    And the book "Kubernetes Patterns" shows its author "Bilgin Ibryam"

  Scenario: A facet narrows the shelf and shows as a filter that can be removed
    Given I open "/library" in the browser
    When I click the "Format: pdf" button
    Then the page shows the text "1 book"
    And the list "Books" has 1 items
    And the first item of the list "Books" is "Event Sourcing in Practice"
    And the "Format: pdf" button is pressed
    And the "Remove filter Format: pdf" button is shown
    And the browser address contains "format"
    When I click the "Remove filter Format: pdf" button
    Then the page shows the text "3 books"
    And the "Format: pdf" button is not pressed

  Scenario: Several filters work together
    Given I open "/library" in the browser
    When I click the "Language: en" button
    And I click the "Format: epub" button
    Then the list "Books" has 1 items
    And the first item of the list "Books" is "Kubernetes Patterns"

  Scenario Outline: Every facet filters
    Given I open "/library" in the browser
    When I click the "<facet>" button
    Then the list "Books" has <count> items
    And the first item of the list "Books" is "<first>"

    Examples:
      | facet                              | count | first                       |
      | Tags: architektur/ddd              | 2     | Event Sourcing in Practice  |
      | Authors: Lukas Weiß                | 1     | Verteilte Systeme verstehen |
      | Language: de                       | 1     | Verteilte Systeme verstehen |
      | Year: 2019                         | 1     | Kubernetes Patterns         |
      | Format: epub                       | 2     | Kubernetes Patterns         |
      | Reading status: read               | 1     | Kubernetes Patterns         |

  Scenario Outline: The books can be sorted, and the order turned around
    Given I open "/library" in the browser
    When I choose "<field>" in the "Sort by" list
    Then the first item of the list "Books" is "<first>"
    When I click the "Sort order" button
    Then the first item of the list "Books" is "<last>"

    Examples:
      | field        | first                       | last                        |
      | Title        | Event Sourcing in Practice  | Verteilte Systeme verstehen |
      | Year         | Verteilte Systeme verstehen | Event Sourcing in Practice  |
      | Author       | Event Sourcing in Practice  | Verteilte Systeme verstehen |

  Scenario: The list view shows the details in a row
    Given I open "/library" in the browser
    When I click the "List" button
    Then the "List" button is pressed
    And the row "Kubernetes Patterns" of the list "Books" shows "2019"
    And the row "Kubernetes Patterns" of the list "Books" shows "Bilgin Ibryam"
    And the row "Kubernetes Patterns" of the list "Books" shows "epub"

  Scenario: The view that was chosen is kept
    Given I open "/library" in the browser
    And I click the "List" button
    When I reload the page
    Then the "List" button is pressed

  Scenario: The shelf is paged
    When I open "/library?limit=2" in the browser
    Then the page shows the text "1–2 of 3"
    And the list "Books" has 2 items
    When I click the "Next page" button
    Then the page shows the text "3–3 of 3"
    And the list "Books" has 1 items
    And the first item of the list "Books" is "Verteilte Systeme verstehen"
    When I click the "Previous page" button
    Then the page shows the text "1–2 of 3"

  Scenario: The filters are in the address, so a reload and a link keep them
    Given I open "/library?q=lang%3Ade" in the browser
    Then the list "Books" has 1 items
    And the "Remove filter Language: de" button is shown
    When I reload the page
    Then the list "Books" has 1 items

  Scenario: Nothing matches
    When I open "/library?q=lang%3Afr" in the browser
    Then the page shows the text "No books match these filters"
    When I click the "Clear filters" button
    Then the list "Books" has 3 items

  Scenario: Opening a book
    Given I open "/library" in the browser
    When I click the "Kubernetes Patterns" link
    Then the browser address contains "/works/01J9Z3K0000000000000000001"
    And the page shows the text "Reusable Elements"

  Scenario: A query that the server refuses is explained
    When I open "/library?q=year%3Aabc" in the browser
    Then an alert says "year"

  Scenario: A library without books says how to add some
    Given an empty working directory
    And I have run shelf with arguments "init empty"
    And the web server is running on the library "empty"
    When I open "/library" in the browser
    Then the page shows the text "Your library is empty"
    And the page shows the text "shelf import"
