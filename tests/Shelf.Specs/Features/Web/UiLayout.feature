@ui @F-WEB-4 @F-WEB-2 @M4
Feature: The interface fits the screen and the system
  As a person who reads on a desktop, a tablet, in the day and at night
  I want the interface to follow my device
  So that I do not have to adjust it

  Background:
    Given an empty working directory
    And I have run shelf with arguments "init library"
    And a sample EPUB "library/Ibryam/Kubernetes Patterns/kp.epub" with the title "Kubernetes Patterns", the authors "Bilgin Ibryam" and the chapters:
      | title                 | text                                                                                                    |
      | Foundational Patterns | The sidecar container extends the main container. Health probes tell the platform when a pod is alive. |
      | Behavioral Patterns   | A batch job runs to completion. A daemon service runs on every node of the cluster.                     |
    And the sidecar "library/Ibryam/Kubernetes Patterns/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000001", "title": "Kubernetes Patterns", "subtitle": "Reusable Elements", "authors": ["Bilgin Ibryam"], "language": "en", "year": 2019, "description": "Design patterns for containers", "tags": ["cloud/kubernetes", "devops"], "readStatus": "read", "files": [{ "name": "kp.epub" }] }
      """
    And a sample PDF "library/Alander/Event Sourcing in Practice/es.pdf" with the title "Event Sourcing in Practice", the author "Jördis Ålander" and the pages:
      | text                                                                                              |
      | Event sourcing stores every change as an event. Aggregates replay events to rebuild their state.  |
      | A projection builds a read model from the event stream. Kubernetes is only mentioned once here.  |
    And the sidecar "library/Alander/Event Sourcing in Practice/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000002", "title": "Event Sourcing in Practice", "authors": ["Jördis Ålander"], "language": "en", "year": 2021, "tags": ["architektur/ddd", "cloud/azure"], "files": [{ "name": "es.pdf" }] }
      """
    And a sample EPUB "library/Weiss/Verteilte Systeme/vs.epub" with the title "Verteilte Systeme verstehen", the authors "Lukas Weiß" and the chapters:
      | title   | text                                                                        |
      | Konsens | Ein Konsensprotokoll sorgt dafür, dass alle Knoten denselben Zustand sehen. |
    And the sidecar "library/Weiss/Verteilte Systeme/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000003", "title": "Verteilte Systeme verstehen", "authors": ["Lukas Weiß"], "language": "de", "year": 2015, "tags": ["architektur/verteilte-systeme", "architektur/ddd"], "files": [{ "name": "vs.epub" }] }
      """
    And I have run shelf with arguments "reindex --library library"
    And the web server is running on the library "library"

  Scenario Outline: The interface follows the color scheme of the system
    Given the browser prefers a <scheme> color scheme
    When I open "/library" in the browser
    Then the page background is <tone>

    Examples:
      | scheme | tone  |
      | light  | light |
      | dark   | dark  |

  Scenario: On a desktop the filters are beside the shelf
    Given the browser window is 1280 x 900 pixels
    When I open "/library" in the browser
    Then the "Filters" panel is visible
    And the "Show filters" button is not shown

  Scenario: On a tablet the filters are behind a button
    Given the browser window is 820 x 1180 pixels
    When I open "/library" in the browser
    Then the "Filters" panel is hidden
    When I click the "Show filters" button
    Then the "Filters" panel is visible
    When I click the "Format: pdf" button
    Then the list "Books" has 1 items

  Scenario Outline: Nothing scrolls sideways
    Given the browser window is <width> x <height> pixels
    When I open "<address>" in the browser
    Then the page has no sideways scroll

    Examples:
      | width | height | address                           |
      | 1280  | 900    | /library                          |
      | 820   | 1180   | /library                          |
      | 820   | 1180   | /works/01J9Z3K0000000000000000001 |
      | 820   | 1180   | /search?q=sidecar                 |
      | 820   | 1180   | /duplicates                       |
      | 390   | 844    | /library                          |

  Scenario Outline: The page of a book groups its parts to fit the window
    Given the browser window is <width> x <height> pixels
    When I open "/works/01J9Z3K0000000000000000001" in the browser
    Then "Design patterns for containers" <description> "Year"
    And "Edit details" <tools> "Load details from the internet"
    When I open the "Files" tab
    Then "Files" stands above "Add a file"

    Examples:
      | width | height | description   | tools         |
      | 1280  | 900    | stands beside | stands beside |
      | 820   | 1180   | stands beside | stands beside |
      | 390   | 844    | stands above  | stands above  |

  Scenario: A file of a book stays usable on a phone
    Given the browser window is 390 x 844 pixels
    When I open "/works/01J9Z3K0000000000000000001" in the browser
    Then the page has no sideways scroll
    When I open the "Files" tab
    And I click the "Delete kp.epub" button
    Then the page shows the text "Move kp.epub to the trash?"

  Scenario Outline: The filters use the width they are given
    Given the browser window is <width> x <height> pixels
    When I open "/library" in the browser
    And I click the "Show filters" button
    Then "Authors" <placement> "Language"

    Examples:
      | width | height | placement     |
      | 820   | 1180   | stands beside |
      | 390   | 844    | stands above  |

  Scenario: The page numbers stay within a phone
    Given the browser window is 390 x 844 pixels
    When I open "/library" in the browser
    Then the "Previous page" button is shown
    And the "Next page" button is shown
    And the page has no sideways scroll

  Scenario: The shelf shows a different number of covers per row as the window changes
    Given the browser window is 1280 x 900 pixels
    When I open "/library" in the browser
    Then the books of the list "Books" stand in 1 row
    Given the browser window is 390 x 844 pixels
    When I open "/library" in the browser
    Then the books of the list "Books" stand in 2 rows

  Scenario: The access token opens the interface once and is kept by the browser
    Given the web server is running on the library "library" with the arguments "--token s3cret"
    When I open "/library?token=s3cret" in the browser
    Then the page shows the text "3 books"
    And the browser address does not contain "token"
    When I open "/library" in the browser
    Then the page shows the text "3 books"

  Scenario: Without the token the interface says what is needed
    Given the web server is running on the library "library" with the arguments "--token s3cret"
    When I open "/library" in the browser
    Then the page shows the text "needs an access token"
