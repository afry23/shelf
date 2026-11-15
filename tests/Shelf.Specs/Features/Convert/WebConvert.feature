@F-CNV-4 @F-CNV-1 @M24
Feature: Converting a PDF into an EPUB over HTTP
  As the work page of the web interface
  I want to ask the server to turn the PDF of a work into a reflowable EPUB
  So that I do not need the command line to read a book on my Kindle

  Background:
    Given an empty working directory
    And I have run shelf with arguments "init library"
    And a sample PDF "library/Alander/Event Sourcing in Practice/es.pdf" with the title "Event Sourcing in Practice", the author "Jördis Ålander" and the pages:
      | text                                            |
      | Event sourcing stores every change as an event. |
    And the sidecar "library/Alander/Event Sourcing in Practice/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000002", "title": "Event Sourcing in Practice", "authors": ["Jördis Ålander"], "language": "en", "year": 2021, "files": [{ "name": "es.pdf" }] }
      """
    And I have run shelf with arguments "reindex --library library"

  Scenario: The EPUB becomes a file of the work
    Given the converter is configured as "fakeconv {input} {output}"
    And the converter writes an EPUB with the chapter text "Event sourcing stores every change as an event."
    And the web server is running on the library "library"
    When I send "POST /api/v1/works/01J9Z3K0000000000000000002/convert" with the JSON:
      """
      {}
      """
    Then the response status is 200
    And the JSON field "file" of the response is "es.epub"
    And the JSON field "source" of the response is "es.pdf"
    And the file "library/Alander/Event Sourcing in Practice/es.epub" exists
    And the file "es.epub" in the index has the role "book"

  Scenario: No body is the same as an empty one
    Given the converter is configured as "fakeconv {input} {output}"
    And the converter writes an EPUB with the chapter text "Event sourcing stores every change as an event."
    And the web server is running on the library "library"
    When I request "POST /api/v1/works/01J9Z3K0000000000000000002/convert"
    Then the response status is 200

  Scenario: A work with an EPUB is a conflict without replace and converted again with it
    Given the converter is configured as "fakeconv {input} {output}"
    And the converter writes an EPUB with the chapter text "First run."
    And I have run shelf with arguments "convert 01J9Z3K --library library"
    And the converter writes an EPUB with the chapter text "Second run."
    And the web server is running on the library "library"
    When I send "POST /api/v1/works/01J9Z3K0000000000000000002/convert" with the JSON:
      """
      {}
      """
    Then the response status is 409
    And the response body contains "already has an EPUB"
    When I send "POST /api/v1/works/01J9Z3K0000000000000000002/convert" with the JSON:
      """
      {"replace": true}
      """
    Then the response status is 200
    And the JSON field "replaced" of the response is "es.epub"

  Scenario: The PDF can be named by its file ID
    Given the converter is configured as "fakeconv {input} {output}"
    And the converter writes an EPUB with the chapter text "Event sourcing stores every change as an event."
    And the web server is running on the library "library"
    When I send "POST /api/v1/works/01J9Z3K0000000000000000002/convert" with the JSON:
      """
      {"file": "01J9Z3K0000000000000000002~es.pdf"}
      """
    Then the response status is 200
    And the JSON field "source" of the response is "es.pdf"

  Scenario: No converter is configured
    Given the web server is running on the library "library"
    When I send "POST /api/v1/works/01J9Z3K0000000000000000002/convert" with the JSON:
      """
      {}
      """
    Then the response status is 400
    And the response body contains "convert.command"

  Scenario: The converter fails
    Given the converter is configured as "fakeconv {input} {output}"
    And the converter fails with "pdftohtml: cannot open input"
    And the web server is running on the library "library"
    When I send "POST /api/v1/works/01J9Z3K0000000000000000002/convert" with the JSON:
      """
      {}
      """
    Then the response status is 502
    And the response body contains "pdftohtml: cannot open input"
    And the table "files" of the index has 1 rows

  Scenario Outline: A request that cannot be met is refused
    Given the converter is configured as "fakeconv {input} {output}"
    And the web server is running on the library "library"
    When I send "POST /api/v1/works/<work>/convert" with the JSON:
      """
      <body>
      """
    Then the response status is <status>
    And the converter was not called

    Examples:
      | work                       | body                        | status |
      | 01J9Z3K0000000000000000099 | {}                          | 404    |
      | not-an-id                  | {}                          | 404    |
      | 01J9Z3K0000000000000000002 | {"replace": "yes"}          | 400    |
      | 01J9Z3K0000000000000000002 | {"file": 5}                 | 400    |
      | 01J9Z3K0000000000000000002 | {"file": "nowhere~es.pdf"}  | 404    |
