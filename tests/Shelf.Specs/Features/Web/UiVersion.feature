@ui @F-WEB-4 @M4
Feature: The interface shows the shelf version
  As a person who runs shelf
  I want to see which version serves the interface
  So that I know what I am using when I report a problem

  Background:
    Given an empty working directory
    And I have run shelf with arguments "init library"
    And the web server is running on the library "library"

  Scenario Outline: The header shows the version on every page
    When I open "<address>" in the browser
    Then the header shows the shelf version

    Examples:
      | address     |
      | /library    |
      | /tags       |
      | /duplicates |
