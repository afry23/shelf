@F-CNV-1 @F-CNV-2 @F-CNV-3 @M24
Feature: Converting the PDF of a work into a reflowable EPUB
  As a person who reads on a Kindle
  I want shelf to turn the PDF of a work into a reflowable EPUB with my own converter
  So that the text fits the small screen and stays with the work

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

  Scenario: The EPUB is added as a file of the same work
    Given the converter is configured as "fakeconv {input} {output}"
    And the converter writes an EPUB with the chapter text "Event sourcing stores every change as an event."
    When I run shelf with arguments "convert 01J9Z3K --library library"
    Then the exit code is 0
    And standard output contains "Converted es.pdf to es.epub"
    And the file "library/Alander/Event Sourcing in Practice/es.epub" exists
    And the file "library/Alander/Event Sourcing in Practice/es.pdf" exists
    And the table "works" of the index has 1 rows
    And the table "files" of the index has 2 rows
    And no conversion folder is left behind

  Scenario: The placeholders are replaced by the paths of the input and the output
    Given the converter is configured as "fakeconv --in {input} --out {output}"
    And the converter writes an EPUB with the chapter text "Event sourcing stores every change as an event."
    When I run shelf with arguments "convert 01J9Z3K --library library"
    Then the converter was called with an input that ends in "es.pdf"
    And the converter was called with an output that ends in ".epub"

  Scenario: The text of the new EPUB is found by the full text search
    Given the converter is configured as "fakeconv {input} {output}"
    And the converter writes an EPUB with the chapter text "Event sourcing stores every change as an event."
    And I have run shelf with arguments "convert 01J9Z3K --library library"
    When I run shelf with arguments "search format:epub stores --library library"
    Then standard output contains "Event Sourcing in Practice"

  Scenario: --json reports the new file
    Given the converter is configured as "fakeconv {input} {output}"
    And the converter writes an EPUB with the chapter text "Event sourcing stores every change as an event."
    When I run shelf with arguments "convert 01J9Z3K --json --library library"
    Then the exit code is 0
    And standard output contains '"name":"es.epub"'

  Scenario: A work that has an EPUB is refused without --replace
    Given the converter is configured as "fakeconv {input} {output}"
    And the converter writes an EPUB with the chapter text "Event sourcing stores every change as an event."
    And I have run shelf with arguments "convert 01J9Z3K --library library"
    When I run shelf with arguments "convert 01J9Z3K --library library"
    Then the exit code is 2
    And standard error contains "already has an EPUB"
    And standard error contains "--replace"

  Scenario: --replace converts again and replaces the EPUB
    Given the converter is configured as "fakeconv {input} {output}"
    And the converter writes an EPUB with the chapter text "First run."
    And I have run shelf with arguments "convert 01J9Z3K --library library"
    And the converter writes an EPUB with the chapter text "Second run."
    When I run shelf with arguments "convert 01J9Z3K --replace --library library"
    Then the exit code is 0
    And the table "files" of the index has 2 rows

  Scenario: --replace with an identical result changes nothing
    Given the converter is configured as "fakeconv {input} {output}"
    And the converter writes an EPUB with the chapter text "Same text."
    And I have run shelf with arguments "convert 01J9Z3K --library library"
    When I run shelf with arguments "convert 01J9Z3K --replace --library library"
    Then the exit code is 2
    And standard error contains "identical to es.epub"
    And the table "files" of the index has 2 rows
    And no conversion folder is left behind

  Scenario: Several PDFs need --file
    Given a sample PDF "library/Alander/Event Sourcing in Practice/es-second.pdf" with the title "Event Sourcing in Practice", the author "Jördis Ålander" and the pages:
      | text                      |
      | A second PDF of the work. |
    And the sidecar "library/Alander/Event Sourcing in Practice/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000002", "title": "Event Sourcing in Practice", "authors": ["Jördis Ålander"], "language": "en", "year": 2021, "files": [{ "name": "es.pdf" }, { "name": "es-second.pdf" }] }
      """
    And I have run shelf with arguments "reindex --library library"
    And the converter is configured as "fakeconv {input} {output}"
    When I run shelf with arguments "convert 01J9Z3K --library library"
    Then the exit code is 2
    And standard error contains "--file"
    And the converter was not called

  Scenario: A PDF without a text layer is refused before the tool is called
    Given the fixture "derived/scanned-page.pdf" is the only file of a work with the ID "01J9Z3K0000000000000000003"
    And the converter is configured as "fakeconv {input} {output}"
    When I run shelf with arguments "convert 01J9Z3K0000000000000000003 --library library"
    Then the exit code is 2
    And standard error contains "no text layer"
    And standard error contains "no OCR"
    And the converter was not called

  Scenario: No converter is configured
    When I run shelf with arguments "convert 01J9Z3K --library library"
    Then the exit code is 2
    And standard error contains "convert.command"
    And the table "files" of the index has 1 rows

  Scenario: The converter is not installed
    Given the converter is configured as "fakeconv {input} {output}"
    And the converter is not installed
    When I run shelf with arguments "convert 01J9Z3K --library library"
    Then the exit code is 2
    And standard error contains "fakeconv was not found"
    And the table "files" of the index has 1 rows

  Scenario: The converter fails
    Given the converter is configured as "fakeconv {input} {output}"
    And the converter fails with "pdftohtml: cannot open input"
    When I run shelf with arguments "convert 01J9Z3K --library library"
    Then the exit code is 1
    And standard error contains "pdftohtml: cannot open input"
    And the table "files" of the index has 1 rows
    And no conversion folder is left behind

  Scenario Outline: A result that is no usable EPUB is not added
    Given the converter is configured as "fakeconv {input} {output}"
    And the converter writes <result>
    When I run shelf with arguments "convert 01J9Z3K --library library"
    Then the exit code is 1
    And standard error contains "<message>"
    And the table "files" of the index has 1 rows
    And no conversion folder is left behind

    Examples:
      | result                           | message              |
      | no output file                   | wrote no output file |
      | a file that is not a zip         | is not a valid EPUB  |
      | an EPUB without any chapter text | has no chapter text  |

  Scenario: The converter runs longer than the time limit
    Given the converter is configured as "fakeconv {input} {output}"
    And the configuration key "convert.timeout" is "1"
    And the converter runs for 5 seconds
    When I run shelf with arguments "convert 01J9Z3K --library library"
    Then the exit code is 1
    And standard error contains "timed out after 1 s"
    And the table "files" of the index has 1 rows
    And no conversion folder is left behind
