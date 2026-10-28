@F-DUP-7 @F-DUP-4 @M5
Feature: Deleting files and works over HTTP
  As the work view of the web interface
  I want to delete a file or a whole work
  So that I do not need the command line to remove a book I no longer want

  Background:
    Given an empty working directory
    And I have run shelf with arguments "init library"
    And a sample EPUB "library/Ibryam, Bilgin/Kubernetes Patterns (2019)/kp.epub" with the title "Kubernetes Patterns", the authors "Bilgin Ibryam" and the text seed 1
    And a sample PDF "library/Ibryam, Bilgin/Kubernetes Patterns (2019)/kp.pdf" with the title "Kubernetes Patterns", the author "Bilgin Ibryam" and the text seed 2
    And the sidecar "library/Ibryam, Bilgin/Kubernetes Patterns (2019)/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K1000000000000000001", "title": "Kubernetes Patterns", "authors": ["Bilgin Ibryam"], "year": 2019, "files": [{ "name": "kp.epub" }, { "name": "kp.pdf" }] }
      """
    And a sample EPUB "library/Davis, Cornelia/Cloud Native Patterns (2019)/cnp.epub" with the title "Cloud Native Patterns", the authors "Cornelia Davis" and the text seed 3
    And the sidecar "library/Davis, Cornelia/Cloud Native Patterns (2019)/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K2000000000000000002", "title": "Cloud Native Patterns", "authors": ["Cornelia Davis"], "year": 2019, "files": [{ "name": "cnp.epub" }] }
      """
    And I have run shelf with arguments "reindex --library library"
    And I take a snapshot of the folder "library"
    And the web server is running on the library "library"

  Scenario: A file goes to the trash and the work stays
    When I request "DELETE /api/v1/files/01J9Z3K1000000000000000001~kp.pdf"
    Then the response status is 200
    And the JSON field "trashed" of the response is 1
    And the JSON field "workRemoved" of the response is false
    And the file "library/Ibryam, Bilgin/Kubernetes Patterns (2019)/kp.pdf" does not exist
    And the directory "library/.shelf/trash" exists
    And the sidecar "library/Ibryam, Bilgin/Kubernetes Patterns (2019)/book.json" lists the files "kp.epub"
    When I request "GET /api/v1/works/01J9Z3K1000000000000000001"
    Then the response status is 200

  Scenario: The last file of a work takes the work along
    Given I request "DELETE /api/v1/files/01J9Z3K1000000000000000001~kp.pdf"
    When I request "DELETE /api/v1/files/01J9Z3K1000000000000000001~kp.epub"
    Then the response status is 200
    And the JSON field "workRemoved" of the response is true
    When I request "GET /api/v1/works/01J9Z3K1000000000000000001"
    Then the response status is 404

  Scenario: A whole work goes to the trash
    When I request "DELETE /api/v1/works/01J9Z3K1000000000000000001"
    Then the response status is 200
    And the JSON field "trashed" of the response is 2
    And the JSON field "workRemoved" of the response is true
    And the directory "library/Ibryam, Bilgin/Kubernetes Patterns (2019)" does not exist
    When I request "GET /api/v1/works"
    Then the JSON field "total" of the response is 1

  Scenario: The deletion can be undone on the command line
    Given I request "DELETE /api/v1/works/01J9Z3K1000000000000000001"
    When I run shelf with arguments "undo --library library"
    Then the folder "library" equals the snapshot

  # ---- refusals ----

  Scenario Outline: A file or work that does not exist is a 404
    When I request "DELETE /api/v1/<path>"
    Then the response status is 404
    And the folder "library" equals the snapshot

    Examples:
      | path                                              |
      | files/01J9Z3K1000000000000000001~missing.pdf      |
      | files/01J9Z3K9000000000000000009~kp.pdf           |
      | works/01J9Z3K9000000000000000009                  |

  Scenario Outline: A malformed ID is a 400
    When I request "DELETE /api/v1/<path>"
    Then the response status is 400
    And the folder "library" equals the snapshot

    Examples:
      | path             |
      | files/kp.pdf     |
      | works/not-an-id  |
