@ui @F-IMP-6 @M5
Feature: Loading book metadata in the browser
  As a person who looks at a book with poor details
  I want to see what the internet knows about it and take it over
  So that I fill in the details without typing them

  Background:
    Given an empty working directory
    And I have run shelf with arguments "init library"
    And a sample EPUB "library/Ibryam/Kubernetes Patterns/kp.epub" with the title "Kubernetes Patterns", the authors "Bilgin Ibryam" and the text seed 1
    And the sidecar "library/Ibryam/Kubernetes Patterns/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000001", "title": "Kubernetes Patterns", "authors": ["Bilgin Ibryam"], "isbn13": "9781492034926", "year": 2019, "files": [{ "name": "kp.epub" }] }
      """
    And I have run shelf with arguments "reindex --library library"
    And the online source "openlibrary" holds the book:
      | field       | value                           |
      | isbn13      | 9781492034926                   |
      | title       | Kubernetes Patterns             |
      | authors     | Bilgin Ibryam                   |
      | publisher   | O'Reilly Media                  |
      | year        | 2020                            |
      | description | Design patterns for Kubernetes. |
    And the web server is running on the library "library"

  Scenario: Nothing is asked online until the button is clicked
    When I open "/works/01J9Z3K0000000000000000001" in the browser
    Then the page shows the text "Load details from the internet"
    And no online source was queried

  Scenario: The preview shows what would be added and what stays
    Given I open "/works/01J9Z3K0000000000000000001" in the browser
    When I click the "Load details from the internet" button
    Then the page shows the text "from openlibrary"
    And the page shows the text "O'Reilly Media"
    And the page shows the text "Design patterns for Kubernetes."
    And the page shows the text "Kept: year 2019 (online: 2020)"

  Scenario: Taking over the details fills the empty fields
    Given I open "/works/01J9Z3K0000000000000000001" in the browser
    And I click the "Load details from the internet" button
    When I click the "Take over" button
    Then the page shows the text "Updated: publisher, description"
    And the page shows the text "Publisher"
    And the page shows the text "O'Reilly Media"
    And the page shows the text "2019"

  Scenario: Overwrite replaces the year as well
    Given I open "/works/01J9Z3K0000000000000000001" in the browser
    And I click the "Load details from the internet" button
    And I click the "Overwrite details that are already set" button
    When I click the "Take over" button
    Then the page shows the text "Updated: publisher, year, description"
    And the page shows the text "2020"

  Scenario: Cancelling leaves the book as it is
    Given I open "/works/01J9Z3K0000000000000000001" in the browser
    And I click the "Load details from the internet" button
    When I click the "Cancel" button
    Then the page does not show the text "from openlibrary"
    And the "Take over" button is not shown

  Scenario: A book that is not found says so
    Given the online source "openlibrary" holds no books
    And I open "/works/01J9Z3K0000000000000000001" in the browser
    When I click the "Load details from the internet" button
    Then an alert says "no online source knows this book"

  Scenario: One of several candidates is chosen
    Given the sidecar "library/Ibryam/Kubernetes Patterns/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000001", "title": "Kubernetes Patterns", "authors": ["Bilgin Ibryam"], "files": [{ "name": "kp.epub" }] }
      """
    And the online source "openlibrary" holds the book:
      | field     | value               |
      | title     | Kubernetes Patterns |
      | authors   | Bilgin Ibryam       |
      | publisher | Manning             |
    And I open "/works/01J9Z3K0000000000000000001" in the browser
    When I click the "Load details from the internet" button
    Then the page shows the text "2 books match"
    When I click the "Use candidate 2" button
    Then the page shows the text "Manning"
    When I click the "Take over" button
    Then the page shows the text "Updated: publisher"
