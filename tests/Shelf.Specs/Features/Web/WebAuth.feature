@F-WEB-2 @M4
Feature: Access to the web server
  As the owner of a library
  I want the server to refuse network access without a token
  So that nobody else on the network can read or change my library

  Background:
    Given an empty working directory
    And I have run shelf with arguments "init library"

  Scenario Outline: A non-loopback address needs a token
    When I run shelf with arguments "serve --library library --port 0 --bind <address>"
    Then the exit code is 2
    And standard error contains "requires --token"
    And standard output is empty

    Examples:
      | address      |
      | 0.0.0.0      |
      | ::           |
      | 192.168.1.10 |

  Scenario Outline: A bind value that is no IP address is refused
    When I run shelf with arguments "serve --library library --port 0 --bind <value> --token s3cret"
    Then the exit code is 2
    And standard error contains "is not an IP address"

    Examples:
      | value       |
      | localhost   |
      | example.com |
      | all         |
      | 127.1       |
      | 0           |

  Scenario: An empty token counts as no token
    When I run shelf with arguments "serve --library library --port 0 --bind 0.0.0.0 --token ''"
    Then the exit code is 2
    And standard error contains "requires --token"

  Scenario: The server starts on all interfaces when it has a token
    When the web server is running on the library "library" with the arguments "--bind 0.0.0.0 --token s3cret"
    Then the server announced an address on all interfaces

  Scenario Outline: The API checks the token in the Authorization header
    Given the web server is running on the library "library" with the arguments "--token s3cret"
    When I request "GET /api/v1/status" with the header "<header>"
    Then the response status is <status>
    And the response header "WWW-Authenticate" is <challenge>

    Examples:
      | header                        | status | challenge |
      | Authorization: Bearer wrong   | 401    | "Bearer"  |
      | Authorization: Bearer S3CRET  | 401    | "Bearer"  |
      | Authorization: Basic s3cret   | 401    | "Bearer"  |
      | Authorization: Bearer         | 401    | "Bearer"  |
      | Authorization: Bearer s3cret  | 200    | none      |
      | Authorization: bearer s3cret  | 200    | none      |

  Scenario: A refused request is a problem document
    Given the web server is running on the library "library" with the arguments "--token s3cret"
    When I request "GET /api/v1/status"
    Then the response status is 401
    And the response header "WWW-Authenticate" is "Bearer"
    And the response header "Content-Type" is "application/problem+json"
    And the JSON field "status" of the response is 401
    And the JSON field "title" of the response is "Unauthorized"

  Scenario: A link with the token sets a cookie and leads to the address without the token
    Given the web server is running on the library "library" with the arguments "--token s3cret"
    When I request "GET /library?token=s3cret"
    Then the response status is 303
    And the response header "Location" is "/library"
    And the response sets the cookie "shelf_token" with the attributes "HttpOnly, SameSite=Strict, Path=/"

  Scenario: The cookie gives access to the API
    Given the web server is running on the library "library" with the arguments "--token s3cret"
    And I request "GET /?token=s3cret"
    When I request "GET /api/v1/status" with the cookie
    Then the response status is 200

  Scenario: A link with a wrong token sets no cookie
    Given the web server is running on the library "library" with the arguments "--token s3cret"
    When I request "GET /?token=wrong"
    Then the response status is 401
    And the response sets no cookie

  Scenario: Other query parameters survive the redirect of the token link
    Given the web server is running on the library "library" with the arguments "--token s3cret"
    When I request "GET /search?q=event&token=s3cret&view=list"
    Then the response status is 303
    And the response header "Location" is "/search?q=event&view=list"

  Scenario: A token is needed on loopback too when one is given
    Given the web server is running on the library "library" with the arguments "--token s3cret"
    When I request "GET /api/v1/status"
    Then the response status is 401

  Scenario: The token from the config is used
    Given the global config contains the line "web.token = \"from-config\""
    And the web server is running on the library "library"
    When I request "GET /api/v1/status" with the header "Authorization: Bearer from-config"
    Then the response status is 200

  Scenario: The token of the command line wins over the config
    Given the global config contains the line "web.token = \"from-config\""
    And the web server is running on the library "library" with the arguments "--token from-cli"
    When I request "GET /api/v1/status" with the header "Authorization: Bearer from-config"
    Then the response status is 401

  Scenario: The token never appears in a log line
    Given the log level is "Trace"
    And the web server is running on the library "library" with the arguments "--token s3cret"
    And I request "GET /api/v1/status?token=s3cret"
    When I request "GET /api/v1/status" with the header "Authorization: Bearer s3cret"
    Then the standard error of the server does not contain "s3cret"
