@F-WEB-9 @PRD-7.3 @M23
Feature: Sort the facet values over HTTP
  As the library view of the web interface
  I want the values of a facet in an order I choose
  So that a person finds an author, a publisher or a year by name or by number of works

  Background:
    Given an empty working directory
    And I have run shelf with arguments "init library"
    And a sample EPUB "library/Ibryam/Kubernetes Patterns/kp.epub" with the title "Kubernetes Patterns", the authors "Bilgin Ibryam" and the chapters:
      | title | text                              |
      | One   | The sidecar container extends it. |
    And the sidecar "library/Ibryam/Kubernetes Patterns/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000001", "title": "Kubernetes Patterns", "authors": ["Bilgin Ibryam"], "language": "en", "publisher": "O'Reilly Media", "year": 2019, "tags": ["devops"], "files": [{ "name": "kp.epub" }] }
      """
    And a sample PDF "library/Mueller/Cloud Patterns/cp.pdf" with the title "Cloud Patterns", the author "Anna Müller" and the pages:
      | text                                     |
      | Serverless functions scale down to zero. |
    And the sidecar "library/Mueller/Cloud Patterns/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000002", "title": "Cloud Patterns", "authors": ["Anna Müller", "Bilgin Ibryam"], "language": "de", "publisher": "Manning", "year": 2020, "tags": ["devops", "cloud"], "files": [{ "name": "cp.pdf" }] }
      """
    And a sample PDF "library/Vaswani/Attention/attention.pdf" with the title "Attention Is All You Need", the author "Ashish Vaswani" and the pages:
      | text                                 |
      | The Transformer relies on attention. |
    And the sidecar "library/Vaswani/Attention/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000003", "title": "Attention Is All You Need", "authors": ["Ashish Vaswani"], "language": "en", "publisher": "Manning", "year": 2017, "tags": ["devops"], "files": [{ "name": "attention.pdf" }] }
      """
    And I have run shelf with arguments "reindex --library library"
    And the web server is running on the library "library"

  Scenario Outline: The values of a facet are sorted by name or by number of works
    When I request "GET /api/v1/works?facetSort=<facet>:<sort>"
    Then the response status is 200
    And the JSON array "facets.<facet>" of the response has <entries> entries
    And the JSON field "facets.<facet>[0].value" of the response is "<first>"
    And the JSON field "facets.<facet>[<last>].value" of the response is "<final>"

    Examples:
      | facet      | sort       | entries | last | first          | final          |
      | authors    | name.asc   | 3       | 2    | Anna Müller    | Bilgin Ibryam  |
      | authors    | name.desc  | 3       | 2    | Bilgin Ibryam  | Anna Müller    |
      | authors    | count.desc | 3       | 2    | Bilgin Ibryam  | Ashish Vaswani |
      | authors    | count.asc  | 3       | 2    | Anna Müller    | Bilgin Ibryam  |
      | publishers | name.asc   | 2       | 1    | Manning        | O'Reilly Media |
      | publishers | name.desc  | 2       | 1    | O'Reilly Media | Manning        |
      | publishers | count.desc | 2       | 1    | Manning        | O'Reilly Media |
      | publishers | count.asc  | 2       | 1    | O'Reilly Media | Manning        |
      | languages  | name.asc   | 2       | 1    | de             | en             |
      | languages  | name.desc  | 2       | 1    | en             | de             |
      | languages  | count.desc | 2       | 1    | en             | de             |
      | languages  | count.asc  | 2       | 1    | de             | en             |
      | tags       | name.asc   | 2       | 1    | cloud          | devops         |
      | tags       | name.desc  | 2       | 1    | devops         | cloud          |
      | tags       | count.desc | 2       | 1    | devops         | cloud          |
      | tags       | count.asc  | 2       | 1    | cloud          | devops         |
      | years      | name.asc   | 3       | 2    | 2017           | 2020           |
      | years      | name.desc  | 3       | 2    | 2020           | 2017           |
      | years      | count.asc  | 3       | 2    | 2017           | 2020           |

  Scenario: Several facets are sorted in one request
    When I request "GET /api/v1/works?facetSort=authors:name.desc,tags:name.asc"
    Then the response status is 200
    And the JSON field "facets.authors[0].value" of the response is "Bilgin Ibryam"
    And the JSON field "facets.tags[0].value" of the response is "cloud"

  Scenario: Without a choice the facets keep their order
    When I request "GET /api/v1/works"
    Then the JSON field "facets.authors[0].value" of the response is "Bilgin Ibryam"
    And the JSON field "facets.authors[0].count" of the response is 2
    And the JSON field "facets.years[0].value" of the response is "2020"

  Scenario: The counts stay the same when the order changes
    When I request "GET /api/v1/works?facetSort=authors:name.asc"
    Then the JSON field "facets.authors[2].value" of the response is "Bilgin Ibryam"
    And the JSON field "facets.authors[2].count" of the response is 2

  Scenario Outline: A bad facet order is refused
    When I request "GET /api/v1/works?facetSort=<value>"
    Then the response status is 400

    Examples:
      | value           |
      | colors:name.asc |
      | authors:size.asc |
      | authors:name.up |
      | authors         |
      | authors:name.asc,authors:count.asc |
