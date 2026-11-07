@F-LNK-4 @M22
Feature: Links survive merges and deletions
  As a reader who curates links
  I want links to follow a merge and to disappear with a deleted work
  So that no link points into the void

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
    And I have run shelf with arguments "link 01J9Z3K0000000000000000001 01J9Z3K0000000000000000002 --note 'background reading' --library library"
    And I have run shelf with arguments "link 01J9Z3K0000000000000000003 01J9Z3K0000000000000000002 --note 'security view' --library library"

  Scenario: Deleting a work removes the links to it from the other works
    When I run shelf with arguments "delete 01J9Z3K0000000000000000002 --yes --library library"
    Then the exit code is 0
    And the file "library/A/Kubernetes/book.json" does not contain "links"
    And the file "library/C/Containers/book.json" does not contain "links"

  Scenario: Merging moves the links of the source to the target
    When I run shelf with arguments "merge 01J9Z3K0000000000000000001 01J9Z3K0000000000000000002 --library library"
    Then the exit code is 0
    When I run shelf with arguments "links 01J9Z3K0000000000000000001 --library library"
    Then standard output contains "Container Security"
    And standard output does not contain "Attention Is All You Need"
    When I run shelf with arguments "links 01J9Z3K0000000000000000003 --library library"
    Then standard output contains "Kubernetes Patterns"
    And standard output contains "security view"

  Scenario: Doctor reports a link to a work that does not exist
    Given the sidecar "library/C/Containers/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000003", "title": "Container Security", "authors": ["Liz Rice"], "language": "en", "links": [{ "work": "01J9Z3K0000000000000000099", "note": "gone" }], "files": [{ "name": "ct.pdf" }] }
      """
    And I have run shelf with arguments "reindex --library library"
    When I run shelf with arguments "doctor --library library"
    Then standard output contains "[warn] links: 1 link points to a work that does not exist"
    And standard output contains "01J9Z3K0000000000000000099"
    When I run shelf with arguments "links 01J9Z3K0000000000000000003 --library library"
    Then standard output is "No links"

  Scenario: A sidecar that links the work to itself is refused
    Given the sidecar "library/C/Containers/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000003", "title": "Container Security", "authors": ["Liz Rice"], "language": "en", "links": [{ "work": "01J9Z3K0000000000000000003" }], "files": [{ "name": "ct.pdf" }] }
      """
    When I run shelf with arguments "doctor --library library"
    Then standard output contains "must not be the work's own ID"
