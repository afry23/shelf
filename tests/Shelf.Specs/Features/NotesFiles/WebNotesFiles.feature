@F-NOTEF-1 @F-NOTEF-6 @M15
Feature: Uploading a notes file over HTTP
  As the work details of the web interface
  I want to upload the notes of an e-reader into a work
  So that I do not need the command line to keep my notes with the book

  Background:
    Given an empty working directory
    And I have run shelf with arguments "init library"
    And a sample EPUB "library/Ibryam, Bilgin/Kubernetes Patterns (2019)/kp.epub" with the title "Kubernetes Patterns", the authors "Bilgin Ibryam" and the text seed 1
    And the sidecar "library/Ibryam, Bilgin/Kubernetes Patterns (2019)/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K1000000000000000001", "title": "Kubernetes Patterns", "authors": ["Bilgin Ibryam"], "year": 2019, "files": [{ "name": "kp.epub" }] }
      """
    And the file "upload/kindle.txt" has the content "Highlight: the sidecar container extends the main container."
    And the file "upload/notes.epub" has the content "data"
    And I have run shelf with arguments "reindex --library library"
    And the web server is running on the library "library"

  Scenario: A TXT upload becomes a notes file of the work
    When I upload the file "upload/kindle.txt" with "POST /api/v1/works/01J9Z3K1000000000000000001/files?name=kindle.txt&role=notes"
    Then the response status is 200
    And the JSON field "file" of the response is "kindle.txt"
    And the file "library/Ibryam, Bilgin/Kubernetes Patterns (2019)/kindle.txt" exists
    And the file "kindle.txt" in the index has the role "notes"

  Scenario: The details of the work show the role of each file
    Given I have run shelf with arguments "add 01J9Z3K1 --notes upload/kindle.txt --library library"
    When I request "GET /api/v1/works/01J9Z3K1000000000000000001"
    Then the response status is 200
    And the response body contains "notes"

  Scenario Outline: An upload that is no notes file is refused
    When I upload the file "upload/<file>" with "POST /api/v1/works/01J9Z3K1000000000000000001/files?name=<file>&role=<role>"
    Then the response status is <status>

    Examples:
      | file       | role    | status |
      | notes.epub | notes   | 400    |
      | kindle.txt | unknown | 400    |
      | kindle.txt | book    | 400    |
