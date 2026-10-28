@F-GRP-7 @M7
Feature: Adding a file to a work over HTTP
  As the work details of the web interface
  I want to upload a new version of a book into its work
  So that I do not need the command line to keep a work up to date

  Background:
    Given an empty working directory
    And I have run shelf with arguments "init library"
    And a sample EPUB "library/Ibryam, Bilgin/Kubernetes Patterns (2019)/kp-v1.epub" with the title "Kubernetes Patterns", the authors "Bilgin Ibryam" and the text seed 1
    And the sidecar "library/Ibryam, Bilgin/Kubernetes Patterns (2019)/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K1000000000000000001", "title": "Kubernetes Patterns", "authors": ["Bilgin Ibryam"], "year": 2019, "files": [{ "name": "kp-v1.epub" }] }
      """
    And a sample EPUB "upload/kp-v2.epub" with the title "Kubernetes Patterns", the authors "Bilgin Ibryam" and the text seed 2
    And I have run shelf with arguments "reindex --library library"
    And I take a snapshot of the folder "library"
    And the web server is running on the library "library"

  Scenario: The upload becomes a file of the work
    When I upload the file "upload/kp-v2.epub" with "POST /api/v1/works/01J9Z3K1000000000000000001/files?name=kp-v2.epub"
    Then the response status is 200
    And the JSON field "work" of the response is "01J9Z3K1000000000000000001"
    And the JSON field "file" of the response is "kp-v2.epub"
    And the file "library/Ibryam, Bilgin/Kubernetes Patterns (2019)/kp-v2.epub" exists
    And the sidecar "library/Ibryam, Bilgin/Kubernetes Patterns (2019)/book.json" lists the files "kp-v1.epub, kp-v2.epub"
    And the directory "library/.shelf/uploads" does not exist

  Scenario: The upload replaces a file, the old one goes to the trash
    When I upload the file "upload/kp-v2.epub" with "POST /api/v1/works/01J9Z3K1000000000000000001/files?name=kp-v2.epub&replace=01J9Z3K1000000000000000001~kp-v1.epub"
    Then the response status is 200
    And the JSON field "replaced" of the response is "kp-v1.epub"
    And the sidecar "library/Ibryam, Bilgin/Kubernetes Patterns (2019)/book.json" lists the files "kp-v2.epub"
    And the file "library/Ibryam, Bilgin/Kubernetes Patterns (2019)/kp-v1.epub" does not exist

  Scenario Outline: An upload that cannot be added is refused and nothing is changed
    When I upload the file "upload/kp-v2.epub" with "POST <request>"
    Then the response status is <status>
    And the folder "library" equals the snapshot

    Examples:
      | request                                                                                                              | status |
      | /api/v1/works/01J9Z3K9000000000000000009/files?name=kp-v2.epub                                                         | 404    |
      | /api/v1/works/01J9Z3K1000000000000000001/files                                                                        | 400    |
      | /api/v1/works/01J9Z3K1000000000000000001/files?name=kp-v2.epub&replace=01J9Z3K1000000000000000001~missing.epub          | 404    |
      | /api/v1/works/01J9Z3K1000000000000000001/files?name=kp-v2.epub&carryNotes=true                                         | 400    |
      | /api/v1/works/01J9Z3K1000000000000000001/files?name=notes.txt                                                          | 400    |

  Scenario: A file that is in the library already is a conflict
    When I upload the file "library/Ibryam, Bilgin/Kubernetes Patterns (2019)/kp-v1.epub" with "POST /api/v1/works/01J9Z3K1000000000000000001/files?name=again.epub"
    Then the response status is 409
    And the folder "library" equals the snapshot
