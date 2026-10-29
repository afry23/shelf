@ui @F-PAP-11 @F-PAP-12 @PRD-8.2 @M11
Feature: References in the book view
  As a person who looks at one paper
  I want to load its references and to see which ones I own
  So that I move from a paper to the papers it builds on

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
    And the web server is running on the library "library"

  Scenario: A paper with a DOI offers to load its references
    When I open "/works/01J9Z3K0000000000000000002" in the browser
    And I open the "References" tab
    Then the page shows the text "No references loaded"
    And the "Load references from Crossref" button is shown
    And no online source was queried

  Scenario: Loading shows the references and links the ones in the library
    Given I open "/works/01J9Z3K0000000000000000002#references" in the browser
    When I click the "Load references from Crossref" button
    Then the page shows the text "Some Outside Paper"
    And the page shows the text "Peters et al."
    And the link "Attention Is All You Need" points to an address containing "/works/01J9Z3K0000000000000000001"
    And the tab "References" shows the number 3
    And the file "library/Devlin/BERT/references.json" soon contains "10.1000/outside"

  Scenario: The cited paper shows who cites it
    Given I run shelf with arguments "refs fetch 01J9Z3K0000000000000000002 --apply --library library"
    When I open "/works/01J9Z3K0000000000000000001#references" in the browser
    Then the page shows the text "Cited by"
    And the link "BERT" points to an address containing "/works/01J9Z3K0000000000000000002"

  Scenario: A work without references, DOI or citations has no such tab
    When I open "/works/01J9Z3K0000000000000000003" in the browser
    Then the "References" tab is not shown

  Scenario: A source that fails is reported and the list stays
    Given the online source "crossref" fails with "crossref is down"
    And I open "/works/01J9Z3K0000000000000000002#references" in the browser
    When I click the "Load references from Crossref" button
    Then the page shows the text "crossref is down"
    And the file "library/Devlin/BERT/references.json" does not exist
