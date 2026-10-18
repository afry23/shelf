@PRD-7.1 @M1
Feature: Opening a work with the default application
  As a reader
  I want shelf open to hand a book to the application my system uses for it
  So that I can start reading from the command line

  Background:
    Given an empty working directory
    And I have run shelf with arguments "init library"
    And a sample EPUB "library/Ibryam, Bilgin/Kubernetes Patterns (2019)/kp.epub" with the title "Kubernetes Patterns", the authors "Bilgin Ibryam" and the text seed 1
    And a sample PDF "library/Ibryam, Bilgin/Kubernetes Patterns (2019)/kp.pdf" with the title "Kubernetes Patterns", the author "Bilgin Ibryam" and the text seed 1
    And the sidecar "library/Ibryam, Bilgin/Kubernetes Patterns (2019)/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K1000000000000000001", "title": "Kubernetes Patterns", "authors": ["Bilgin Ibryam"], "files": [{ "name": "kp.epub" }, { "name": "kp.pdf" }] }
      """
    And a sample PDF "library/Ålander, Jördis/Event Sourcing in Practice/es.pdf" with the title "Event Sourcing in Practice", the author "Jördis Ålander" and the text seed 2
    And the sidecar "library/Ålander, Jördis/Event Sourcing in Practice/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K2000000000000000002", "title": "Event Sourcing in Practice", "authors": ["Jördis Ålander"], "files": [{ "name": "es.pdf" }] }
      """
    And I have run shelf with arguments "reindex --library library"

  Scenario: Without a format the first file of the work is opened
    When I run shelf with arguments "open 01J9Z3K1 --library library"
    Then the exit code is 0
    And standard output matches the pattern "^Opening .*kp\.epub$"
    And shelf asked the system to open "library/Ibryam, Bilgin/Kubernetes Patterns (2019)/kp.epub"

  Scenario Outline: The format picks the file
    When I run shelf with arguments "open 01J9Z3K1 --format <format> --library library"
    Then the exit code is 0
    And shelf asked the system to open "library/Ibryam, Bilgin/Kubernetes Patterns (2019)/<file>"

    Examples:
      | format | file    |
      | pdf    | kp.pdf  |
      | PDF    | kp.pdf  |
      | epub   | kp.epub |

  Scenario: A work that has no file of the format is an error
    When I run shelf with arguments "open 01J9Z3K2 --format epub --library library"
    Then the exit code is 1
    And standard error starts with "error: 01J9Z3K2000000000000000002 has no epub file (available: pdf)"
    And shelf asked the system to open nothing

  Scenario: A file that is gone from the disk is an error
    Given I delete the file "library/Ålander, Jördis/Event Sourcing in Practice/es.pdf"
    When I run shelf with arguments "open 01J9Z3K2 --library library"
    Then the exit code is 1
    And standard error starts with "error: Ålander, Jördis/Event Sourcing in Practice/es.pdf does not exist"
    And shelf asked the system to open nothing

  Scenario: The JSON form names the file
    When I run shelf with arguments "open 01J9Z3K2 --library library --json"
    Then the exit code is 0
    And the JSON of standard output has the string "id" equal to "01J9Z3K2000000000000000002"
    And the JSON of standard output has the string "file" containing "es.pdf"

  Scenario: An unknown work is an error
    When I run shelf with arguments "open 01J9Z3K9 --library library"
    Then the exit code is 1
    And standard error starts with "error: no work matches '01J9Z3K9'"
    And shelf asked the system to open nothing

  Scenario: The system cannot open the file
    Given the system fails to open files with the message "no application is set for this file type"
    When I run shelf with arguments "open 01J9Z3K2 --library library"
    Then the exit code is 1
    And standard error starts with "error: cannot open Ålander, Jördis/Event Sourcing in Practice/es.pdf: no application is set for this file type"
