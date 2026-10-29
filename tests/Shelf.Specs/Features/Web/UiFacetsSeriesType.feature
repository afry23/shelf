@ui @F-WEB-7 @PRD-8.2 @M12
Feature: Series and type in the filter panel
  As a person who browses the library
  I want to narrow it to a series or to papers with a click
  So that I see the books of a series together

  Background:
    Given an empty working directory
    And I have run shelf with arguments "init library"
    And a sample EPUB "library/Ibryam/Kubernetes Patterns/kp.epub" with the title "Kubernetes Patterns", the authors "Bilgin Ibryam" and the chapters:
      | title | text                                  |
      | One   | The sidecar container extends it.     |
    And the sidecar "library/Ibryam/Kubernetes Patterns/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000001", "title": "Kubernetes Patterns", "authors": ["Bilgin Ibryam"], "language": "en", "year": 2019, "series": "Reusable Elements", "seriesIndex": 1, "files": [{ "name": "kp.epub" }] }
      """
    And a sample PDF "library/Mueller/Cloud Patterns/cp.pdf" with the title "Cloud Patterns", the author "Anna Müller" and the pages:
      | text                                     |
      | Serverless functions scale down to zero. |
    And the sidecar "library/Mueller/Cloud Patterns/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000002", "title": "Cloud Patterns", "authors": ["Anna Müller"], "language": "en", "year": 2020, "series": "reusable elements", "seriesIndex": 2, "files": [{ "name": "cp.pdf" }] }
      """
    And a sample PDF "library/Vaswani/Attention/attention.pdf" with the title "Attention Is All You Need", the author "Ashish Vaswani" and the pages:
      | text                                 |
      | The Transformer relies on attention. |
    And the sidecar "library/Vaswani/Attention/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000003", "kind": "paper", "title": "Attention Is All You Need", "authors": ["Ashish Vaswani"], "language": "en", "year": 2017, "series": "Neural Papers", "files": [{ "name": "attention.pdf" }] }
      """
    And a sample PDF "library/Body/Loose/loose.pdf" with the title "Loose Notes", the author "N. Body" and the pages:
      | text        |
      | Some notes. |
    And the sidecar "library/Body/Loose/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000004", "title": "Loose Notes", "authors": ["N. Body"], "language": "en", "files": [{ "name": "loose.pdf" }] }
      """
    And I have run shelf with arguments "reindex --library library"
    And the web server is running on the library "library"

  Scenario Outline: Series and type filter the shelf
    Given I open "/library" in the browser
    When I click the "<facet>" button
    Then the list "Books" has <count> items
    And the "Remove filter <facet>" button is shown

    Examples:
      | facet                       | count |
      | Series: Reusable Elements   | 2     |
      | Series: Neural Papers       | 1     |
      | Type: paper                 | 1     |
      | Type: book                  | 3     |

  Scenario: A series can be combined with a type, and the filter is kept in the address
    Given I open "/library" in the browser
    When I click the "Series: Reusable Elements" button
    And I click the "Type: book" button
    Then the list "Books" has 2 items
    And the browser address contains "series"
    When I reload the page
    Then the list "Books" has 2 items
    And the "Series: Reusable Elements" button is pressed
