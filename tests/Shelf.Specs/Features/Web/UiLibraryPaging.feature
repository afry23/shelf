@ui @F-WEB-6 @PRD-8.2 @M8
Feature: The library is paged
  As a person with a large library
  I want to see how many pages there are, jump between them and choose how many books a page holds
  So that I reach any book without paging through the shelf one page at a time

  Background:
    Given an empty working directory
    And I have run shelf with arguments "init library"
    And the folder "library/Filler" holds 200 works titled "Filler Book" followed by a number
    And I have run shelf with arguments "reindex --metadata-only --library library"
    And the web server is running on the library "library"

  Scenario: The shelf starts with 20 books and shows the number of pages
    When I open "/library" in the browser
    Then the page shows the text "1–20 of 200"
    And the page shows the text "Page 1 of 10"
    And the list "Books" has 20 items
    And the "Works per page" field has the value "20"

  Scenario Outline: The number of books per page can be chosen
    Given I open "/library" in the browser
    When I choose "<size>" in the "Works per page" list
    Then the list "Books" has <items> items
    And the page shows the text "1–<size> of 200"
    And the page shows the text "Page 1 of <pages>"
    And the browser address contains "limit=<size>"

    Examples:
      | size | items | pages |
      | 20   | 20    | 10    |
      | 40   | 40    | 5     |
      | 60   | 60    | 4     |
      | 80   | 80    | 3     |
      | 100  | 100   | 2     |

  Scenario Outline: A page can be opened directly
    Given I open "/library" in the browser
    When I click the "Page <page>" button
    Then the page shows the text "<range>"
    And the page shows the text "Page <page> of 10"
    And the "Page <page>" button is current
    And the browser address contains "offset=<offset>"

    Examples:
      | page | range          | offset |
      | 2    | 21–40 of 200   | 20     |
      | 10   | 181–200 of 200 | 180    |

  Scenario: Previous and next move one page
    Given I open "/library?offset=40" in the browser
    When I click the "Next page" button
    Then the page shows the text "Page 4 of 10"
    When I click the "Previous page" button
    And I click the "Previous page" button
    Then the page shows the text "Page 2 of 10"

  Scenario: The first page cannot go back and the last page cannot go on
    When I open "/library" in the browser
    Then the "Previous page" button is disabled
    When I open "/library?offset=180" in the browser
    Then the "Next page" button is disabled

  Scenario: Far pages are folded into gaps around the current page
    When I open "/library?offset=80" in the browser
    Then the "Page 1" button is shown
    And the "Page 4" button is shown
    And the "Page 5" button is shown
    And the "Page 6" button is shown
    And the "Page 10" button is shown
    And the "Page 3" button is not shown
    And the "Page 7" button is not shown

  Scenario: Changing the page size starts again on the first page
    Given I open "/library?offset=40" in the browser
    When I choose "60" in the "Works per page" list
    Then the page shows the text "1–60 of 200"
    And the browser address does not contain "offset"

  Scenario: The page size in the address is used after a reload
    Given I open "/library?limit=40" in the browser
    When I reload the page
    Then the page shows the text "Page 1 of 5"
    And the "Works per page" field has the value "40"

  Scenario Outline: A page size that makes no sense is replaced
    When I open "/library?limit=<limit>" in the browser
    Then the page shows the text "<range>"

    Examples:
      | limit | range        |
      | abc   | 1–20 of 200  |
      | 0     | 1–20 of 200  |
      | -5    | 1–20 of 200  |
      | 500   | 1–100 of 200 |

  Scenario Outline: The shelf is full on every page, whatever the width of the window
    Given the browser window is <width> x 900 pixels
    When I open "/library" in the browser
    Then every row of the shelf "Books" is full
    And the list "Books" has <items> items
    And the page shows the text "Page 1 of <pages>"

    Examples:
      | width | items | pages |
      | 1280  | 20    | 10    |
      | 1100  | 20    | 10    |
      | 1500  | 18    | 12    |
      | 1900  | 18    | 12    |

  Scenario: The chosen page size is a whole number of rows
    Given the browser window is 1500 x 900 pixels
    And I open "/library" in the browser
    When I choose "96" in the "Works per page" list
    Then every row of the shelf "Books" is full
    And the list "Books" has 96 items
    And the browser address contains "limit=100"

  Scenario: The list view needs no rows and keeps the plain sizes
    When I open "/library?view=list" in the browser
    Then the list "Books" has 20 items
