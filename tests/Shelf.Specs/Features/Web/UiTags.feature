@ui @PRD-8.2 @F-TAG-4 @F-TAG-5 @M5
Feature: The tags in the browser
  As a person who keeps tags
  I want to see the tag tree and to rename and merge tags
  So that I tidy my tags without the command line

  Background:
    Given an empty working directory
    And I have run shelf with arguments "init library"
    And a sample EPUB "library/Ibryam/Kubernetes Patterns/kp.epub" with the title "Kubernetes Patterns", the authors "Bilgin Ibryam" and the chapters:
      | title                 | text                                              |
      | Foundational Patterns | The sidecar container extends the main container. |
    And the sidecar "library/Ibryam/Kubernetes Patterns/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000001", "title": "Kubernetes Patterns", "subtitle": "Reusable Elements", "authors": ["Bilgin Ibryam"], "language": "en", "year": 2019, "description": "Design patterns for containers", "tags": ["cloud/kubernetes", "devops"], "files": [{ "name": "kp.epub" }] }
      """
    And a sample PDF "library/Alander/Event Sourcing in Practice/es.pdf" with the title "Event Sourcing in Practice", the author "Jördis Ålander" and the pages:
      | text                                            |
      | Event sourcing stores every change as an event. |
    And the sidecar "library/Alander/Event Sourcing in Practice/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000002", "title": "Event Sourcing in Practice", "authors": ["Jördis Ålander"], "language": "en", "year": 2021, "tags": ["architektur/ddd", "cloud/azure"], "files": [{ "name": "es.pdf" }] }
      """
    And I have run shelf with arguments "reindex --library library"
    And the web server is running on the library "library"

  Scenario: The tree shows every tag with the number of works
    When I open "/tags" in the browser
    Then the page shows the text "4 tags"
    And the page shows the text "cloud"
    And the page shows the text "kubernetes"
    And the page shows the text "azure"
    And the page shows the text "devops"

  Scenario: The header leads to the tags
    Given I open "/library" in the browser
    When I click the "Tags" link
    Then the browser address contains "/tags"

  Scenario: A tag leads to the books that carry it
    Given I open "/tags" in the browser
    When I click the "devops" link
    Then the browser address contains "tag%3Adevops"

  Scenario: A tag is renamed
    Given I open "/tags" in the browser
    When I click the "Rename devops" button
    And I fill the "New name" field with "operations"
    And I click the "Rename tag" button
    Then the page shows the text "Renamed devops to operations on 1 work."
    And the page shows the text "operations"
    And the file "library/Ibryam/Kubernetes Patterns/book.json" contains "operations"

  Scenario: A tag is merged into another one
    Given I open "/tags" in the browser
    When I click the "Merge devops" button
    And I fill the "Merge into" field with "cloud/azure"
    And I click the "Merge tag" button
    Then the page shows the text "Merged devops into cloud/azure on 1 work."
    And the file "library/Ibryam/Kubernetes Patterns/book.json" does not contain "devops"

  Scenario: A tag that cannot be renamed says why
    Given I open "/tags" in the browser
    When I click the "Rename devops" button
    And I fill the "New name" field with "cloud/azure"
    And I click the "Rename tag" button
    Then the page shows the text "already exists"
    And the file "library/Ibryam/Kubernetes Patterns/book.json" contains "devops"

  Scenario: Cancel closes the form and changes nothing
    Given I open "/tags" in the browser
    And I click the "Rename devops" button
    When I click the "Cancel" button
    Then the page does not show the text "Rename tag"
