@ui @F-WEB-6 @PRD-8.2 @M8
Feature: The default page size is set in the config
  As a person with a large library
  I want to set how many books a page holds by default
  So that I do not have to choose the size again in every new window

  Background:
    Given an empty working directory
    And I have run shelf with arguments "init library"
    And the folder "library/Filler" holds 200 works titled "Filler Book" followed by a number
    And I have run shelf with arguments "reindex --metadata-only --library library"

  Scenario: The default number of books per page comes from the global config
    Given a global config file with content:
      """
      [web]
      page_size = 40
      """
    And the web server is running on the library "library"
    And the browser window is 1280 x 900 pixels
    When I open "/library" in the browser
    Then the page shows the text "1–40 of 200"
    And the page shows the text "Page 1 of 5"
    And the "Works per page" field has the value "40"

  Scenario: A page size from the config that is not in the list is offered too
    Given a global config file with content:
      """
      [web]
      page_size = 50
      """
    And the web server is running on the library "library"
    And the browser window is 1280 x 900 pixels
    When I open "/library" in the browser
    Then the page shows the text "1–50 of 200"
    And the "Works per page" field has the value "50"

  Scenario: The page size in the address wins over the config
    Given a global config file with content:
      """
      [web]
      page_size = 40
      """
    And the web server is running on the library "library"
    And the browser window is 1280 x 900 pixels
    When I open "/library?limit=60" in the browser
    Then the page shows the text "1–60 of 200"

  Scenario: The status tells the page size of the config
    Given a global config file with content:
      """
      [web]
      page_size = 40
      """
    And the web server is running on the library "library"
    When I request "GET /api/v1/status"
    Then the JSON field "pageSize" of the response is 40
