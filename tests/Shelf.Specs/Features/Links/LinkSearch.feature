@F-LNK-3 @M22
Feature: Searching by links
  As a reader
  I want to list the works that are linked with a work
  So that I can follow my own connections in a search

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
    And I have run shelf with arguments "link 01J9Z3K0000000000000000001 01J9Z3K0000000000000000002 --library library"

  Scenario Outline: linked finds the works linked with a work, however the work is named
    When I run shelf with arguments "search <query> --library library"
    Then the exit code is 0
    And standard output contains "Attention Is All You Need"
    And standard output does not contain "Container Security"

    Examples:
      | query                              |
      | linked:01J9Z3K0000000000000000001  |
      | linked:01J9Z3K0000000000000000001                    |
      | linked:ibryam2019                  |

  Scenario: The link works from both sides
    When I run shelf with arguments "search linked:01J9Z3K0000000000000000002 --library library"
    Then standard output contains "Kubernetes Patterns"

  Scenario Outline: has:links selects works with or without links
    When I run shelf with arguments "search --library library -- <query>"
    Then the exit code is 0
    And standard output contains "<present>"
    And standard output does not contain "<absent>"

    Examples:
      | query       | present            | absent             |
      | has:links   | Kubernetes Patterns | Container Security |
      | -has:links  | Container Security  | Kubernetes Patterns |

  Scenario Outline: A wrong filter value is explained
    When I run shelf with arguments "search <query> --library library"
    Then the exit code is 2
    And standard error contains "<message>"

    Examples:
      | query        | message                              |
      | has:pizza    | has: 'pizza' is not one of links     |
      | linked:      | linked: needs a value                |

  Scenario: Reindex keeps the filter working
    When I delete the file "library/.shelf/index.db"
    And I run shelf with arguments "reindex --library library"
    And I run shelf with arguments "search linked:01J9Z3K0000000000000000001 --library library"
    Then standard output contains "Attention Is All You Need"
