@ui @F-CNV-4 @M24
Feature: Converting a PDF into an EPUB in the browser
  As a person who reads on a Kindle
  I want a button on the work page that makes an EPUB from the PDF
  So that I can send the book to my Kindle without a converter on the command line

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

  Scenario: The button turns the PDF into an EPUB
    Given the converter is configured as "fakeconv {input} {output}"
    And the converter writes an EPUB with the chapter text "Event sourcing stores every change as an event."
    And the web server is running on the library "library"
    And I open "/works/01J9Z3K0000000000000000002#files" in the browser
    When I click the "Convert to EPUB" button
    Then the page shows the text "Converted es.pdf to es.epub"
    And the page shows the text "es.epub"
    And the file "library/Alander/Event Sourcing in Practice/es.epub" exists

  Scenario: A work that has an EPUB offers to convert again
    Given the converter is configured as "fakeconv {input} {output}"
    And the converter writes an EPUB with the chapter text "Event sourcing stores every change as an event."
    And I have run shelf with arguments "convert 01J9Z3K --library library"
    And the web server is running on the library "library"
    When I open "/works/01J9Z3K0000000000000000002#files" in the browser
    Then the page shows the text "Convert to EPUB again"

  Scenario: The reason is shown when nothing can be converted
    Given the web server is running on the library "library"
    And I open "/works/01J9Z3K0000000000000000002#files" in the browser
    When I click the "Convert to EPUB" button
    Then an alert says "convert.command"
    And the file "library/Alander/Event Sourcing in Practice/es.epub" does not exist

  Scenario: A work without a PDF has no button
    Given a sample EPUB "library/Ibryam/Kubernetes Patterns/kp.epub" with the title "Kubernetes Patterns", the authors "Bilgin Ibryam" and the text seed 1
    And the sidecar "library/Ibryam/Kubernetes Patterns/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000003", "title": "Kubernetes Patterns", "authors": ["Bilgin Ibryam"], "language": "en", "files": [{ "name": "kp.epub" }] }
      """
    And I have run shelf with arguments "reindex --library library"
    And the web server is running on the library "library"
    When I open "/works/01J9Z3K0000000000000000003#files" in the browser
    Then the page does not show the text "Convert to EPUB"
