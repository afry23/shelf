@ui @PRD-8.3 @F-RLS-2 @F-RLS-7 @M14
Feature: The reading list in the browser
  As a person who reads in the browser
  I want a page with the works I want to read next
  So that I can pick the next one and see it leave the list when I am done

  Background:
    Given an empty working directory
    And I have run shelf with arguments "init library"
    And a sample PDF "library/A/Kubernetes/kp.pdf" with the title "Kubernetes Patterns", the author "Bilgin Ibryam" and the pages:
      | text |
      | One. |
    And the sidecar "library/A/Kubernetes/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000001", "title": "Kubernetes Patterns", "authors": ["Bilgin Ibryam"], "language": "en", "readingList": "2026-10-01T08:00:00Z", "files": [{ "name": "kp.pdf" }] }
      """
    And a sample PDF "library/B/Attention/at.pdf" with the title "Attention Is All You Need", the author "Ashish Vaswani" and the pages:
      | text |
      | Two. |
    And the sidecar "library/B/Attention/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000002", "kind": "paper", "title": "Attention Is All You Need", "authors": ["Ashish Vaswani"], "language": "en", "files": [{ "name": "at.pdf" }] }
      """
    And I have run shelf with arguments "reindex --library library"
    And the web server is running on the library "library"

  Scenario: The header leads to the reading list
    Given I open "/library" in the browser
    When I click the "Reading list" link
    Then the browser address contains "/reading-list"

  Scenario: The page shows the works on the list and no others
    When I open "/reading-list" in the browser
    Then the page shows the text "Kubernetes Patterns"
    And the page does not show the text "Attention Is All You Need"

  Scenario: An empty list says so
    Given I open "/reading-list" in the browser
    And I click the "Remove" button
    Then the page shows the text "Your reading list is empty"

  Scenario: A work is taken off the list on the page
    Given I open "/reading-list" in the browser
    When I click the "Remove" button
    Then the page does not show the text "Kubernetes Patterns"
    And the file "library/A/Kubernetes/book.json" soon does not contain "readingList"

  Scenario: A work is marked as read on the page and leaves the list
    Given I open "/reading-list" in the browser
    When I click the "Mark as read" button
    Then the page does not show the text "Kubernetes Patterns"
    And the file "library/A/Kubernetes/book.json" soon contains "finishedAt"
    And the file "library/A/Kubernetes/book.json" soon does not contain "readingList"

  Scenario: A work is put on the list from its page
    Given I open "/library" in the browser
    And I click the "Attention Is All You Need" link
    When I click the "Add to reading list" button
    Then the page shows the text "On the reading list"
    And the file "library/B/Attention/book.json" soon contains "readingList"

  Scenario: A work is taken off the list from its page
    Given I open "/library" in the browser
    And I click the "Kubernetes Patterns" link
    When I click the "On the reading list" button
    Then the page shows the text "Add to reading list"
    And the file "library/A/Kubernetes/book.json" soon does not contain "readingList"

  Scenario: Marking a work as read on its page takes it off the list
    Given I open "/library" in the browser
    And I click the "Kubernetes Patterns" link
    When I click the "Finished" button
    Then the page shows the text "Add to reading list"
    And the file "library/A/Kubernetes/book.json" soon does not contain "readingList"
