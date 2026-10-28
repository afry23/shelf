@F-GRP-9 @M7
Feature: Creating a work over HTTP
  As the library view of the web interface
  I want to upload a file as a work of its own
  So that I can add a book that grouping would put in the wrong place

  Background:
    Given an empty working directory
    And I have run shelf with arguments "init library"
    And a sample EPUB "upload/draft.epub" with the title "Wrong Title", the authors "Wrong Author" and the text seed 7
    And I have run shelf with arguments "reindex --library library"
    And the web server is running on the library "library"

  Scenario: The metadata of the file is used when nothing is given
    When I upload the file "upload/draft.epub" with "POST /api/v1/works?name=draft.epub"
    Then the response status is 200
    And the JSON field "work" of the response is not empty
    And the JSON field "file" of the response is "draft.epub"
    And the JSON field "folder" of the response is "Author, Wrong/Wrong Title"
    And the file "library/Author, Wrong/Wrong Title/draft.epub" exists

  Scenario: Given values replace the metadata of the file
    When I upload the file "upload/draft.epub" with "POST /api/v1/works?name=draft.epub&title=Real%20Title&author=Jane%20Doe&year=2026"
    Then the response status is 200
    And the JSON field "folder" of the response is "Doe, Jane/Real Title (2026)"
    And the sidecar "library/Doe, Jane/Real Title (2026)/book.json" has the title "Real Title" and the authors "Jane Doe"

  Scenario Outline: An upload that cannot become a work is refused
    When I upload the file "upload/draft.epub" with "POST <request>"
    Then the response status is 400
    And the directory "library/Author, Wrong" does not exist

    Examples:
      | request                                |
      | /api/v1/works                          |
      | /api/v1/works?name=notes.txt           |
      | /api/v1/works?name=draft.epub&year=abc |
