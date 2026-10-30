@F-RLS-1 @F-RLS-2 @F-RLS-6 @M14
Feature: The reading list through MCP
  As an AI assistant
  I want to see the reading list and to put works on it or take them off
  So that I can help a person to decide what to read next

  Background:
    Given an empty working directory
    And I have run shelf with arguments "init library"
    And a sample PDF "library/A/Kubernetes/kp.pdf" with the title "Kubernetes Patterns", the author "Bilgin Ibryam" and the pages:
      | text |
      | One. |
    And the sidecar "library/A/Kubernetes/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000001", "title": "Kubernetes Patterns", "authors": ["Bilgin Ibryam"], "language": "en", "files": [{ "name": "kp.pdf" }] }
      """
    And a sample PDF "library/B/Attention/at.pdf" with the title "Attention Is All You Need", the author "Ashish Vaswani" and the pages:
      | text |
      | Two. |
    And the sidecar "library/B/Attention/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000002", "kind": "paper", "title": "Attention Is All You Need", "authors": ["Ashish Vaswani"], "language": "en", "files": [{ "name": "at.pdf" }] }
      """
    And a sample PDF "library/D/Done/dn.pdf" with the title "Already Read", the author "N. Body" and the pages:
      | text  |
      | Four. |
    And the sidecar "library/D/Done/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000004", "title": "Already Read", "authors": ["N. Body"], "language": "en", "readStatus": "read", "files": [{ "name": "dn.pdf" }] }
      """
    And I have run shelf with arguments "reindex --library library"
    And the MCP server is running on the library "library"

  Scenario: The reading list is empty at the start
    When I call the tool "get_reading_list" with the arguments:
      """
      {}
      """
    Then the tool call succeeds
    And the result has the number "total" equal to 0

  Scenario: A work is put on the list and the list shows it
    Given I call the tool "add_to_reading_list" with the arguments:
      """
      { "id": "01J9Z3K0000000000000000001" }
      """
    When I call the tool "get_reading_list" with the arguments:
      """
      {}
      """
    Then the tool call succeeds
    And the result has the number "total" equal to 1
    And the result has the string "works[0].title" equal to "Kubernetes Patterns"
    And the file "library/A/Kubernetes/book.json" contains "readingList"

  Scenario: add_to_reading_list says what happened
    When I call the tool "add_to_reading_list" with the arguments:
      """
      { "id": "01J9Z3K0000000000000000002" }
      """
    Then the result has the string "id" equal to "01J9Z3K0000000000000000002"
    And the result has the string "title" equal to "Attention Is All You Need"
    And the result has the boolean "onReadingList" equal to true
    And the result has the boolean "changed" equal to true

  Scenario: Putting a work on the list twice changes nothing
    Given I call the tool "add_to_reading_list" with the arguments:
      """
      { "id": "01J9Z3K0000000000000000002" }
      """
    When I call the tool "add_to_reading_list" with the arguments:
      """
      { "id": "01J9Z3K0000000000000000002" }
      """
    Then the result has the boolean "changed" equal to false

  Scenario: A work is taken off the list
    Given I call the tool "add_to_reading_list" with the arguments:
      """
      { "id": "01J9Z3K0000000000000000001" }
      """
    When I call the tool "remove_from_reading_list" with the arguments:
      """
      { "id": "01J9Z3K0000000000000000001" }
      """
    Then the result has the boolean "onReadingList" equal to false
    And the result has the boolean "changed" equal to true
    And the file "library/A/Kubernetes/book.json" does not contain "readingList"

  Scenario: A work that is read already is refused
    When I call the tool "add_to_reading_list" with the arguments:
      """
      { "id": "01J9Z3K0000000000000000004" }
      """
    Then the tool call fails with the message containing "already read"

  Scenario: get_book says whether the work is on the list
    Given I call the tool "add_to_reading_list" with the arguments:
      """
      { "id": "01J9Z3K0000000000000000001" }
      """
    When I call the tool "get_book" with the arguments:
      """
      { "id": "01J9Z3K0000000000000000001" }
      """
    Then the result has the boolean "onReadingList" equal to true
