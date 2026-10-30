@F-NOTEF-5 @F-NOTEF-6 @M15
Feature: A notes file belongs to its work
  As a person who organizes a library
  I want the notes files to go wherever the work goes
  So that the notes never get lost

  Background:
    Given an empty working directory
    And I have run shelf with arguments "init library"
    And a sample PDF "library/Alander/Event Sourcing in Practice/es.pdf" with the title "Event Sourcing in Practice", the author "Jördis Ålander" and the pages:
      | text                                            |
      | Event sourcing stores every change as an event. |
    And the sidecar "library/Alander/Event Sourcing in Practice/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000002", "title": "Event Sourcing in Practice", "authors": ["Jördis Ålander"], "language": "en", "year": 2021, "files": [{ "name": "es.pdf" }, { "name": "kindle.txt", "role": "notes" }] }
      """
    And the file "library/Alander/Event Sourcing in Practice/kindle.txt" has the content "Highlight: the saga coordinates a compensating action."
    And I have run shelf with arguments "reindex --library library"

  Scenario: The notes files of a work are listed
    When I run shelf with arguments "notes files 01J9Z3K --library library"
    Then the exit code is 0
    And standard output contains "kindle.txt"
    And standard output does not contain "es.pdf"

  Scenario: The list is available as JSON
    When I run shelf with arguments "notes files 01J9Z3K --json --library library"
    Then standard output contains "kindle.txt"

  Scenario: A notes file is not a format of the work
    When I run shelf with arguments "show 01J9Z3K --json --library library"
    Then standard output contains "pdf"

  Scenario: Organize moves the notes file with the work
    When I run shelf with arguments "organize --apply --library library"
    Then the exit code is 0
    When I run shelf with arguments "reindex --library library"
    And I run shelf with arguments "notes files 01J9Z3K --library library"
    Then standard output contains ".txt"
    And standard output does not contain ".pdf"

  Scenario: Moving the notes file to another work keeps its role
    Given a sample PDF "library/Ibryam/Kubernetes Patterns/kp.pdf" with the title "Kubernetes Patterns", the author "Bilgin Ibryam" and the pages:
      | text                                              |
      | The sidecar container extends the main container. |
    And the sidecar "library/Ibryam/Kubernetes Patterns/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000001", "title": "Kubernetes Patterns", "authors": ["Bilgin Ibryam"], "language": "en", "year": 2019, "files": [{ "name": "kp.pdf" }] }
      """
    And I have run shelf with arguments "reindex --library library"
    When I run shelf with arguments "move 01J9Z3K0000000000000000002 kindle.txt --to 01J9Z3K0000000000000000001 --yes --library library"
    Then the exit code is 0
    And the file "kindle.txt" in the index has the role "notes"

  Scenario: Removing the notes file and undoing it
    When I run shelf with arguments "delete 01J9Z3K kindle.txt --yes --library library"
    Then the file "library/Alander/Event Sourcing in Practice/kindle.txt" does not exist
    When I run shelf with arguments "undo --library library"
    Then the file "library/Alander/Event Sourcing in Practice/kindle.txt" exists
    And the file "kindle.txt" in the index has the role "notes"
