@PRD-7.2 @AC-6 @M3
Feature: The MCP server as a process
  As a user of Claude Desktop
  I want log output never to disturb the protocol
  So that the assistant and shelf can talk over standard input and output

  Background:
    Given an empty working directory
    And I have run shelf with arguments "init library"
    And a sample EPUB "library/Ibryam/Kubernetes Patterns/kp.epub" with the title "Kubernetes Patterns", the authors "Bilgin Ibryam" and the chapters:
      | title                 | text                                                                                       |
      | Foundational Patterns | The sidecar container extends the main container. Health probes tell the platform when a pod is alive. |
      | Behavioral Patterns   | A batch job runs to completion. A daemon service runs on every node of the cluster.        |
    And the sidecar "library/Ibryam/Kubernetes Patterns/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000001", "title": "Kubernetes Patterns", "subtitle": "Reusable Elements", "authors": ["Bilgin Ibryam"], "language": "en", "year": 2019, "description": "Design patterns for containers", "tags": ["cloud/kubernetes", "devops"], "readStatus": "read", "files": [{ "name": "kp.epub" }] }
      """
    And a sample PDF "library/Alander/Event Sourcing in Practice/es.pdf" with the title "Event Sourcing in Practice", the author "Jördis Ålander" and the pages:
      | text                                                                                          |
      | Event sourcing stores every change as an event. Aggregates replay events to rebuild their state. |
      | A projection builds a read model from the event stream. Kubernetes is only mentioned once here. |
    And the sidecar "library/Alander/Event Sourcing in Practice/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000002", "title": "Event Sourcing in Practice", "authors": ["Jördis Ålander"], "language": "en", "year": 2021, "tags": ["architektur/ddd", "cloud/azure"], "files": [{ "name": "es.pdf" }] }
      """
    And a sample EPUB "library/Weiss/Verteilte Systeme/vs.epub" with the title "Verteilte Systeme verstehen", the authors "Lukas Weiß" and the chapters:
      | title   | text                                                                      |
      | Konsens | Ein Konsensprotokoll sorgt dafür, dass alle Knoten denselben Zustand sehen. |
    And the sidecar "library/Weiss/Verteilte Systeme/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000003", "title": "Verteilte Systeme verstehen", "authors": ["Lukas Weiß"], "language": "de", "year": 2015, "tags": ["architektur/verteilte-systeme", "architektur/ddd"], "files": [{ "name": "vs.epub" }] }
      """
    And I have run shelf with arguments "reindex --library library"

  Scenario: Standard output carries nothing but protocol messages
    When I start "shelf mcp" as a process on the library "library" and send these requests:
      | request                                                                                                                          |
      | {"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-06-18","capabilities":{},"clientInfo":{"name":"spec","version":"1"}}} |
      | {"jsonrpc":"2.0","method":"notifications/initialized"}                                                                           |
      | {"jsonrpc":"2.0","id":2,"method":"tools/list"}                                                                                   |
      | {"jsonrpc":"2.0","id":3,"method":"tools/call","params":{"name":"search_books","arguments":{"query":"kubernetes"}}}              |
      | {"jsonrpc":"2.0","id":4,"method":"tools/call","params":{"name":"get_book","arguments":{"id":"nope"}}}                           |
    And I close its input
    Then every line on its standard output is a JSON-RPC message
    And its standard output has 4 responses
    And the response 3 of the process is a success
    And the response 4 of the process is a tool error
    And the process ends with the exit code 0

  Scenario: Log lines go to standard error
    Given the environment variable "SHELF_LOG_LEVEL" is set to "Debug"
    When I start "shelf mcp" as a process on the library "library" and send these requests:
      | request                                                                                                                          |
      | {"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-06-18","capabilities":{},"clientInfo":{"name":"spec","version":"1"}}} |
      | {"jsonrpc":"2.0","method":"notifications/initialized"}                                                                           |
      | {"jsonrpc":"2.0","id":2,"method":"tools/list"}                                                                                   |
    And I close its input
    Then every line on its standard output is a JSON-RPC message
    And its standard error is not empty
    And the process ends with the exit code 0

  Scenario: A library that does not exist is reported on standard error
    When I start "shelf mcp" as a process on the library "nowhere" and send these requests:
      | request |
    And I close its input
    Then its standard output is empty
    And its standard error contains "nowhere"
    And the process ends with the exit code 1
