@PRD-7.3 @M4
Feature: The web API frame
  As a client of the web API
  I want JSON answers with one error format and a machine-readable description
  So that the web interface and other tools can rely on them

  Background:
    Given an empty working directory
    And I have run shelf with arguments "init library"
    And a sample EPUB "incoming/kp.epub" with the title "Kubernetes Patterns", the authors "Bilgin Ibryam" and the chapters:
      | title                 | text                                                         |
      | Foundational Patterns | The sidecar container extends the main container.           |
    And a sample PDF "incoming/es.pdf" with the title "Event Sourcing in Practice", the author "Jördis Ålander" and the pages:
      | text                                                              |
      | Event sourcing stores every change as an event.                   |
    And I have run shelf with arguments "import incoming --library library"
    And the web server is running on the library "library"

  Scenario: The status reports the version and the size of the library
    When I request "GET /api/v1/status"
    Then the response status is 200
    And the response header "Content-Type" starts with "application/json"
    And the JSON field "version" of the response is not empty
    And the JSON field "works" of the response is 2
    And the JSON field "files" of the response is 2

  Scenario Outline: A request that cannot be answered is a problem document
    When I request "<request>"
    Then the response status is <status>
    And the response header "Content-Type" is "application/problem+json"
    And the JSON field "status" of the response is <status>
    And the JSON field "title" of the response is "<title>"

    Examples:
      | request                     | status | title              |
      | GET /api/v1/nothing         | 404    | Not Found          |
      | GET /api/v1/status/extra    | 404    | Not Found          |
      | POST /api/v1/status         | 405    | Method Not Allowed |
      | DELETE /api/v1/status       | 405    | Method Not Allowed |

  Scenario: A wrong method names the allowed ones
    When I request "POST /api/v1/status"
    Then the response header "Allow" contains "GET"

  Scenario: The OpenAPI document describes the API
    When I request "GET /api/v1/openapi.json"
    Then the response status is 200
    And the response is a JSON document
    And the JSON field "openapi" of the response starts with "3."
    And the JSON document has the path "/api/v1/status" with the method "get"

  Scenario: The OpenAPI document needs the token when there is one
    Given the web server is running on the library "library" with the arguments "--token s3cret"
    When I request "GET /api/v1/openapi.json"
    Then the response status is 401
