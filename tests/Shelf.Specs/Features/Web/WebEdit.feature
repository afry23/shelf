@PRD-7.3 @F-TAG-5 @F-LIB-6 @M4
Feature: Changing metadata and tags over HTTP
  As the work view of the web interface
  I want to edit the metadata of a work and its tags
  So that I can keep the library in order from the browser

  Background:
    Given an empty working directory
    And I have run shelf with arguments "init library"
    And a sample EPUB "library/Ibryam/Kubernetes Patterns/kp.epub" with the title "Kubernetes Patterns", the authors "Bilgin Ibryam" and the chapters:
      | title                 | text                                              |
      | Foundational Patterns | The sidecar container extends the main container. |
    And the sidecar "library/Ibryam/Kubernetes Patterns/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000001", "title": "Kubernetes Patterns", "subtitle": "Reusable Elements", "authors": ["Bilgin Ibryam"], "language": "en", "year": 2019, "description": "Design patterns for containers", "tags": ["cloud/kubernetes", "devops"], "files": [{ "name": "kp.epub" }] }
      """
    And a sample PDF "library/Alander/Event Sourcing in Practice/es.pdf" with the title "Event Sourcing in Practice", the author "Jördis Ålander" and the pages:
      | text                                            |
      | Event sourcing stores every change as an event. |
    And the sidecar "library/Alander/Event Sourcing in Practice/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000002", "title": "Event Sourcing in Practice", "authors": ["Jördis Ålander"], "language": "en", "year": 2021, "tags": ["architektur/ddd", "cloud/azure"], "files": [{ "name": "es.pdf" }] }
      """
    And I have run shelf with arguments "reindex --library library"
    And the web server is running on the library "library"

  # ---- metadata ----

  Scenario Outline: A field of the metadata is changed and the list shows it at once
    When I send "PATCH /api/v1/works/01J9Z3K0000000000000000001" with the JSON:
      """
      { "<field>": <value> }
      """
    Then the response status is 200
    And the JSON field "<field>" of the response is <shown>
    When I request "GET /api/v1/works/01J9Z3K0000000000000000001"
    Then the JSON field "<field>" of the response is <shown>

    Examples:
      | field       | value                  | shown                  |
      | title       | "Kubernetes in Depth"  | "Kubernetes in Depth"  |
      | subtitle    | "Second Edition"       | "Second Edition"       |
      | language    | "de"                   | "de"                   |
      | year        | 2024                   | 2024                   |
      | publisher   | "O'Reilly"             | "O'Reilly"             |
      | series      | "Patterns"             | "Patterns"             |
      | seriesIndex | 2.5                    | 2.5                    |
      | edition     | "2"                    | "2"                    |
      | citeKey     | "ibryam2019kubernetes" | "ibryam2019kubernetes" |
      | description | "A new description"    | "A new description"    |
      | rating      | 4                      | 4                      |
      | readStatus  | "reading"              | "reading"              |

  Scenario: A cite key is trimmed and written to the sidecar
    When I send "PATCH /api/v1/works/01J9Z3K0000000000000000001" with the JSON:
      """
      { "citeKey": "  ibryam2019:kubernetes_patterns-2nd.ed+1/2  " }
      """
    Then the response status is 200
    And the JSON field "citeKey" of the response is "ibryam2019:kubernetes_patterns-2nd.ed+1/2"
    And the file "library/Ibryam/Kubernetes Patterns/book.json" contains "ibryam2019:kubernetes_patterns-2nd.ed+1/2"

  Scenario: The authors are replaced
    When I send "PATCH /api/v1/works/01J9Z3K0000000000000000001" with the JSON:
      """
      { "authors": ["Bilgin Ibryam", "Roland Huß"] }
      """
    Then the response status is 200
    And the JSON array "authors" of the response has 2 entries
    And the JSON field "authors[1]" of the response is "Roland Huß"

  Scenario: An ISBN-13 is stored and an ISBN-10 gives both
    When I send "PATCH /api/v1/works/01J9Z3K0000000000000000001" with the JSON:
      """
      { "isbn": "978-0-306-40615-7" }
      """
    Then the response status is 200
    And the JSON field "isbn13" of the response is "9780306406157"

  Scenario Outline: A field set to null is removed
    When I send "PATCH /api/v1/works/01J9Z3K0000000000000000001" with the JSON:
      """
      { "<field>": null }
      """
    Then the response status is 200
    And the JSON field "<field>" of the response is null

    Examples:
      | field       |
      | subtitle    |
      | year        |
      | description |
      | language    |
      | citeKey     |

  Scenario: A field that is left out stays
    When I send "PATCH /api/v1/works/01J9Z3K0000000000000000001" with the JSON:
      """
      { "year": 2024 }
      """
    Then the JSON field "title" of the response is "Kubernetes Patterns"
    And the JSON field "subtitle" of the response is "Reusable Elements"
    And the JSON field "tags[0]" of the response is "cloud/kubernetes"

  Scenario: A change is written to the sidecar, the source of truth
    When I send "PATCH /api/v1/works/01J9Z3K0000000000000000001" with the JSON:
      """
      { "title": "Kubernetes in Depth", "rating": 5 }
      """
    Then the response status is 200
    And the file "library/Ibryam/Kubernetes Patterns/book.json" contains "Kubernetes in Depth"

  Scenario Outline: A value that is not valid is refused and nothing is changed
    When I send "PATCH /api/v1/works/01J9Z3K0000000000000000001" with the JSON:
      """
      <body>
      """
    Then the response status is 400
    And the response header "Content-Type" is "application/problem+json"
    And the JSON field "detail" of the response contains "<message>"
    When I request "GET /api/v1/works/01J9Z3K0000000000000000001"
    Then the JSON field "title" of the response is "Kubernetes Patterns"
    And the JSON field "year" of the response is 2019

    Examples:
      | body                                          | message                              |
      | { "title": "" }                               | title: must not be empty             |
      | { "title": null }                             | title: must not be empty             |
      | { "title": 3 }                                | title: must be text                  |
      | { "year": 0, "title": "X" }                   | year: must be between 1 and 9999     |
      | { "year": "2019" }                            | year: must be a whole number         |
      | { "rating": 6, "title": "X" }                 | rating: must be between 1 and 5      |
      | { "readStatus": "done", "title": "X" }        | readStatus: must be unread, reading or read |
      | { "isbn": "123", "title": "X" }               | isbn: '123' is not a valid ISBN      |
      | { "authors": [], "title": "X" }               | authors: give at least one author    |
      | { "authors": ["A", ""], "title": "X" }        | authors: must not be empty           |
      | { "seriesIndex": "one", "title": "X" }        | seriesIndex: must be a number        |
      | { "citeKey": "a b", "title": "X" }            | citeKey: may only contain letters, digits and - _ : . + / |
      | { "citeKey": "a,b", "title": "X" }            | citeKey: may only contain letters, digits and - _ : . + / |
      | { "citeKey": 7 }                              | citeKey: must be text                |
      | { "colour": "red", "title": "X" }             | colour: unknown field                |
      | { "tags": ["a"] }                             | tags: unknown field                  |

  Scenario Outline: A body that is no object is refused
    When I send "PATCH /api/v1/works/01J9Z3K0000000000000000001" with the JSON:
      """
      <body>
      """
    Then the response status is 400
    And the response header "Content-Type" is "application/problem+json"

    Examples:
      | body        |
      | [1, 2]      |
      | "title"     |
      | not json    |
      | {}          |

  Scenario: A change to an unknown work is a 404
    When I send "PATCH /api/v1/works/01J9Z3K0000000000000000009" with the JSON:
      """
      { "title": "X" }
      """
    Then the response status is 404
    And the JSON field "detail" of the response contains "no work matches"

  # ---- tags ----

  Scenario: The tags of the library with the number of works
    When I request "GET /api/v1/tags"
    Then the response status is 200
    And the JSON field "tags" of the response is 4
    And the JSON field "roots[0].name" of the response is "architektur"
    And the JSON field "roots[0].works" of the response is 1
    And the JSON field "roots[0].children[0].path" of the response is "architektur/ddd"
    And the JSON field "roots[1].name" of the response is "cloud"
    And the JSON field "roots[1].works" of the response is 2

  Scenario: The tags of a work are replaced as a set
    When I send "PUT /api/v1/works/01J9Z3K0000000000000000001/tags" with the JSON:
      """
      { "tags": ["devops", "architektur/ddd", "new"] }
      """
    Then the response status is 200
    And the JSON field "id" of the response is "01J9Z3K0000000000000000001"
    And the JSON array "tags" of the response has 3 entries
    And the JSON field "tags[0]" of the response is "architektur/ddd"
    And the JSON field "tags[1]" of the response is "devops"
    And the JSON field "tags[2]" of the response is "new"
    And the JSON field "added" of the response is 2
    And the JSON field "removed" of the response is 1
    When I request "GET /api/v1/works?q=tag%3Anew"
    Then the JSON field "total" of the response is 1

  Scenario: The same set again changes nothing
    When I send "PUT /api/v1/works/01J9Z3K0000000000000000001/tags" with the JSON:
      """
      { "tags": ["devops", "cloud/kubernetes"] }
      """
    Then the response status is 200
    And the JSON field "added" of the response is 0
    And the JSON field "removed" of the response is 0

  Scenario: An empty list removes every tag
    When I send "PUT /api/v1/works/01J9Z3K0000000000000000001/tags" with the JSON:
      """
      { "tags": [] }
      """
    Then the response status is 200
    And the JSON array "tags" of the response has 0 entries
    And the JSON field "removed" of the response is 2

  Scenario: Blanks are removed and a name twice counts once
    When I send "PUT /api/v1/works/01J9Z3K0000000000000000001/tags" with the JSON:
      """
      { "tags": ["devops", " devops ", " cloud / kubernetes "] }
      """
    Then the response status is 200
    And the JSON array "tags" of the response has 2 entries
    And the JSON field "added" of the response is 0

  Scenario Outline: A tag that is not valid is refused and no other tag of the call is applied
    When I send "PUT /api/v1/works/01J9Z3K0000000000000000001/tags" with the JSON:
      """
      <body>
      """
    Then the response status is 400
    And the JSON field "detail" of the response contains "<message>"
    When I request "GET /api/v1/works/01J9Z3K0000000000000000001"
    Then the JSON array "tags" of the response has 2 entries

    Examples:
      | body                              | message                       |
      | { "tags": ["fine", "a//b"] }      | tag 'a//b'                    |
      | { "tags": ["fine", ""] }          | tag ''                        |
      | { "tags": "devops" }              | tags must be a list of text   |
      | { }                               | tags must be a list of text   |

  Scenario: A tag change can be taken back with undo
    Given I send "PUT /api/v1/works/01J9Z3K0000000000000000001/tags" with the JSON:
      """
      { "tags": ["only"] }
      """
    When I run shelf with arguments "undo --library library"
    And I request "GET /api/v1/works/01J9Z3K0000000000000000001"
    Then the JSON array "tags" of the response has 2 entries
    And the JSON field "tags[0]" of the response is "cloud/kubernetes"
