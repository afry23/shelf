@F-WEB-7 @PRD-7.3 @M12
Feature: Series and type as facets over HTTP
  As the library view of the web interface
  I want the series and the types of the works with their counts
  So that a person narrows the library to a series or to papers

  Background:
    Given an empty working directory
    And I have run shelf with arguments "init library"
    And a sample EPUB "library/Ibryam/Kubernetes Patterns/kp.epub" with the title "Kubernetes Patterns", the authors "Bilgin Ibryam" and the chapters:
      | title | text                                  |
      | One   | The sidecar container extends it.     |
    And the sidecar "library/Ibryam/Kubernetes Patterns/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000001", "title": "Kubernetes Patterns", "authors": ["Bilgin Ibryam"], "language": "en", "year": 2019, "series": "Reusable Elements", "seriesIndex": 1, "files": [{ "name": "kp.epub" }] }
      """
    And a sample PDF "library/Mueller/Cloud Patterns/cp.pdf" with the title "Cloud Patterns", the author "Anna Müller" and the pages:
      | text                                     |
      | Serverless functions scale down to zero. |
    And the sidecar "library/Mueller/Cloud Patterns/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000002", "title": "Cloud Patterns", "authors": ["Anna Müller"], "language": "en", "year": 2020, "series": "reusable elements", "seriesIndex": 2, "files": [{ "name": "cp.pdf" }] }
      """
    And a sample PDF "library/Vaswani/Attention/attention.pdf" with the title "Attention Is All You Need", the author "Ashish Vaswani" and the pages:
      | text                                 |
      | The Transformer relies on attention. |
    And the sidecar "library/Vaswani/Attention/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000003", "kind": "paper", "title": "Attention Is All You Need", "authors": ["Ashish Vaswani"], "language": "en", "year": 2017, "series": "Neural Papers", "files": [{ "name": "attention.pdf" }] }
      """
    And a sample PDF "library/Body/Loose/loose.pdf" with the title "Loose Notes", the author "N. Body" and the pages:
      | text        |
      | Some notes. |
    And the sidecar "library/Body/Loose/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000004", "title": "Loose Notes", "authors": ["N. Body"], "language": "en", "files": [{ "name": "loose.pdf" }] }
      """
    And I have run shelf with arguments "reindex --library library"
    And the web server is running on the library "library"

  Scenario: The facets count the series, whatever the case it was written in
    When I request "GET /api/v1/works"
    Then the response status is 200
    And the JSON array "facets.series" of the response has 2 entries
    And the JSON field "facets.series[0].count" of the response is 2
    And the JSON field "facets.series[1].value" of the response is "Neural Papers"
    And the JSON field "facets.series[1].count" of the response is 1

  Scenario: The facets count the types
    When I request "GET /api/v1/works"
    Then the JSON array "facets.types" of the response has 2 entries
    And the JSON field "facets.types[0].value" of the response is "book"
    And the JSON field "facets.types[0].count" of the response is 3
    And the JSON field "facets.types[1].value" of the response is "paper"
    And the JSON field "facets.types[1].count" of the response is 1

  Scenario Outline: A series or a type narrows the list and the facets
    When I request "GET /api/v1/works?q=<q>"
    Then the response status is 200
    And the JSON field "total" of the response is <total>

    Examples:
      | q                                       | total |
      | series%3A%22Reusable%20Elements%22      | 2     |
      | series%3A%22neural%20papers%22          | 1     |
      | type%3Apaper                            | 1     |
      | -series%3A%22Reusable%20Elements%22     | 2     |

  Scenario: The facets of a narrowed list count only the works that pass
    When I request "GET /api/v1/works?q=type%3Apaper"
    Then the JSON array "facets.series" of the response has 1 entries
    And the JSON field "facets.series[0].value" of the response is "Neural Papers"
    And the JSON array "facets.types" of the response has 1 entries
