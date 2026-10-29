@ui @F-STA-3 @PRD-8.2 @M12
Feature: The statistics view of a library without anything read
  As a person who just started a library
  I want the statistics view to say what is missing
  So that I know what to do

  Background:
    Given an empty working directory
    And I have run shelf with arguments "init library"
    And a sample PDF "library/A/One/one.pdf" with the title "One", the author "A. Author" and the pages:
      | text   |
      | Hello. |
    And I have run shelf with arguments "reindex --library library"
    And the web server is running on the library "library"

  Scenario: The view says that nothing was read
    When I open "/stats" in the browser
    Then the heading "Statistics" is shown
    And the page shows the text "Nothing read yet"
    And the page shows the text "Set a book to Read on its page"
