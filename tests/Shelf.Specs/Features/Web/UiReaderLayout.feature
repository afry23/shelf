@ui @F-RDR-7 @M16
Feature: Choosing the page layout of an EPUB
  As a person who reads an EPUB in the browser
  I want to read in pages or scrolled, on one page or two, with a text column of my width
  So that the book fits my screen and the way I read

  Background:
    Given an empty working directory
    And I have run shelf with arguments "init library"
    And a sample EPUB "library/Ibryam/Kubernetes Patterns/kp.epub" with the title "Kubernetes Patterns", the authors "Bilgin Ibryam" and the chapters:
      | title                 | text                                                                                                    |
      | Foundational Patterns | The sidecar container extends the main container. Health probes tell the platform when a pod is alive. |
      | Behavioral Patterns   | A batch job runs to completion. A daemon service runs on every node of the cluster.                     |
    And the sidecar "library/Ibryam/Kubernetes Patterns/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000001", "title": "Kubernetes Patterns", "authors": ["Bilgin Ibryam"], "language": "en", "year": 2019, "files": [{ "name": "kp.epub" }] }
      """
    And I have run shelf with arguments "reindex --library library"
    And the web server is running on the library "library"

  Scenario: A book opens in pages
    When I open "/read/01J9Z3K0000000000000000001~kp.epub" in the browser
    And I click the "Display" button
    Then the "Paged" button is pressed
    And the book is shown "paginated"

  Scenario Outline: The flow can be chosen
    Given I open "/read/01J9Z3K0000000000000000001~kp.epub" in the browser
    And the reader shows the text "The sidecar container extends the main container"
    And I click the "Display" button
    When I click the "<flow>" button
    Then the "<flow>" button is pressed
    And the book is shown "<mode>"
    And the reader shows the text "The sidecar container extends the main container"

    Examples:
      | flow     | mode      |
      | Scrolled | scrolled  |
      | Paged    | paginated |

  Scenario: The place is kept when the flow changes
    Given I open "/read/01J9Z3K0000000000000000001~kp.epub" in the browser
    And I click the "Contents" button
    And I click the entry "Behavioral Patterns" in the panel "Contents"
    And the reader shows the text "A daemon service runs on every node"
    When I click the "Display" button
    And I click the "Scrolled" button
    Then the reader shows the text "A daemon service runs on every node"

  Scenario Outline: The number of pages side by side can be chosen
    Given I open "/read/01J9Z3K0000000000000000001~kp.epub" in the browser
    And I click the "Display" button
    When I click the "<choice>" button
    Then the "<choice>" button is pressed
    And the book is shown with at most <columns> columns

    Examples:
      | choice    | columns |
      | One page  | 1       |
      | Two pages | 2       |

  Scenario: The pages cannot be chosen while the book is scrolled
    Given I open "/read/01J9Z3K0000000000000000001~kp.epub" in the browser
    And I click the "Display" button
    When I click the "Scrolled" button
    Then the "One page" button is disabled
    And the "Two pages" button is disabled

  Scenario Outline: The width of the text column can be chosen
    Given I open "/read/01J9Z3K0000000000000000001~kp.epub" in the browser
    And I click the "Display" button
    When I click the "<width>" button
    Then the "<width>" button is pressed
    And the text column of the book is <pixels> pixels wide at most

    Examples:
      | width  | pixels |
      | Narrow | 560    |
      | Medium | 720    |
      | Wide   | 960    |

  Scenario: The layout stays when the text size changes
    Given I open "/read/01J9Z3K0000000000000000001~kp.epub" in the browser
    And I click the "Display" button
    And I click the "Scrolled" button
    When I click the "Larger text" button
    Then the book is shown "scrolled"
    And the text in the book is 110 percent

  Scenario: The display panel closes
    Given I open "/read/01J9Z3K0000000000000000001~kp.epub" in the browser
    And I click the "Display" button
    When I click the "Display" button
    Then the panel "Display" is closed
