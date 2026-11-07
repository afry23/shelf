@F-LNK-5 @M22
Feature: Links over HTTP
  As the web interface
  I want to read and change the links of a work through the API
  So that a person can link works from the browser

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
    And the web server is running on the library "library"

  Scenario: A work without links
    When I request "GET /api/v1/works/01J9Z3K0000000000000000001/links"
    Then the response status is 200
    And the JSON array "links" of the response has 0 entries

  Scenario Outline: A link is made from either side and shows on both
    When I send "PUT /api/v1/works/<from>/links/<to>" with the JSON:
      """
      { "note": "<note>", "otherNote": "<otherNote>" }
      """
    Then the response status is 200
    When I request "GET /api/v1/works/<from>/links"
    Then the JSON array "links" of the response has 1 entries
    And the JSON field "links[0].id" of the response is "<to>"
    And the JSON field "links[0].note" of the response is "<note>"
    When I request "GET /api/v1/works/<to>/links"
    Then the JSON field "links[0].id" of the response is "<from>"
    And the JSON field "links[0].note" of the response is "<otherNote>"

    Examples:
      | from                       | to                         | note             | otherNote   |
      | 01J9Z3K0000000000000000001 | 01J9Z3K0000000000000000002 | background       | applied in  |
      | 01J9Z3K0000000000000000002 | 01J9Z3K0000000000000000003 | security view    | cited by    |

  Scenario: A link shows the other work
    Given I send "PUT /api/v1/works/01J9Z3K0000000000000000001/links/01J9Z3K0000000000000000002" with the JSON:
      """
      { "note": "background" }
      """
    When I request "GET /api/v1/works/01J9Z3K0000000000000000001/links"
    Then the JSON field "links[0].title" of the response is "Attention Is All You Need"
    And the JSON field "links[0].kind" of the response is "paper"
    And the JSON field "links[0].authors[0]" of the response is "Ashish Vaswani"

  Scenario: Putting the link again replaces the note
    Given I send "PUT /api/v1/works/01J9Z3K0000000000000000001/links/01J9Z3K0000000000000000002" with the JSON:
      """
      { "note": "first" }
      """
    When I send "PUT /api/v1/works/01J9Z3K0000000000000000001/links/01J9Z3K0000000000000000002" with the JSON:
      """
      { "note": "second" }
      """
    Then the response status is 200
    When I request "GET /api/v1/works/01J9Z3K0000000000000000001/links"
    Then the JSON array "links" of the response has 1 entries
    And the JSON field "links[0].note" of the response is "second"

  Scenario: A link is removed from both works
    Given I send "PUT /api/v1/works/01J9Z3K0000000000000000001/links/01J9Z3K0000000000000000002" with the JSON:
      """
      {}
      """
    When I request "DELETE /api/v1/works/01J9Z3K0000000000000000002/links/01J9Z3K0000000000000000001"
    Then the response status is 200
    When I request "GET /api/v1/works/01J9Z3K0000000000000000001/links"
    Then the JSON array "links" of the response has 0 entries

  Scenario Outline: A request that cannot be met is answered with a reason
    When I send "<request>" with the JSON:
      """
      <body>
      """
    Then the response status is <status>

    Examples:
      | request                                                                                              | body                  | status |
      | PUT /api/v1/works/01J9Z3K0000000000000000001/links/01J9Z3K0000000000000000001                        | {}                    | 400    |
      | PUT /api/v1/works/01J9Z3K0000000000000000001/links/01J9Z3K0000000000000000099                        | {}                    | 404    |
      | PUT /api/v1/works/01J9Z3K0000000000000000099/links/01J9Z3K0000000000000000001                        | {}                    | 404    |
      | PUT /api/v1/works/01J9Z3K0000000000000000001/links/01J9Z3K0000000000000000002                        | { "note": 5 }         | 400    |
      | DELETE /api/v1/works/01J9Z3K0000000000000000001/links/01J9Z3K0000000000000000002                      | {}                    | 404    |
