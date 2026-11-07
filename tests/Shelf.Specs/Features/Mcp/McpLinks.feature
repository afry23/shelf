@F-LNK-6 @M22
Feature: Links through MCP
  As an AI assistant
  I want to see the links of a work and to link works
  So that I can follow and extend the connections a person made

  Background:
    Given an empty working directory
    And I have run shelf with arguments "init library"
    And a sample PDF "library/A/Kubernetes/kp.pdf" with the title "Kubernetes Patterns", the author "Bilgin Ibryam" and the pages:
      | text |
      | One. |
    And the sidecar "library/A/Kubernetes/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000001", "title": "Kubernetes Patterns", "authors": ["Bilgin Ibryam"], "language": "en", "citeKey": "ibryam2019", "files": [{ "name": "kp.pdf" }] }
      """
    And a sample PDF "library/B/Attention/at.pdf" with the title "Attention Is All You Need", the author "Ashish Vaswani" and the pages:
      | text |
      | Two. |
    And the sidecar "library/B/Attention/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000002", "kind": "paper", "title": "Attention Is All You Need", "authors": ["Ashish Vaswani"], "language": "en", "files": [{ "name": "at.pdf" }] }
      """
    And a sample PDF "library/C/Containers/ct.pdf" with the title "Container Security", the author "Liz Rice" and the pages:
      | text  |
      | Three. |
    And the sidecar "library/C/Containers/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000003", "title": "Container Security", "authors": ["Liz Rice"], "language": "en", "files": [{ "name": "ct.pdf" }] }
      """
    And I have run shelf with arguments "reindex --library library"
    And the MCP server is running on the library "library"

  Scenario: A work without links
    When I call the tool "get_links" with the arguments:
      """
      { "id": "01J9Z3K0000000000000000001" }
      """
    Then the tool call succeeds
    And the result has the number "total" equal to 0

  Scenario: Works are linked and the link shows on both sides
    Given I call the tool "link_works" with the arguments:
      """
      { "id": "01J9Z3K0000000000000000001", "other": "01J9Z3K0000000000000000002", "note": "background reading" }
      """
    When I call the tool "get_links" with the arguments:
      """
      { "id": "01J9Z3K0000000000000000002" }
      """
    Then the result has the number "total" equal to 1
    And the result has the string "links[0].title" equal to "Kubernetes Patterns"
    And the file "library/A/Kubernetes/book.json" contains "background reading"

  Scenario: link_works says what happened
    When I call the tool "link_works" with the arguments:
      """
      { "id": "01J9Z3K0000000000000000001", "other": "01J9Z3K0000000000000000002" }
      """
    Then the result has the boolean "changed" equal to true
    When I call the tool "link_works" with the arguments:
      """
      { "id": "01J9Z3K0000000000000000001", "other": "01J9Z3K0000000000000000002" }
      """
    Then the result has the boolean "changed" equal to false

  Scenario: A link is removed
    Given I call the tool "link_works" with the arguments:
      """
      { "id": "01J9Z3K0000000000000000001", "other": "01J9Z3K0000000000000000002" }
      """
    When I call the tool "unlink_works" with the arguments:
      """
      { "id": "01J9Z3K0000000000000000001", "other": "01J9Z3K0000000000000000002" }
      """
    Then the result has the boolean "changed" equal to true
    And the file "library/A/Kubernetes/book.json" does not contain "links"

  Scenario: A work cannot be linked to itself
    When I call the tool "link_works" with the arguments:
      """
      { "id": "01J9Z3K0000000000000000001", "other": "01J9Z3K0000000000000000001" }
      """
    Then the tool call fails
