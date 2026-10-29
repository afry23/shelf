@PRD-7.3 @F-PAP-8 @M9
Feature: Adding a paper by its DOI or arXiv ID over HTTP
  As the library view of the web interface
  I want to create a paper from a DOI or an arXiv ID
  So that I can add a paper from the browser, and only when I ask for it

  Background:
    Given an empty working directory
    And I have run shelf with arguments "init library"
    And the online source "arxiv" holds the paper:
      | field   | value                                     |
      | arxiv   | 1706.03762                                |
      | title   | Attention Is All You Need                 |
      | authors | Ashish Vaswani, Noam Shazeer, Niki Parmar |
      | year    | 2017                                      |
    And the online source "arxiv" has a PDF for the arXiv ID "1706.03762"
    And the web server is running on the library "library"

  Scenario: A paper is created from an arXiv ID
    When I send "POST /api/v1/works/fetch" with the JSON:
      """
      { "paper": "arXiv:1706.03762v5" }
      """
    Then the response status is 200
    And the JSON field "title" of the response is "Attention Is All You Need"
    And the JSON field "source" of the response is "arxiv"
    And the JSON field "folder" of the response contains "Attention Is All You Need (2017)"

  Scenario: A paper that is in the library already is a conflict
    Given I send "POST /api/v1/works/fetch" with the JSON:
      """
      { "paper": "1706.03762" }
      """
    When I send "POST /api/v1/works/fetch" with the JSON:
      """
      { "paper": "https://arxiv.org/abs/1706.03762" }
      """
    Then the response status is 409
    And the JSON field "detail" of the response contains "already in the library"

  Scenario Outline: Unusable requests are refused before a source is asked
    When I send "POST /api/v1/works/fetch" with the JSON:
      """
      <body>
      """
    Then the response status is 400
    And the JSON field "detail" of the response contains "<message>"
    And no online source was queried

    Examples:
      | body                      | message                              |
      | { "paper": "not-an-id" }  | neither a DOI nor an arXiv ID        |
      | {}                        | paper is required                    |
      | { "paper": 5 }            | paper                                |

  Scenario: A paper that no source knows is a conflict
    Given the online source "arxiv" holds no books
    When I send "POST /api/v1/works/fetch" with the JSON:
      """
      { "paper": "1706.03762" }
      """
    Then the response status is 409
    And the JSON field "detail" of the response contains "no online source knows"
