@F-WEB-1 @M4
Feature: The web server
  As a user of shelf
  I want to start the web interface with one command
  So that I can use my library in the browser without exposing it to the network

  Background:
    Given an empty working directory
    And I have run shelf with arguments "init library"

  Scenario: The server announces its address on standard error and keeps standard output empty
    When the web server is running on the library "library"
    Then the server announced an address on loopback
    And the server wrote nothing to standard output

  Scenario Outline: A request to the server on the loopback address is answered
    Given the web server is running on the library "library"
    When I request "GET <path>"
    Then the response status is <status>

    Examples:
      | path              | status |
      | /api/v1/status    | 200    |
      | /api/v1/nothing   | 404    |

  Scenario: A library that does not exist is reported and nothing starts
    When I run shelf with arguments "serve --library nowhere --port 0"
    Then the exit code is 1
    And standard error contains "nowhere"

  Scenario: A port that is in use is reported
    Given the web server is running on the library "library"
    When I run shelf with the arguments "serve --library library --port {port}" while the web server is running
    Then the exit code is 1
    And standard error contains "already in use"

  Scenario: The server stops when it is interrupted
    Given the web server is running on the library "library"
    When the server is interrupted
    Then the server ends with the exit code 0

  Scenario Outline: A request that names another host is refused, so that a web page cannot reach the server by DNS rebinding
    Given the web server is running on the library "library"
    When I request "GET /api/v1/status" with the header "Host: <host>"
    Then the response status is <status>

    Examples:
      | host                     | status |
      | 127.0.0.1:{port}     | 200    |
      | localhost:{port}     | 200    |
      | evil.example             | 421    |
      | evil.example:{port}  | 421    |
      | 192.168.1.20             | 421    |
