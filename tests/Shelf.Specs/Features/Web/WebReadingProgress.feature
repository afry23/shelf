@F-RDR-4 @F-RDR-5 @PRD-7.3 @M5
Feature: Reading progress and the preferred format over HTTP
  As the reader of the web interface
  I want to save where I am in a file and to know which file of a work to open
  So that a book opens where I stopped, in the format I prefer

  Background:
    Given an empty working directory
    And I have run shelf with arguments "init library"
    And a sample EPUB "library/Ibryam/Kubernetes Patterns/kp.epub" with the title "Kubernetes Patterns", the authors "Bilgin Ibryam" and the chapters:
      | title                 | text                                                       |
      | Foundational Patterns | The sidecar container extends the main container.          |
      | Behavioral Patterns   | A batch job runs to completion.                            |
    And a sample PDF "library/Ibryam/Kubernetes Patterns/kp.pdf" with the title "Kubernetes Patterns", the author "Bilgin Ibryam" and the pages:
      | text                                  |
      | The sidecar container extends the main container. |
      | A batch job runs to completion.       |
    And the sidecar "library/Ibryam/Kubernetes Patterns/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000001", "title": "Kubernetes Patterns", "authors": ["Bilgin Ibryam"], "language": "en", "year": 2019, "files": [{ "name": "kp.pdf" }, { "name": "kp.epub" }] }
      """
    And a sample PDF "library/Alander/Event Sourcing in Practice/es.pdf" with the title "Event Sourcing in Practice", the author "Jördis Ålander" and the pages:
      | text                                              |
      | Event sourcing stores every change as an event.   |
      | A projection builds a read model.                 |
    And the sidecar "library/Alander/Event Sourcing in Practice/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000002", "title": "Event Sourcing in Practice", "authors": ["Jördis Ålander"], "language": "en", "year": 2021, "files": [{ "name": "es.pdf" }] }
      """
    And I have run shelf with arguments "reindex --library library"

  # ---- F-RDR-4: saving the position ----

  Scenario: A file has no progress until a reader saves one
    Given the web server is running on the library "library"
    When I request "GET /api/v1/works/01J9Z3K0000000000000000002"
    Then the JSON field "files[0].progress" of the response is null

  Scenario Outline: The position is saved in the sidecar and shown with the file
    Given the web server is running on the library "library"
    When I send "PUT /api/v1/files/<file>/progress" with the JSON:
      """
      { "locator": "<locator>" }
      """
    Then the response status is 200
    And the JSON field "locator" of the response is "<locator>"
    And the JSON field "updatedAt" of the response is not empty
    When I request "GET /api/v1/works/<work>"
    Then the JSON field "files[<index>].progress.locator" of the response is "<locator>"
    And the file "library/<folder>/book.json" contains "<locator>"

    Examples:
      | work                       | file                                    | index | folder                          | locator                         |
      | 01J9Z3K0000000000000000002 | 01J9Z3K0000000000000000002~es.pdf       | 0     | Alander/Event Sourcing in Practice | page=2                       |
      | 01J9Z3K0000000000000000001 | 01J9Z3K0000000000000000001~kp.epub      | 1     | Ibryam/Kubernetes Patterns      | epubcfi(/6/4!/4/2/1:0)          |

  Scenario: The progress of one file does not touch the other file of the work
    Given the web server is running on the library "library"
    When I send "PUT /api/v1/files/01J9Z3K0000000000000000001~kp.pdf/progress" with the JSON:
      """
      { "locator": "page=2" }
      """
    And I request "GET /api/v1/works/01J9Z3K0000000000000000001"
    Then the JSON field "files[0].progress.locator" of the response is "page=2"
    And the JSON field "files[1].progress" of the response is null

  Scenario: A later position replaces the earlier one
    Given the web server is running on the library "library"
    When I send "PUT /api/v1/files/01J9Z3K0000000000000000002~es.pdf/progress" with the JSON:
      """
      { "locator": "page=2" }
      """
    And I send "PUT /api/v1/files/01J9Z3K0000000000000000002~es.pdf/progress" with the JSON:
      """
      { "locator": "page=1" }
      """
    And I request "GET /api/v1/works/01J9Z3K0000000000000000002"
    Then the JSON field "files[0].progress.locator" of the response is "page=1"

  Scenario: The progress survives a reindex, because the sidecar is the source of truth
    Given the web server is running on the library "library"
    And I send "PUT /api/v1/files/01J9Z3K0000000000000000002~es.pdf/progress" with the JSON:
      """
      { "locator": "page=2" }
      """
    When I run shelf with arguments "reindex --library library"
    And I request "GET /api/v1/works/01J9Z3K0000000000000000002"
    Then the JSON field "files[0].progress.locator" of the response is "page=2"
    When I run shelf with arguments "show 01J9Z3K0000000000000000002 --library library"
    Then standard output contains "reading position page=2"

  Scenario Outline: A position that is not a location in this kind of file is refused and changes nothing
    Given the web server is running on the library "library"
    And I take a snapshot of the folder "library"
    When I send "PUT /api/v1/files/<file>/progress" with the JSON:
      """
      <body>
      """
    Then the response status is 400
    And the JSON field "detail" of the response contains "<reason>"
    And the folder "library" equals the snapshot

    Examples:
      | file                               | body                          | reason         |
      | 01J9Z3K0000000000000000002~es.pdf  | { "locator": "page=0" }       | page           |
      | 01J9Z3K0000000000000000002~es.pdf  | { "locator": "page=x" }       | page           |
      | 01J9Z3K0000000000000000002~es.pdf  | { "locator": "epubcfi(/6/4)" } | page          |
      | 01J9Z3K0000000000000000001~kp.epub | { "locator": "page=2" }       | epubcfi        |
      | 01J9Z3K0000000000000000001~kp.epub | { "locator": "epubcfi(/6/4" } | epubcfi        |
      | 01J9Z3K0000000000000000002~es.pdf  | { "locator": "" }             | locator        |
      | 01J9Z3K0000000000000000002~es.pdf  | { "locator": 3 }              | locator        |
      | 01J9Z3K0000000000000000002~es.pdf  | {}                            | locator        |

  Scenario Outline: A file that is not in the library has no progress
    Given the web server is running on the library "library"
    When I send "PUT /api/v1/files/<file>/progress" with the JSON:
      """
      { "locator": "page=1" }
      """
    Then the response status is 404

    Examples:
      | file                               |
      | 01J9Z3K0000000000000000002~gone.pdf |
      | 01J9Z3K0000000000000000009~es.pdf  |
      | not-an-id                          |

  # ---- F-RDR-5: the file to read ----

  Scenario: Without a setting a work with an EPUB and a PDF is read as EPUB
    Given the web server is running on the library "library"
    When I request "GET /api/v1/works/01J9Z3K0000000000000000001"
    Then the JSON field "readFile" of the response is "01J9Z3K0000000000000000001~kp.epub"

  Scenario: The format can be chosen in the config of the library
    Given the config file of the library "library" contains the line "reader.preferred_format = 'pdf'"
    And the web server is running on the library "library"
    When I request "GET /api/v1/works/01J9Z3K0000000000000000001"
    Then the JSON field "readFile" of the response is "01J9Z3K0000000000000000001~kp.pdf"

  Scenario: A work without the preferred format is read in the format it has
    Given the web server is running on the library "library"
    When I request "GET /api/v1/works/01J9Z3K0000000000000000002"
    Then the JSON field "readFile" of the response is "01J9Z3K0000000000000000002~es.pdf"
