@F-SRC-1 @PRD-8.2 @M4
Feature: How many hits a search shows
  As a person who searches a large library
  I want a search without a limit to show more than a handful of hits
  So that I see more of the matches at once

  Background:
    Given an empty working directory
    And I have run shelf with arguments "init library"
    And the folder "library/Filler" holds 80 works titled "Filler Book" followed by a number
    And I have run shelf with arguments "reindex --metadata-only --library library"
    And the web server is running on the library "library"

  Scenario: A search without a limit shows up to 50 hits
    When I request "GET /api/v1/search?q=filler&scope=title"
    Then the JSON array "results" of the response has 50 entries

  Scenario: A search can still ask for fewer hits
    When I request "GET /api/v1/search?q=filler&scope=title&limit=10"
    Then the JSON array "results" of the response has 10 entries
