@F-NOTEF-3 @F-NOTEF-4 @M15
Feature: Search finds the text of notes files
  As a person with the notes of an e-reader attached to a work
  I want the full text search to find words from these notes
  So that I find the book by what I noted

  Background:
    Given an empty working directory
    And I have run shelf with arguments "init library"
    And a sample PDF "library/Alander/Event Sourcing in Practice/es.pdf" with the title "Event Sourcing in Practice", the author "Jördis Ålander" and the pages:
      | text                                            |
      | Event sourcing stores every change as an event. |
    And the sidecar "library/Alander/Event Sourcing in Practice/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000002", "title": "Event Sourcing in Practice", "authors": ["Jördis Ålander"], "language": "en", "year": 2021, "files": [{ "name": "es.pdf" }, { "name": "kindle.txt", "role": "notes" }, { "name": "kindle.pdf", "role": "notes" }, { "name": "scanned-page.pdf", "role": "notes" }] }
      """
    And the file "library/Alander/Event Sourcing in Practice/kindle.txt" has the content "Highlight: the saga coordinates a compensating action."
    And a sample PDF "library/Alander/Event Sourcing in Practice/kindle.pdf" with the title "Kindle notes", the author "Jördis Ålander" and the pages:
      | text                                         |
      | Nothing here.                                |
      | Note: ask about the tombstone event.         |
    And the folder "library/Alander/Event Sourcing in Practice" contains the fixtures "derived/scanned-page.pdf"
    And I have run shelf with arguments "reindex --library library"

  Scenario Outline: A word that is only in a notes file finds the work
    When I run shelf with arguments "search <word> --library library"
    Then the exit code is 0
    And the search results are, in any order: "Event Sourcing in Practice"
    And standard output contains "<label>"

    Examples:
      | word      | label                    |
      | saga      | notes file, line 1       |
      | tombstone | notes file, page 2       |

  Scenario: A PDF without a text layer is kept but not searched
    Then the file "scanned-page.pdf" in the index has the text status "none"
    And the file "scanned-page.pdf" in the index has the role "notes"

  Scenario: A filter applies to a hit of a notes file
    When I run shelf with arguments "search saga author:ibryam --library library"
    Then the search results are, in any order: ""

  Scenario: The notes file hit is in the JSON output
    When I run shelf with arguments "search saga --json --library library"
    Then standard output contains "notes file, line 1"

  Scenario: A notes file is no book for the duplicate check
    Given the file "library/Alander/Event Sourcing in Practice/kindle.txt" has the content "Highlight: the saga coordinates a compensating action."
    When I run shelf with arguments "dupes --library library"
    Then the exit code is 0
    And standard output does not contain "kindle"
