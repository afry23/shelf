@F-ANN-8 @PRD-7.3 @M11
Feature: A passage as a quote over HTTP
  As the reader of the web interface
  I want a selected passage as a quote with its citation
  So that a person pastes it into their text

  Background:
    Given an empty working directory
    And I have run shelf with arguments "init library"
    And a sample PDF "library/Alander/Event Sourcing in Practice/es.pdf" with the title "Event Sourcing in Practice", the author "Jördis Ålander" and the pages:
      | text                                            |
      | Event sourcing stores every change as an event. |
      | A projection builds a read model.               |
    And the sidecar "library/Alander/Event Sourcing in Practice/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000002", "title": "Event Sourcing in Practice", "authors": ["Jördis Ålander"], "language": "en", "year": 2021, "citeKey": "alander2021event", "files": [{ "name": "es.pdf" }] }
      """
    And a sample PDF "library/Nobody/No Key/nk.pdf" with the title "No Key", the author "N. Body" and the pages:
      | text            |
      | A page of text. |
    And the sidecar "library/Nobody/No Key/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000003", "title": "No Key", "authors": ["N. Body"], "language": "en", "files": [{ "name": "nk.pdf" }] }
      """
    And I have run shelf with arguments "reindex --library library"
    And the web server is running on the library "library"

  Scenario: The quote of a passage with the place of the file
    When I send "POST /api/v1/files/01J9Z3K0000000000000000002~es.pdf/quote" with the JSON:
      """
      { "quote": "stores every change", "locator": "page=1" }
      """
    Then the response status is 200
    And the JSON field "id" of the response is "01J9Z3K0000000000000000002"
    And the JSON field "citeKey" of the response is "alander2021event"
    And the JSON field "place" of the response is "page 1"
    And the JSON field "text" of the response contains "> stores every change"
    And the JSON field "text" of the response contains "[@alander2021event, p. 1]"
    And the JSON field "bibtex" of the response contains "alander2021event,"

  Scenario: A quote without a place cites the work only
    When I send "POST /api/v1/files/01J9Z3K0000000000000000002~es.pdf/quote" with the JSON:
      """
      { "quote": "stores every change" }
      """
    Then the response status is 200
    And the JSON field "place" of the response is null
    And the JSON field "text" of the response contains "[@alander2021event]"

  Scenario: A request changes nothing
    Given I take a snapshot of the folder "library"
    When I send "POST /api/v1/files/01J9Z3K0000000000000000002~es.pdf/quote" with the JSON:
      """
      { "quote": "stores every change", "locator": "page=1" }
      """
    Then the folder "library" equals the snapshot

  Scenario Outline: A request that cannot be answered is a problem document
    When I send "POST /api/v1/files/<file>/quote" with the JSON:
      """
      <body>
      """
    Then the response status is <status>
    And the response header "Content-Type" starts with "application/problem+json"

    Examples:
      | file                               | body                                        | status |
      | 01J9Z3K0000000000000000003~nk.pdf  | { "quote": "x", "locator": "page=1" }       | 400    |
      | 01J9Z3K0000000000000000002~es.pdf  | { "quote": "  " }                           | 400    |
      | 01J9Z3K0000000000000000002~es.pdf  | { }                                         | 400    |
      | 01J9Z3K0000000000000000002~es.pdf  | { "quote": "x", "locator": "chapter 2" }    | 400    |
      | 01J9Z3K0000000000000000002~gone.pdf | { "quote": "x" }                           | 404    |
