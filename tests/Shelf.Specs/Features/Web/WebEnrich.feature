@PRD-7.3 @F-IMP-6 @M5
Feature: Loading book metadata from the internet over HTTP
  As the work view of the web interface
  I want to preview and apply the metadata that Open Library and Google Books know
  So that I can fill in a book from the browser, and only when I ask for it

  Background:
    Given an empty working directory
    And I have run shelf with arguments "init library"
    And a sample EPUB "library/Ibryam/Kubernetes Patterns/kp.epub" with the title "Kubernetes Patterns", the authors "Bilgin Ibryam" and the text seed 1
    And the sidecar "library/Ibryam/Kubernetes Patterns/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000001", "title": "Kubernetes Patterns", "authors": ["Bilgin Ibryam"], "isbn13": "9781492034926", "year": 2019, "files": [{ "name": "kp.epub" }] }
      """
    And I have run shelf with arguments "reindex --library library"
    And the online source "openlibrary" holds the book:
      | field       | value                           |
      | isbn13      | 9781492034926                   |
      | title       | Kubernetes Patterns             |
      | authors     | Bilgin Ibryam                   |
      | publisher   | O'Reilly Media                  |
      | year        | 2020                            |
      | language    | en                              |
      | description | Design patterns for Kubernetes. |
    And the web server is running on the library "library"

  Scenario: The preview lists what would change and writes nothing
    When I send "POST /api/v1/works/01J9Z3K0000000000000000001/enrich" with the JSON:
      """
      {}
      """
    Then the response status is 200
    And the JSON field "source" of the response is "openlibrary"
    And the JSON field "match" of the response contains "9781492034926"
    And the JSON array "changes" of the response has 3 entries
    And the JSON field "changes[0].field" of the response is "publisher"
    And the JSON field "changes[0].after" of the response is "O'Reilly Media"
    And the JSON field "kept[0].field" of the response is "year"
    And the JSON field "kept[0].online" of the response is "2020"
    When I request "GET /api/v1/works/01J9Z3K0000000000000000001"
    Then the JSON field "publisher" of the response is null

  Scenario: Applying fills the empty fields and keeps the others
    When I send "POST /api/v1/works/01J9Z3K0000000000000000001/enrich/apply" with the JSON:
      """
      {}
      """
    Then the response status is 200
    And the JSON array "fields" of the response includes "publisher"
    And the JSON array "fields" of the response does not include "year"
    When I request "GET /api/v1/works/01J9Z3K0000000000000000001"
    Then the JSON field "publisher" of the response is "O'Reilly Media"
    And the JSON field "year" of the response is 2019

  Scenario: Overwrite replaces values that are already set
    When I send "POST /api/v1/works/01J9Z3K0000000000000000001/enrich/apply" with the JSON:
      """
      { "overwrite": true }
      """
    Then the response status is 200
    And the JSON array "fields" of the response includes "year"
    When I request "GET /api/v1/works/01J9Z3K0000000000000000001"
    Then the JSON field "year" of the response is 2020

  Scenario: The cover is saved when the work has none
    Given the online source "openlibrary" has a cover of type "image/jpeg" for the ISBN "9781492034926"
    When I send "POST /api/v1/works/01J9Z3K0000000000000000001/enrich/apply" with the JSON:
      """
      {}
      """
    Then the response status is 200
    And the JSON field "cover" of the response is "cover.jpg"
    And the file "library/Ibryam/Kubernetes Patterns/cover.jpg" exists

  Scenario: The cover is left out when the request says so
    Given the online source "openlibrary" has a cover of type "image/jpeg" for the ISBN "9781492034926"
    When I send "POST /api/v1/works/01J9Z3K0000000000000000001/enrich/apply" with the JSON:
      """
      { "cover": false }
      """
    Then the response status is 200
    And the file "library/Ibryam/Kubernetes Patterns/cover.jpg" does not exist

  Scenario: Several matches are offered as candidates
    Given the sidecar "library/Ibryam/Kubernetes Patterns/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000001", "title": "Kubernetes Patterns", "authors": ["Bilgin Ibryam"], "files": [{ "name": "kp.epub" }] }
      """
    And the online source "openlibrary" holds the book:
      | field     | value               |
      | title     | Kubernetes Patterns |
      | authors   | Bilgin Ibryam       |
      | publisher | Manning             |
    When I send "POST /api/v1/works/01J9Z3K0000000000000000001/enrich" with the JSON:
      """
      {}
      """
    Then the response status is 200
    And the JSON array "candidates" of the response has 2 entries
    And the JSON field "candidates[1].publisher" of the response is "Manning"
    And the JSON array "changes" of the response has 0 entries
    When I send "POST /api/v1/works/01J9Z3K0000000000000000001/enrich" with the JSON:
      """
      { "pick": 2 }
      """
    Then the response status is 200
    And the JSON field "changes[0].after" of the response is "Manning"

  Scenario: Applying without a choice among candidates is refused
    Given the sidecar "library/Ibryam/Kubernetes Patterns/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000001", "title": "Kubernetes Patterns", "authors": ["Bilgin Ibryam"], "files": [{ "name": "kp.epub" }] }
      """
    And the online source "openlibrary" holds the book:
      | field     | value               |
      | title     | Kubernetes Patterns |
      | authors   | Bilgin Ibryam       |
      | publisher | Manning             |
    When I send "POST /api/v1/works/01J9Z3K0000000000000000001/enrich/apply" with the JSON:
      """
      {}
      """
    Then the response status is 409
    And the JSON field "detail" of the response contains "pick one"

  Scenario: A book that no source knows is a conflict, not a server error
    Given the online source "openlibrary" holds no books
    When I send "POST /api/v1/works/01J9Z3K0000000000000000001/enrich" with the JSON:
      """
      {}
      """
    Then the response status is 409
    And the JSON field "detail" of the response contains "no online source knows this book"

  Scenario: A failing source is reported in the warnings and the next one answers
    Given the online source "openlibrary" fails with "HTTP 429 Too Many Requests"
    And the online source "google" holds the book:
      | field     | value               |
      | isbn13    | 9781492034926       |
      | title     | Kubernetes Patterns |
      | authors   | Bilgin Ibryam       |
      | publisher | O'Reilly Media      |
    When I send "POST /api/v1/works/01J9Z3K0000000000000000001/enrich" with the JSON:
      """
      {}
      """
    Then the response status is 200
    And the JSON field "source" of the response is "google"
    And the JSON array "warnings" of the response includes "openlibrary: HTTP 429 Too Many Requests"

  Scenario Outline: Unusable requests are refused before a source is asked
    When I send "POST /api/v1/works/01J9Z3K0000000000000000001/enrich" with the JSON:
      """
      <body>
      """
    Then the response status is 400
    And the JSON field "detail" of the response contains "<message>"
    And no online source was queried

    Examples:
      | body                | message                                |
      | { "source": "bing" } | source must be openlibrary, google, crossref or arxiv   |
      | { "pick": 0 }       | pick must be 1 or greater              |

  Scenario: An unknown work is not found
    When I send "POST /api/v1/works/01J9Z3K0000000000000000099/enrich" with the JSON:
      """
      {}
      """
    Then the response status is 404
    And no online source was queried

  Scenario: Nothing goes online unless the enrich endpoint is called
    When I request "GET /api/v1/works/01J9Z3K0000000000000000001"
    And I send "PATCH /api/v1/works/01J9Z3K0000000000000000001" with the JSON:
      """
      { "year": 2021 }
      """
    Then no online source was queried
