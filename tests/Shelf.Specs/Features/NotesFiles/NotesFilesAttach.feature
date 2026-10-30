@F-NOTEF-1 @F-NOTEF-2 @M15
Feature: Attaching a notes file to a work
  As a person who exports the notes of an e-reader
  I want to attach them as a PDF or TXT file to the work of the book
  So that the notes stay with the book

  Background:
    Given an empty working directory
    And I have run shelf with arguments "init library"
    And a sample PDF "library/Alander/Event Sourcing in Practice/es.pdf" with the title "Event Sourcing in Practice", the author "Jördis Ålander" and the pages:
      | text                                            |
      | Event sourcing stores every change as an event. |
    And the sidecar "library/Alander/Event Sourcing in Practice/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000002", "title": "Event Sourcing in Practice", "authors": ["Jördis Ålander"], "language": "en", "year": 2021, "files": [{ "name": "es.pdf" }] }
      """
    And I have run shelf with arguments "reindex --library library"

  Scenario: A TXT file is attached with the role notes
    Given the file "incoming/kindle.txt" has the content "Highlight: stores every change"
    When I run shelf with arguments "add 01J9Z3K --notes incoming/kindle.txt --library library"
    Then the exit code is 0
    And standard output contains "Added kindle.txt to 01J9Z3K0000000000000000002"
    And the file "library/Alander/Event Sourcing in Practice/kindle.txt" exists
    And the file "library/Alander/Event Sourcing in Practice/book.json" contains '"role": "notes"'
    And the file "kindle.txt" in the index has the role "notes"
    And the file "es.pdf" in the index has the role "book"

  Scenario: A PDF file is attached with the role notes
    Given a sample PDF "incoming/kindle.pdf" with the title "Kindle notes", the author "Jördis Ålander" and the pages:
      | text                          |
      | Highlight: stores every change |
    When I run shelf with arguments "add 01J9Z3K --notes incoming/kindle.pdf --library library"
    Then the exit code is 0
    And the file "library/Alander/Event Sourcing in Practice/kindle.pdf" exists
    And the file "kindle.pdf" in the index has the role "notes"
    And the table "works" of the index has 1 rows

  Scenario Outline: Any other file is refused
    Given the file "incoming/<name>" has the content "data"
    When I run shelf with arguments "add 01J9Z3K --notes incoming/<name> --library library"
    Then the exit code is 1
    And standard error contains "notes file"
    And the file "library/Alander/Event Sourcing in Practice/<name>" does not exist

    Examples:
      | name       |
      | notes.epub |
      | notes.docx |

  Scenario: The same content twice is refused
    Given the file "incoming/kindle.txt" has the content "Highlight: stores every change"
    And I have run shelf with arguments "add 01J9Z3K --notes incoming/kindle.txt --library library"
    And the file "incoming/copy.txt" has the content "Highlight: stores every change"
    When I run shelf with arguments "add 01J9Z3K --notes incoming/copy.txt --library library"
    Then the exit code is 1
    And standard error contains "already"

  Scenario: The role comes back after the index is rebuilt
    Given the file "incoming/kindle.txt" has the content "Highlight: stores every change"
    And I have run shelf with arguments "add 01J9Z3K --notes incoming/kindle.txt --library library"
    When I run shelf with arguments "reindex --library library"
    Then the file "kindle.txt" in the index has the role "notes"

  Scenario: A loose TXT file is still no book for import
    Given the file "incoming/loose.txt" has the content "just text"
    When I run shelf with arguments "import incoming --library library"
    Then the table "works" of the index has 1 rows
