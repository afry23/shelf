@PRD-7.2 @M5
Feature: The rules of the MCP HTTP server
  As a user who lets an assistant connect to shelf
  I want the HTTP server to stay on my machine unless I say otherwise
  So that my library is not open to anybody on the network

  Background:
    Given an empty working directory
    And I have run shelf with arguments "init library"

  Scenario: The server announces the address of the endpoint on standard error and keeps standard output empty
    When the MCP HTTP server is running on the library "library"
    Then the server announced an address on loopback
    And the standard error of the server contains "/mcp"
    And the server wrote nothing to standard output

  Scenario: The server stops when it is interrupted
    Given the MCP HTTP server is running on the library "library"
    When the server is interrupted
    Then the server ends with the exit code 0

  Scenario Outline: A request that names another host is refused, so that a web page cannot reach the server by DNS rebinding
    Given the MCP HTTP server is running on the library "library"
    When I send "POST /mcp" with the header "Host: <host>" and the JSON:
      """
      { "jsonrpc": "2.0", "id": 1, "method": "ping" }
      """
    Then the response status is <status>

    Examples:
      | host                | status |
      | 127.0.0.1:{port}    | 200    |
      | localhost:{port}    | 200    |
      | evil.example        | 421    |
      | evil.example:{port} | 421    |

  Scenario Outline: With a token the endpoint answers to a request that carries it as a bearer token, and to no other
    Given the MCP HTTP server is running on the library "library" with the arguments "--token s3cret"
    When I send "POST /mcp" with the header "<header>" and the JSON:
      """
      { "jsonrpc": "2.0", "id": 1, "method": "ping" }
      """
    Then the response status is <status>

    Examples:
      | header                      | status |
      | Authorization: Bearer s3cret | 200    |
      | authorization: bearer s3cret | 200    |
      | Authorization: Bearer wrong  | 401    |
      | Authorization: Basic s3cret  | 401    |
      | Authorization: s3cret        | 401    |
      | X-Other: s3cret              | 401    |

  Scenario: A request without the token is refused with the scheme to use
    Given the MCP HTTP server is running on the library "library" with the arguments "--token s3cret"
    When I send "POST /mcp" with the JSON:
      """
      { "jsonrpc": "2.0", "id": 1, "method": "ping" }
      """
    Then the response status is 401
    And the response header "WWW-Authenticate" is "Bearer"
    And the response header "Content-Type" starts with "application/problem+json"

  Scenario: A token in the query is no token
    Given the MCP HTTP server is running on the library "library" with the arguments "--token s3cret"
    When I send "POST /mcp?token=s3cret" with the JSON:
      """
      { "jsonrpc": "2.0", "id": 1, "method": "ping" }
      """
    Then the response status is 401

  Scenario: The token of the config is used when the command line has none
    Given the global config file contains the line "web.token = 'from-config'"
    And the MCP HTTP server is running on the library "library"
    When I send "POST /mcp" with the header "Authorization: Bearer from-config" and the JSON:
      """
      { "jsonrpc": "2.0", "id": 1, "method": "ping" }
      """
    Then the response status is 200
    When I send "POST /mcp" with the JSON:
      """
      { "jsonrpc": "2.0", "id": 1, "method": "ping" }
      """
    Then the response status is 401

  Scenario: The token of the command line wins over the config
    Given the global config file contains the line "web.token = 'from-config'"
    And the MCP HTTP server is running on the library "library" with the arguments "--token from-cli"
    When I send "POST /mcp" with the header "Authorization: Bearer from-config" and the JSON:
      """
      { "jsonrpc": "2.0", "id": 1, "method": "ping" }
      """
    Then the response status is 401

  Scenario: The token never appears in a log line
    Given the log level is "Trace"
    And the MCP HTTP server is running on the library "library" with the arguments "--token s3cret"
    And I send "POST /mcp?token=s3cret" with the JSON:
      """
      { "jsonrpc": "2.0", "id": 1, "method": "ping" }
      """
    When I send "POST /mcp" with the header "Authorization: Bearer s3cret" and the JSON:
      """
      { "jsonrpc": "2.0", "id": 1, "method": "ping" }
      """
    Then the standard error of the server does not contain "s3cret"

  Scenario Outline: An address that is not loopback needs a token
    When I run shelf with arguments "mcp --http --library library --port 0 --bind <address>"
    Then the exit code is 2
    And standard error contains "requires --token"

    Examples:
      | address |
      | 0.0.0.0 |
      | ::      |

  Scenario: With a token the server may listen on all interfaces
    When the MCP HTTP server is running on the library "library" with the arguments "--bind 0.0.0.0 --token s3cret"
    Then the server announced an address on all interfaces

  Scenario: A bind value that is no IP address is refused
    When I run shelf with arguments "mcp --http --library library --port 0 --bind example.org --token s3cret"
    Then the exit code is 2
    And standard error contains "is not an IP address"

  Scenario Outline: A port outside the range is refused
    When I run shelf with arguments "mcp --http --library library --port <port>"
    Then the exit code is 2
    And standard error contains "--port must be between 0 and 65535"

    Examples:
      | port  |
      | -1    |
      | 65536 |

  Scenario Outline: The options of the HTTP server need --http
    When I run shelf with arguments "mcp --library library <option>"
    Then the exit code is 2
    And standard error contains "only apply with --http"

    Examples:
      | option         |
      | --port 8421    |
      | --bind 0.0.0.0 |
      | --token s3cret |

  Scenario: A port that is in use is reported
    Given the MCP HTTP server is running on the library "library"
    When I run shelf with the arguments "mcp --http --library library --port {port}" while the web server is running
    Then the exit code is 1
    And standard error contains "already in use"

  Scenario: A library that does not exist is reported and nothing starts
    When I run shelf with arguments "mcp --http --library nowhere --port 0"
    Then the exit code is 1
    And standard error contains "nowhere"
