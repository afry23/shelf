@F-DOC-3 @M21
Feature: The health check over HTTP
  As a script or a later health page of the web interface
  I want the report of shelf doctor as JSON
  So that the same checks are available without the command line

  Background:
    Given an empty working directory
    And I have run shelf with arguments "init library"
    And a sample PDF "library/Ibryam/Kubernetes Patterns/kp.pdf" with the title "Kubernetes Patterns", the author "Bilgin Ibryam" and the text seed 1
    And the sidecar "library/Ibryam/Kubernetes Patterns/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000001", "title": "Kubernetes Patterns", "authors": ["Bilgin Ibryam"], "language": "en", "year": 2019, "files": [{ "name": "kp.pdf" }] }
      """
    And a sample PDF "library/Mueller/Cloud Native/cn.pdf" with the title "Cloud Native", the author "Anna Müller" and the text seed 2
    And the sidecar "library/Mueller/Cloud Native/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000004", "title": "Cloud Native", "authors": ["Anna Müller"], "language": "en", "year": 2017, "files": [{ "name": "cn.pdf" }] }
      """
    And a sample PDF "library/Weiss/Verteilte Systeme/vs.pdf" with the title "Verteilte Systeme verstehen", the author "Lukas Weiß" and the text seed 3
    And the sidecar "library/Weiss/Verteilte Systeme/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000003", "title": "Verteilte Systeme verstehen", "authors": ["Lukas Weiß"], "language": "de", "year": 2015, "files": [{ "name": "vs.pdf" }] }
      """
    And I have run shelf with arguments "reindex --library library"
    And the web server is running on the library "library"

  Scenario: The report of a healthy library
    When I request "GET /api/v1/doctor"
    Then the response status is 200
    And the JSON field "works" of the response is 3
    And the JSON field "errors" of the response is 0
    And the JSON field "warnings" of the response is 0
    And the response body contains "sidecars"

  Scenario: A problem is reported in the body, not as an error of the request
    Given I delete the file "library/Ibryam/Kubernetes Patterns/kp.pdf"
    When I request "GET /api/v1/doctor"
    Then the response status is 200
    And the JSON field "errors" of the response is 1
    And the response body contains "kp.pdf"
