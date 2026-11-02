@ui @F-SRC-10 @M19
Feature: Saved searches in the library view
  As a person who browses the library in the browser
  I want to save the filters I set and open them again from the sidebar
  So that I get to my usual views in one click

  Background:
    Given an empty working directory
    And I have run shelf with arguments "init library"
    And a sample PDF "library/Ibryam/Kubernetes Patterns/kp.pdf" with the title "Kubernetes Patterns", the author "Bilgin Ibryam" and the pages:
      | text                                     |
      | The sidecar container extends the app.   |
    And the sidecar "library/Ibryam/Kubernetes Patterns/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000001", "title": "Kubernetes Patterns", "authors": ["Bilgin Ibryam"], "language": "en", "year": 2019, "tags": ["cloud/kubernetes"], "readStatus": "read", "files": [{ "name": "kp.pdf" }] }
      """
    And a sample PDF "library/Alander/Event Sourcing in Practice/es.pdf" with the title "Event Sourcing in Practice", the author "Jördis Ålander" and the pages:
      | text                                            |
      | A projection builds a read model for kubernetes. |
    And the sidecar "library/Alander/Event Sourcing in Practice/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000002", "title": "Event Sourcing in Practice", "authors": ["Jördis Ålander"], "language": "en", "year": 2021, "tags": ["architektur/ddd"], "files": [{ "name": "es.pdf" }] }
      """
    And a sample PDF "library/Weiss/Verteilte Systeme/vs.pdf" with the title "Verteilte Systeme verstehen", the author "Lukas Weiß" and the pages:
      | text                    |
      | Ein Konsensprotokoll.   |
    And the sidecar "library/Weiss/Verteilte Systeme/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000003", "title": "Verteilte Systeme verstehen", "authors": ["Lukas Weiß"], "language": "de", "year": 2015, "tags": ["architektur/ddd", "architektur/verteilte-systeme"], "files": [{ "name": "vs.pdf" }] }
      """
    And I have run shelf with arguments "reindex --library library"
    And I have run shelf with arguments "searches add stack tag:architektur/ddd --library library"
    And the web server is running on the library "library"

  Scenario: The saved searches are listed in the library view
    When I open "/library" in the browser
    Then the link "stack" points to an address containing "q=tag%3Aarchitektur%2Fddd"

  Scenario: A saved search opens the library with its filters
    Given I open "/library" in the browser
    When I click the "stack" link
    Then the browser address contains "q=tag%3Aarchitektur%2Fddd"
    And the page shows the text "Event Sourcing in Practice"
    And the page does not show the text "Kubernetes Patterns"

  Scenario: The filters of the view are saved under a name
    Given I open "/library?q=status%3Aread" in the browser
    When I fill the "Name of the search" field with "finished"
    And I click the "Save search" button
    Then the link "finished" points to an address containing "q=status%3Aread"
    And the file "library/.shelf/searches.json" soon contains "finished"

  Scenario: A saved search is removed from the sidebar
    Given I open "/library" in the browser
    When I click the "Remove saved search stack" button
    Then the page does not show the text "stack"
    And the file "library/.shelf/searches.json" soon does not contain "stack"

  Scenario: Without filters there is nothing to save
    When I open "/library" in the browser
    Then the "Save search" button is not shown

  Scenario: A name that cannot be kept is shown as an alert
    Given I open "/library?q=status%3Aread" in the browser
    When I fill the "Name of the search" field with "a b"
    And I click the "Save search" button
    Then an alert says "name"
