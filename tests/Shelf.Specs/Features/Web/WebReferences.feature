@F-PAP-11 @F-PAP-12 @PRD-7.3 @M11
Feature: References of a paper over HTTP
  As the work view of the web interface
  I want the references of a paper and the papers that cite it
  So that a person follows a line of work with a click

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

  Scenario: A paper without loaded references has an empty list
    When I request "GET /api/v1/works/01J9Z3K0000000000000000002/references"
    Then the response status is 200
    And the JSON array "references" of the response has 0 entries
    And the JSON array "citedBy" of the response has 0 entries
    And the JSON field "fetchedAt" of the response is null

  Scenario: Loading with apply stores the list and returns it
    When I send "POST /api/v1/works/01J9Z3K0000000000000000002/references/fetch" with the JSON:
      """
      { "apply": true }
      """
    Then the response status is 200
    And the JSON field "applied" of the response is true
    And the JSON field "total" of the response is 3
    And the file "library/Devlin/BERT/references.json" contains "10.1000/outside"
    When I request "GET /api/v1/works/01J9Z3K0000000000000000002/references"
    Then the JSON array "references" of the response has 3 entries
    And the JSON field "inLibrary" of the response is 1
    And the JSON field "references[0].work.id" of the response is "01J9Z3K0000000000000000001"
    And the JSON field "references[0].work.title" of the response is "Attention Is All You Need"
    And the JSON field "references[1].work" of the response is null
    And the JSON field "references[2].unstructured" of the response contains "Peters"
    And the JSON field "source" of the response is "crossref"
    And the JSON field "fetchedAt" of the response is not empty

  Scenario: Without apply nothing is stored
    When I send "POST /api/v1/works/01J9Z3K0000000000000000002/references/fetch" with the JSON:
      """
      {}
      """
    Then the response status is 200
    And the JSON field "applied" of the response is false
    And the JSON field "total" of the response is 3
    And the file "library/Devlin/BERT/references.json" does not exist

  Scenario: The cited paper lists the papers that cite it
    Given I send "POST /api/v1/works/01J9Z3K0000000000000000002/references/fetch" with the JSON:
      """
      { "apply": true }
      """
    When I request "GET /api/v1/works/01J9Z3K0000000000000000001/references"
    Then the JSON array "citedBy" of the response has 1 entries
    And the JSON field "citedBy[0].id" of the response is "01J9Z3K0000000000000000002"
    And the JSON field "citedBy[0].title" of the response is "BERT"

  Scenario: A request for the list asks nobody
    When I request "GET /api/v1/works/01J9Z3K0000000000000000002/references"
    Then no online source was queried

  Scenario Outline: A request that cannot be answered is a problem document
    When I send "<method> <path>" with the JSON:
      """
      <body>
      """
    Then the response status is <status>
    And the response header "Content-Type" starts with "application/problem+json"

    Examples:
      | method | path                                                           | body                | status |
      | GET    | /api/v1/works/01J9Z3K9000000000000000009/references            | {}                  | 404    |
      | POST   | /api/v1/works/01J9Z3K9000000000000000009/references/fetch      | { "apply": true }   | 404    |
      | POST   | /api/v1/works/01J9Z3K0000000000000000003/references/fetch      | { "apply": true }   | 400    |
      | POST   | /api/v1/works/01J9Z3K0000000000000000002/references/fetch      | { "apply": "yes" }  | 400    |
