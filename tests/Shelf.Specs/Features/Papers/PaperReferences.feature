@F-PAP-11 @F-PAP-12 @M11
Feature: References of a paper and the papers that cite it
  As a person who reads papers
  I want to load the reference list of a paper and see which of the works it cites are in my library
  So that I follow a line of work through the papers I own, and see who cites a paper

  Background:
    Given an empty working directory
    And I have run shelf with arguments "init library"
    And a sample PDF "library/Vaswani/Attention/attention.pdf" with the title "Attention Is All You Need", the author "Ashish Vaswani" and the pages:
      | text                                   |
      | The Transformer relies on attention.   |
    And the sidecar "library/Vaswani/Attention/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000001", "kind": "paper", "title": "Attention Is All You Need", "authors": ["Ashish Vaswani"], "language": "en", "year": 2017, "doi": "10.5555/Attention", "files": [{ "name": "attention.pdf" }] }
      """
    And a sample PDF "library/Devlin/BERT/bert.pdf" with the title "BERT", the author "Jacob Devlin" and the pages:
      | text                                         |
      | BERT pre-trains deep bidirectional encoders. |
    And the sidecar "library/Devlin/BERT/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000002", "kind": "paper", "title": "BERT", "authors": ["Jacob Devlin"], "language": "en", "year": 2019, "doi": "10.18653/bert", "files": [{ "name": "bert.pdf" }] }
      """
    And a sample PDF "library/Notes/Unlisted/unlisted.pdf" with the title "Unlisted Notes", the author "N. Body" and the pages:
      | text           |
      | Some notes.    |
    And the sidecar "library/Notes/Unlisted/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000003", "title": "Unlisted Notes", "authors": ["N. Body"], "language": "en", "files": [{ "name": "unlisted.pdf" }] }
      """
    And the online source "crossref" holds the paper:
      | field | value                     |
      | doi   | 10.18653/bert             |
      | title | BERT                      |
      | year  | 2019                      |
    And the online source "crossref" lists these references for the DOI "10.18653/bert":
      | doi                | title                     | year | author   | unstructured                                                 |
      | 10.5555/attention  | Attention Is All You Need | 2017 | Vaswani  |                                                              |
      | 10.1000/outside    | Some Outside Paper        | 2018 | Smith    |                                                              |
      |                    |                           |      |          | Peters et al., Deep contextualized word representations, 2018 |
    And I have run shelf with arguments "reindex --library library"

  # ---- loading: only on the command ----

  Scenario: The preview says what Crossref lists and stores nothing
    When I run shelf with arguments "refs fetch 01J9Z3K0000000000000000002 --library library"
    Then the exit code is 0
    And standard output contains "from crossref"
    And standard output contains "3 references"
    And standard output contains "2 with a DOI"
    And standard output contains "1 in the library"
    And standard output contains "Nothing written; run again with --apply"
    And the file "library/Devlin/BERT/references.json" does not exist

  Scenario: Applying stores the list next to book.json
    When I run shelf with arguments "refs fetch 01J9Z3K0000000000000000002 --apply --library library"
    Then the exit code is 0
    And standard output contains "Stored 3 references"
    And the file "library/Devlin/BERT/references.json" contains "10.1000/outside"
    And the file "library/Devlin/BERT/references.json" contains "Peters et al."
    And the file "library/Devlin/BERT/book.json" does not contain "10.1000/outside"

  Scenario: Loading again replaces the list
    Given I run shelf with arguments "refs fetch 01J9Z3K0000000000000000002 --apply --library library"
    And the online source "crossref" lists these references for the DOI "10.18653/bert":
      | doi             | title              | year | author | unstructured |
      | 10.1000/outside | Some Outside Paper | 2018 | Smith  |              |
    When I run shelf with arguments "refs fetch 01J9Z3K0000000000000000002 --apply --library library"
    Then the exit code is 0
    And standard output contains "Stored 1 reference"
    And the file "library/Devlin/BERT/references.json" does not contain "Peters et al."

  Scenario: Nothing goes online unless the person asks for it
    When I run shelf with arguments "reindex --library library"
    And I run shelf with arguments "scan --library library"
    And I run shelf with arguments "refs list 01J9Z3K0000000000000000002 --library library"
    Then no online source was queried

  Scenario: Only Crossref is asked, and by the DOI of the work
    When I run shelf with arguments "refs fetch 01J9Z3K0000000000000000002 --apply --library library"
    Then the online source "crossref" was queried
    And the online source "arxiv" was not queried
    And the online source "openlibrary" was not queried

  Scenario Outline: A paper that cannot be asked is reported and nothing changes
    Given I take a snapshot of the folder "library"
    When I run shelf with arguments "refs fetch <work> --apply --library library"
    Then the exit code is 1
    And standard error contains "<message>"
    And the folder "library" equals the snapshot

    Examples:
      | work                       | message         |
      | 01J9Z3K0000000000000000003 | has no DOI      |
      | 01J9Z3K9000000000000000009 | no work matches |

  Scenario: A source that fails is reported and nothing changes
    Given the online source "crossref" fails with "crossref is down"
    And I take a snapshot of the folder "library"
    When I run shelf with arguments "refs fetch 01J9Z3K0000000000000000002 --apply --library library"
    Then the exit code is 1
    And standard error contains "crossref is down"
    And the folder "library" equals the snapshot

  Scenario: A paper that Crossref lists no references for stores nothing
    Given the online source "crossref" lists these references for the DOI "10.5555/attention":
      | doi | title | year | author | unstructured |
    When I run shelf with arguments "refs fetch 01J9Z3K0000000000000000001 --apply --library library"
    Then the exit code is 0
    And standard output contains "lists no references"
    And the file "library/Vaswani/Attention/references.json" does not exist

  # ---- listing: what the library knows ----

  Scenario: The references show which works are in the library
    Given I run shelf with arguments "refs fetch 01J9Z3K0000000000000000002 --apply --library library"
    When I run shelf with arguments "refs list 01J9Z3K0000000000000000002 --library library"
    Then the exit code is 0
    And standard output contains "3 references, 1 in the library"
    And standard output contains "Attention Is All You Need"
    And standard output contains "01J9Z3K0000000000000000001"
    And standard output contains "10.1000/outside"
    And standard output contains "Peters et al."

  Scenario: A DOI is matched whatever its case
    Given I run shelf with arguments "refs fetch 01J9Z3K0000000000000000002 --apply --library library"
    When I run shelf with arguments "refs list 01J9Z3K0000000000000000002 --json --library library"
    Then the JSON of standard output has the number "inLibrary" equal to 1
    And the JSON of standard output has the string "references[0].work.id" equal to "01J9Z3K0000000000000000001"

  Scenario: The cited paper knows who cites it
    Given I run shelf with arguments "refs fetch 01J9Z3K0000000000000000002 --apply --library library"
    When I run shelf with arguments "refs list 01J9Z3K0000000000000000001 --library library"
    Then the exit code is 0
    And standard output contains "Cited by 1 work in the library"
    And standard output contains "BERT"

  Scenario: A paper that nobody cites and that cites nobody says so
    When I run shelf with arguments "refs list 01J9Z3K0000000000000000003 --library library"
    Then the exit code is 0
    And standard output contains "No references loaded"
    And standard output contains "Cited by no work in the library"

  Scenario: Reindex brings the references back
    Given I run shelf with arguments "refs fetch 01J9Z3K0000000000000000002 --apply --library library"
    When I run shelf with arguments "reindex --library library"
    And I run shelf with arguments "refs list 01J9Z3K0000000000000000001 --library library"
    Then standard output contains "Cited by 1 work in the library"

  # ---- with the work ----

  Scenario: The references follow the work when the library is organized
    Given I run shelf with arguments "refs fetch 01J9Z3K0000000000000000002 --apply --library library"
    When I run shelf with arguments "organize --apply --library library"
    Then the exit code is 0
    And standard output contains "papers/2019/BERT/BERT.pdf"
    And the file "library/papers/2019/BERT/references.json" contains "10.1000/outside"
    And the file "library/Devlin/BERT/references.json" does not exist

  Scenario: Deleting the work takes its references along, and undo brings them back
    Given I run shelf with arguments "refs fetch 01J9Z3K0000000000000000002 --apply --library library"
    When I run shelf with arguments "delete 01J9Z3K0000000000000000002 --yes --library library"
    Then the exit code is 0
    And the file "library/Devlin/BERT/references.json" does not exist
    When I run shelf with arguments "refs list 01J9Z3K0000000000000000001 --library library"
    Then standard output contains "Cited by no work in the library"
    When I run shelf with arguments "undo --library library"
    Then the file "library/Devlin/BERT/references.json" contains "10.1000/outside"

  Scenario: Merging gives the target the references of the source if it has none
    Given I run shelf with arguments "refs fetch 01J9Z3K0000000000000000002 --apply --library library"
    When I run shelf with arguments "merge 01J9Z3K0000000000000000003 01J9Z3K0000000000000000002 --library library"
    Then the exit code is 0
    And the file "library/Notes/Unlisted/references.json" contains "10.1000/outside"
    And the file "library/Devlin/BERT/references.json" does not exist
