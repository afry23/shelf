@F-IMP-5 @PRD-7.3 @M4
Feature: Cover images over HTTP
  As the library grid of the web interface
  I want the cover of a work as an image
  So that a book can be found by its picture

  Background:
    Given an empty working directory
    And I have run shelf with arguments "init library"
    And the folder "incoming" contains the fixtures "derived/epub2-isbn-cover.epub, derived/Event Streams in Practice.pdf, derived/Jane Doe - Quiet Systems (2004).epub"
    And I have run shelf with arguments "import incoming --library library"
    And the web server is running on the library "library"

  Scenario Outline: The cover is sent as the image it is
    Given I look up the ID of the work "<title>"
    When I request "GET /api/v1/works/{id}/cover"
    Then the response status is 200
    And the response header "Content-Type" is "<type>"
    And the response body is the cover "<file>" of the work "<title>"

    Examples:
      | title                           | type       | file      |
      | Größenordnungen verteilter Systeme | image/png  | cover.png |
      | Event Streams in Practice       | image/jpeg | cover.jpg |

  Scenario: A cover that has not changed is not sent again
    Given I look up the ID of the work "Event Streams"
    And I request "GET /api/v1/works/{id}/cover"
    When I request "GET /api/v1/works/{id}/cover" again with the ETag
    Then the response status is 304

  Scenario: The list says which works have a cover
    When I request "GET /api/v1/works?sort=title"
    Then the JSON field "items[0].title" of the response is "Event Streams in Practice"
    And the JSON field "items[0].hasCover" of the response is true
    And the JSON field "items[1].title" of the response is "Größenordnungen verteilter Systeme"
    And the JSON field "items[1].hasCover" of the response is true
    And the JSON field "items[2].title" of the response is "Quiet Systems"
    And the JSON field "items[2].hasCover" of the response is false

  Scenario: The details say it too
    Given I look up the ID of the work "Quiet Systems"
    When I request "GET /api/v1/works/{id}"
    Then the JSON field "hasCover" of the response is false

  Scenario: A work without a cover is a 404 with the reason
    Given I look up the ID of the work "Quiet Systems"
    When I request "GET /api/v1/works/{id}/cover"
    Then the response status is 404
    And the JSON field "detail" of the response contains "has no cover"

  Scenario: A cover that is gone from the disk is a 404
    Given I look up the ID of the work "Event Streams"
    And the cover of "Event Streams" is deleted from the disk
    When I request "GET /api/v1/works/{id}/cover"
    Then the response status is 404
    And the JSON field "detail" of the response contains "is missing"

  Scenario: An unknown work is a 404
    When I request "GET /api/v1/works/01J9Z3K0000000000000000009/cover"
    Then the response status is 404
    And the JSON field "detail" of the response contains "no work matches"

  Scenario: A cover needs the access token like every call of the API
    Given I look up the ID of the work "Event Streams"
    And the web server is running on the library "library" with the arguments "--token s3cret"
    When I request "GET /api/v1/works/{id}/cover"
    Then the response status is 401
