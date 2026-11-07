@F-LNK-1 @F-LNK-2 @M22
Feature: Linking works by hand
  As a reader who knows which works belong together
  I want to link two works and write why
  So that my own connections are kept next to the automatic similar books

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

  Scenario: A link is written to both works, each with its own note
    When I run shelf with arguments "link 01J9Z3K0000000000000000001 01J9Z3K0000000000000000002 --note 'background reading' --other-note 'applied in' --library library"
    Then the exit code is 0
    And standard output is "Linked Kubernetes Patterns and Attention Is All You Need"
    And the file "library/A/Kubernetes/book.json" contains "01J9Z3K0000000000000000002"
    And the file "library/A/Kubernetes/book.json" contains "background reading"
    And the file "library/B/Attention/book.json" contains "01J9Z3K0000000000000000001"
    And the file "library/B/Attention/book.json" contains "applied in"

  Scenario: A link needs no note
    When I run shelf with arguments "link 01J9Z3K0000000000000000001 01J9Z3K0000000000000000002 --library library"
    Then the exit code is 0
    And the file "library/A/Kubernetes/book.json" does not contain "note"

  Scenario: Links lists the links of a work with the note of its side
    Given I have run shelf with arguments "link 01J9Z3K0000000000000000001 01J9Z3K0000000000000000002 --note 'background reading' --other-note 'applied in' --library library"
    And I have run shelf with arguments "link 01J9Z3K0000000000000000001 01J9Z3K0000000000000000003 --library library"
    When I run shelf with arguments "links 01J9Z3K0000000000000000001 --library library"
    Then the exit code is 0
    And standard output matches the pattern "(?m)^Attention Is All You Need\s+01J9Z3K0000000000000000002\s+background reading$"
    And standard output matches the pattern "(?m)^Container Security\s+01J9Z3K0000000000000000003$"
    When I run shelf with arguments "links 01J9Z3K0000000000000000002 --library library"
    Then standard output matches the pattern "(?m)^Kubernetes Patterns\s+01J9Z3K0000000000000000001\s+applied in$"

  Scenario: Links as JSON
    Given I have run shelf with arguments "link 01J9Z3K0000000000000000001 01J9Z3K0000000000000000002 --note 'background reading' --library library"
    When I run shelf with arguments "links 01J9Z3K0000000000000000001 --json --library library"
    Then the exit code is 0
    And the JSON property "links" of standard output has 1 items
    And standard output contains "background reading"

  Scenario: A work without links says so
    When I run shelf with arguments "links 01J9Z3K0000000000000000001 --library library"
    Then the exit code is 0
    And standard output is "No links"

  Scenario: Show has a section Links
    Given I have run shelf with arguments "link 01J9Z3K0000000000000000001 01J9Z3K0000000000000000002 --note 'background reading' --library library"
    When I run shelf with arguments "show 01J9Z3K0000000000000000001 --library library"
    Then standard output matches the pattern "(?m)^Links\s+Attention Is All You Need\s+01J9Z3K0000000000000000002\s+background reading$"

  Scenario: Linking again replaces the notes that are given and keeps one link
    Given I have run shelf with arguments "link 01J9Z3K0000000000000000001 01J9Z3K0000000000000000002 --note 'first' --other-note 'other first' --library library"
    When I run shelf with arguments "link 01J9Z3K0000000000000000001 01J9Z3K0000000000000000002 --note 'second' --library library"
    Then the exit code is 0
    And the file "library/A/Kubernetes/book.json" contains "second"
    And the file "library/A/Kubernetes/book.json" does not contain "first"
    And the file "library/B/Attention/book.json" contains "other first"

  Scenario: Unlink removes the link from both works
    Given I have run shelf with arguments "link 01J9Z3K0000000000000000001 01J9Z3K0000000000000000002 --note 'x' --library library"
    When I run shelf with arguments "unlink 01J9Z3K0000000000000000002 01J9Z3K0000000000000000001 --library library"
    Then the exit code is 0
    And standard output is "Unlinked Attention Is All You Need and Kubernetes Patterns"
    And the file "library/A/Kubernetes/book.json" does not contain "links"
    And the file "library/B/Attention/book.json" does not contain "links"

  Scenario: Unlinking works that are not linked is an error
    When I run shelf with arguments "unlink 01J9Z3K0000000000000000001 01J9Z3K0000000000000000002 --library library"
    Then the exit code is 1
    And standard error contains "are not linked"

  Scenario Outline: A link that cannot be made is refused and nothing is written
    When I run shelf with arguments "link <first> <second> <options> --library library"
    Then the exit code is <code>
    And standard error contains "<message>"
    And the file "library/A/Kubernetes/book.json" does not contain "links"

    Examples:
      | first      | second     | options                                | code | message                           |
      | 01J9Z3K0000000000000000001   | 01J9Z3K0000000000000000001   |                                        | 1    | cannot be linked to itself        |
      | 01J9Z3K0000000000000000001   | 01J9Z3KZ   |                                        | 1    | no work                           |
      | 01J9Z3K0000000000000000001   | 01J9Z3K0000000000000000002   | --note 'xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx'                   | 1    | at most 200 characters            |

  Scenario: Reindex restores the links from the sidecars
    Given I have run shelf with arguments "link 01J9Z3K0000000000000000001 01J9Z3K0000000000000000002 --note 'background reading' --library library"
    When I delete the file "library/.shelf/index.db"
    And I run shelf with arguments "reindex --library library"
    And I run shelf with arguments "links 01J9Z3K0000000000000000001 --library library"
    Then standard output contains "background reading"
