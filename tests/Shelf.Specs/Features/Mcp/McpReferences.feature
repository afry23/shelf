@F-PAP-11 @F-PAP-12 @PRD-7.2 @M11
Feature: References of a paper through MCP
  As an AI assistant
  I want to see what a paper cites and who cites it
  So that I can follow a line of work through the library of the person

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
    And I have run shelf with arguments "refs fetch 01J9Z3K0000000000000000002 --apply --library library"
    And the online questions are forgotten
    And the MCP server is running on the library "library"

  Scenario: get_references gives the references of a paper and which of them are in the library
    When I call the tool "get_references" with the arguments:
      """
      { "id": "01J9Z3K0000000000000000002" }
      """
    Then the tool call succeeds
    And the result has the string "title" equal to "BERT"
    And the result has the number "total" equal to 3
    And the result has the number "inLibrary" equal to 1
    And the result has the string "references[0].work.id" equal to "01J9Z3K0000000000000000001"

  Scenario: get_references gives the papers that cite a paper
    When I call the tool "get_references" with the arguments:
      """
      { "id": "01J9Z3K0000000000000000001" }
      """
    Then the tool call succeeds
    And the result has the array "citedBy" with 1 items
    And the result has the string "citedBy[0].title" equal to "BERT"

  Scenario: get_references asks nobody and changes nothing
    Given I take a snapshot of the folder "library"
    When I call the tool "get_references" with the arguments:
      """
      { "id": "01J9Z3K0000000000000000002" }
      """
    Then no online source was queried
    And the folder "library" equals the snapshot

  Scenario: An unknown work is refused
    When I call the tool "get_references" with the arguments:
      """
      { "id": "01ZZZZZZZZZZZZZZZZZZZZZZZZ" }
      """
    Then the tool call fails with the message containing "work"

  Scenario: get_references only reads
    Then the tool "get_references" is read-only
