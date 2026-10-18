@PRD-7.3 @F-RDR-3 @M4
Feature: Files over HTTP
  As the reader and the download button of the web interface
  I want to fetch a file, or just a part of it
  So that a large PDF opens at once and a book can be saved

  Background:
    Given an empty working directory
    And I have run shelf with arguments "init library"
    And a sample EPUB "library/Ibryam/Kubernetes Patterns/kp.epub" with the title "Kubernetes Patterns", the authors "Bilgin Ibryam" and the text seed 3
    And a sample PDF "library/Ibryam/Kubernetes Patterns/Kubernetes Patterns (2nd).pdf" with the title "Kubernetes Patterns", the author "Bilgin Ibryam" and the text seed 3
    And the sidecar "library/Ibryam/Kubernetes Patterns/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000001", "title": "Kubernetes Patterns", "authors": ["Bilgin Ibryam"], "year": 2019, "files": [{ "name": "kp.epub" }, { "name": "Kubernetes Patterns (2nd).pdf" }] }
      """
    And I have run shelf with arguments "reindex --library library"
    And the web server is running on the library "library"

  Scenario: The details of a work name its files by an ID
    When I request "GET /api/v1/works/01J9Z3K0000000000000000001"
    Then the JSON field "files[0].id" of the response is "01J9Z3K0000000000000000001~kp.epub"
    And the JSON field "files[1].id" of the response is "01J9Z3K0000000000000000001~Kubernetes Patterns (2nd).pdf"

  Scenario Outline: A file is served whole, with its media type
    When I request "GET /api/v1/files/<id>"
    Then the response status is 200
    And the response header "Content-Type" is "<type>"
    And the response header "Accept-Ranges" is "bytes"
    And the response header "Content-Disposition" is none
    And the response body is the file "<path>"

    Examples:
      | id                                                       | type                 | path                                                              |
      | 01J9Z3K0000000000000000001~kp.epub                       | application/epub+zip | library/Ibryam/Kubernetes Patterns/kp.epub                        |
      | 01J9Z3K0000000000000000001~Kubernetes%20Patterns%20(2nd).pdf | application/pdf  | library/Ibryam/Kubernetes Patterns/Kubernetes Patterns (2nd).pdf  |

  Scenario Outline: A part of a file is served for a range
    When I request "GET /api/v1/files/01J9Z3K0000000000000000001~kp.epub" with the header "Range: bytes=<range>"
    Then the response status is 206
    And the response header "Accept-Ranges" is "bytes"
    And the response header "Content-Range" starts with "bytes <first>-<last>/"
    And the response body is bytes <first> to <last> of the file "library/Ibryam/Kubernetes Patterns/kp.epub"

    Examples:
      | range   | first | last |
      | 0-99    | 0     | 99   |
      | 100-199 | 100   | 199  |
      | 0-0     | 0     | 0    |
      | 500-500 | 500   | 500  |

  Scenario: A range without an end reads to the end of the file
    When I request "GET /api/v1/files/01J9Z3K0000000000000000001~kp.epub" with the header "Range: bytes=200-"
    Then the response status is 206
    And the response body is the file "library/Ibryam/Kubernetes Patterns/kp.epub" from byte 200

  Scenario: A range with only a length reads the end of the file
    When I request "GET /api/v1/files/01J9Z3K0000000000000000001~kp.epub" with the header "Range: bytes=-100"
    Then the response status is 206
    And the response body is the last 100 bytes of the file "library/Ibryam/Kubernetes Patterns/kp.epub"

  Scenario: A range beyond the end of the file cannot be met
    When I request "GET /api/v1/files/01J9Z3K0000000000000000001~kp.epub" with the header "Range: bytes=99999999-"
    Then the response status is 416
    And the response header "Content-Range" starts with "bytes */"
    And the response header "Content-Type" is "application/problem+json"

  Scenario: Several ranges are answered with the whole file
    When I request "GET /api/v1/files/01J9Z3K0000000000000000001~kp.epub" with the header "Range: bytes=0-9,20-29"
    Then the response status is 200
    And the response body is the file "library/Ibryam/Kubernetes Patterns/kp.epub"

  Scenario: HEAD gives the headers and no body
    When I request "HEAD /api/v1/files/01J9Z3K0000000000000000001~kp.epub"
    Then the response status is 200
    And the response header "Content-Type" is "application/epub+zip"
    And the response header "Content-Length" is the size of the file "library/Ibryam/Kubernetes Patterns/kp.epub"
    And the response body is empty

  Scenario: A file that has not changed is not sent again
    Given I request "GET /api/v1/files/01J9Z3K0000000000000000001~kp.epub"
    When I request "GET /api/v1/files/01J9Z3K0000000000000000001~kp.epub" again with the ETag
    Then the response status is 304
    And the response body is empty

  Scenario: A file that has changed is sent again
    Given I request "GET /api/v1/files/01J9Z3K0000000000000000001~kp.epub"
    And the file "library/Ibryam/Kubernetes Patterns/kp.epub" has a new modification time
    When I request "GET /api/v1/files/01J9Z3K0000000000000000001~kp.epub" again with the ETag
    Then the response status is 200

  Scenario: A download names the file
    When I request "GET /api/v1/files/01J9Z3K0000000000000000001~Kubernetes%20Patterns%20(2nd).pdf?download=true"
    Then the response status is 200
    And the response header "Content-Disposition" starts with "attachment"
    And the response header "Content-Disposition" contains "Kubernetes Patterns (2nd).pdf"

  Scenario Outline: A file that is not there is a 404 with the reason
    When I request "GET /api/v1/files/<id>"
    Then the response status is 404
    And the response header "Content-Type" is "application/problem+json"
    And the JSON field "detail" of the response contains "<message>"

    Examples:
      | id                                                     | message           |
      | 01J9Z3K0000000000000000009~kp.epub                     | no work matches   |
      | 01J9Z3K0000000000000000001~nothing.epub                | no file           |
      | 01J9Z3K0000000000000000001~..%2F..%2Fsecret.epub       | no file           |
      | 01J9Z3K0000000000000000001~book.json                   | no file           |
      | 01J9Z3K0000000000000000001                             | not the ID of a file |
      | kp.epub                                                | not the ID of a file |
      | ~kp.epub                                               | not the ID of a file |

  Scenario: A file that is listed but gone from the disk is a 404
    Given I delete the file "library/Ibryam/Kubernetes Patterns/kp.epub"
    When I request "GET /api/v1/files/01J9Z3K0000000000000000001~kp.epub"
    Then the response status is 404
    And the JSON field "detail" of the response contains "is missing"

  Scenario: A file needs the access token like every call of the API
    Given the web server is running on the library "library" with the arguments "--token s3cret"
    When I request "GET /api/v1/files/01J9Z3K0000000000000000001~kp.epub"
    Then the response status is 401
