@F-GRP-8 @M7
Feature: Moving a file to another work over HTTP
  As the work details of the web interface
  I want to move a file that was put into the wrong work
  So that I do not need the command line to correct the grouping

  Background:
    Given an empty working directory
    And I have run shelf with arguments "init library"
    And a sample EPUB "library/Ibryam, Bilgin/Kubernetes Patterns (2019)/kp.epub" with the title "Kubernetes Patterns", the authors "Bilgin Ibryam" and the text seed 1
    And a sample PDF "library/Ibryam, Bilgin/Kubernetes Patterns (2019)/cnp.pdf" with the title "Kubernetes Patterns", the author "Bilgin Ibryam" and the text seed 3
    And the sidecar "library/Ibryam, Bilgin/Kubernetes Patterns (2019)/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K1000000000000000001", "title": "Kubernetes Patterns", "authors": ["Bilgin Ibryam"], "year": 2019, "files": [{ "name": "kp.epub" }, { "name": "cnp.pdf" }] }
      """
    And a sample EPUB "library/Davis, Cornelia/Cloud Native Patterns (2019)/cnp.epub" with the title "Cloud Native Patterns", the authors "Cornelia Davis" and the text seed 2
    And the sidecar "library/Davis, Cornelia/Cloud Native Patterns (2019)/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K2000000000000000002", "title": "Cloud Native Patterns", "authors": ["Cornelia Davis"], "year": 2019, "files": [{ "name": "cnp.epub" }] }
      """
    And I have run shelf with arguments "reindex --library library"
    And I take a snapshot of the folder "library"
    And the web server is running on the library "library"

  Scenario: The file moves to the other work
    When I send "POST /api/v1/files/01J9Z3K1000000000000000001~cnp.pdf/move" with the JSON:
      """
      { "to": "01J9Z3K2000000000000000002" }
      """
    Then the response status is 200
    And the JSON field "work" of the response is "01J9Z3K1000000000000000001"
    And the JSON field "target" of the response is "01J9Z3K2000000000000000002"
    And the JSON field "file" of the response is "cnp.pdf"
    And the JSON field "workRemoved" of the response is false
    And the sidecar "library/Davis, Cornelia/Cloud Native Patterns (2019)/book.json" lists the files "cnp.epub, cnp.pdf"
    And the sidecar "library/Ibryam, Bilgin/Kubernetes Patterns (2019)/book.json" lists the files "kp.epub"

  Scenario: The file becomes a work of its own, described by the given values
    When I send "POST /api/v1/files/01J9Z3K1000000000000000001~cnp.pdf/move" with the JSON:
      """
      { "new": true, "title": "Cloud Notes", "authors": ["Jane Doe"] }
      """
    Then the response status is 200
    And the JSON field "target" of the response is not empty
    And the file "library/Doe, Jane/Cloud Notes/cnp.pdf" exists
    And the sidecar "library/Doe, Jane/Cloud Notes/book.json" has the title "Cloud Notes" and the authors "Jane Doe"

  Scenario: A move can be undone
    Given I send "POST /api/v1/files/01J9Z3K1000000000000000001~cnp.pdf/move" with the JSON:
      """
      { "to": "01J9Z3K2000000000000000002" }
      """
    When I send "POST /api/v1/undo" with the JSON:
      """
      {}
      """
    Then the response status is 200
    And the sidecar "library/Ibryam, Bilgin/Kubernetes Patterns (2019)/book.json" lists the files "kp.epub, cnp.pdf"

  Scenario Outline: A move that cannot be done is refused and nothing is changed
    When I send "POST /api/v1/files/<file>/move" with the JSON:
      """
      <body>
      """
    Then the response status is <status>
    And the folder "library" equals the snapshot

    Examples:
      | file                                      | body                                          | status |
      | 01J9Z3K1000000000000000001~cnp.pdf        | { "to": "01J9Z3K1000000000000000001" }        | 409    |
      | 01J9Z3K1000000000000000001~cnp.pdf        | { "to": "01J9Z3K9000000000000000009" }        | 404    |
      | 01J9Z3K1000000000000000001~missing.pdf    | { "to": "01J9Z3K2000000000000000002" }        | 404    |
      | 01J9Z3K1000000000000000001~cnp.pdf        | {}                                            | 400    |
      | 01J9Z3K1000000000000000001~cnp.pdf        | { "to": "01J9Z3K2000000000000000002", "new": true } | 400 |
      | 01J9Z3K1000000000000000001~cnp.pdf        | { "to": 5 }                                   | 400    |
