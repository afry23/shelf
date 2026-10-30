@PRD-7.3 @F-ART-2 @F-ART-8 @M13
Feature: Keeping an article over HTTP
  As the library view of the web interface
  I want to create an article from the address of a page and to show its images
  So that I can add an article from the browser, and only when I ask for it

  Background:
    Given an empty working directory
    And I have run shelf with arguments "init library"

  Scenario: A public page is kept as an article
    Given the online source "web" serves the article at "https://blog.example.com/why-tests-matter" titled "Why Tests Matter" by "Jane Doe" with one image
    And the web server is running on the library "library"
    When I send "POST /api/v1/works/clip" with the JSON:
      """
      { "url": "https://blog.example.com/why-tests-matter?utm_source=feed#top" }
      """
    Then the response status is 200
    And the JSON field "title" of the response is "Why Tests Matter"
    And the JSON field "images" of the response is 1
    And the JSON field "existing" of the response is false
    And the JSON field "folder" of the response contains "Example Blog/Why Tests Matter"

  Scenario: An article that is in the library already is reported, not made again
    Given the online source "web" serves the article at "https://blog.example.com/why-tests-matter" titled "Why Tests Matter" by "Jane Doe" with one image
    And the web server is running on the library "library"
    And I send "POST /api/v1/works/clip" with the JSON:
      """
      { "url": "https://blog.example.com/why-tests-matter" }
      """
    When I send "POST /api/v1/works/clip" with the JSON:
      """
      { "url": "https://blog.example.com/why-tests-matter#comments" }
      """
    Then the response status is 200
    And the JSON field "existing" of the response is true

  Scenario Outline: Unusable requests are refused before a page is loaded
    Given the web server is running on the library "library"
    When I send "POST /api/v1/works/clip" with the JSON:
      """
      <body>
      """
    Then the response status is 400
    And the JSON field "detail" of the response contains "<message>"
    And no online source was queried

    Examples:
      | body                           | message                |
      | {}                             | url is required        |
      | { "url": 7 }                   | url                    |
      | { "url": "post.html" }         | is not a web address   |
      | { "url": "file:///etc/hosts" } | is not a web address   |

  Scenario: A page that cannot be loaded is a conflict and creates nothing
    Given the online source "web" fails with "503"
    And the web server is running on the library "library"
    When I send "POST /api/v1/works/clip" with the JSON:
      """
      { "url": "https://blog.example.com/other" }
      """
    Then the response status is 409
    And the JSON field "detail" of the response contains "503"

  Scenario Outline: The images of an article are served from its folder and nothing else
    Given an article "Why Tests Matter" from the site "Example Blog" with the text "Tests document intent." and 1 images and the ID "01J9Z3K0000000000000000A01"
    And the web server is running on the library "library"
    When I request "GET <path>"
    Then the response status is <status>
    And the response header "Content-Type" <type>

    Examples:
      | path                                                                   | status | type                           |
      | /api/v1/files/01J9Z3K0000000000000000A01~article.md/assets/01-00000001.jpg | 200    | is "image/jpeg"                |
      | /api/v1/files/01J9Z3K0000000000000000A01~article.md/assets/missing.jpg     | 404    | contains "json"                |
      | /api/v1/files/01J9Z3K0000000000000000A01~article.md/assets/book.json       | 404    | contains "json"                |
      | /api/v1/files/01J9Z3K0000000000000000A01~article.md/assets/..%2Fbook.json  | 404    | contains "json"                |
      | /api/v1/files/01J9Z3K0000000000000000A01~article.md                        | 200    | is "text/markdown; charset=utf-8" |
