@PRD-7.2 @M5
Feature: The MCP server over Streamable HTTP
  As a user of an AI assistant that connects to a server instead of starting one
  I want shelf to serve the same MCP tools at an HTTP address
  So that an assistant on this machine, or on another one that I allow, can look into my library

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

  Scenario: The server introduces itself
    Given the MCP server is running over HTTP on the library "library"
    Then the server is called "shelf"
    And the server instructions mention "search_books"

  Scenario: The tools are those of the stdio server
    Given the MCP server is running over HTTP on the library "library"
    Then the server offers exactly the tools:
      | tool                 |
      | add_bookmark         |
      | add_note             |
      | add_tags             |
      | cite_passage         |
      | export_citations     |
      | find_duplicates      |
      | find_probable_duplicates |
      | get_annotations      |
      | get_book             |
      | list_tags            |
      | propose_organization |
      | read_passage         |
      | remove_tags          |
      | search_books         |
      | semantic_search      |
      | similar_books        |
      | suggest_tags         |
    And every tool except "add_bookmark", "add_note", "add_tags" and "remove_tags" is marked read-only
    And no tool is marked destructive
    And the server offers the resource templates:
      | uri                   |
      | shelf://book/{id}     |
      | shelf://book/{id}/toc |

  Scenario: A tool is called and answers with the same result as over standard input
    Given the MCP server is running over HTTP on the library "library"
    When I call the tool "search_books" with the arguments:
      """
      { "query": "kubernetes" }
      """
    Then the result has the number "total" equal to 2

  Scenario: A resource is read
    Given the MCP server is running over HTTP on the library "library"
    When I read the resource "shelf://book/01J9Z3K0000000000000000002"
    Then the resource has the type "application/json"
    And the result has the string "title" equal to "Event Sourcing in Practice"

  Scenario: A tag is added over HTTP and stays in the sidecar
    Given the MCP server is running over HTTP on the library "library"
    When I call the tool "add_tags" with the arguments:
      """
      { "id": "01J9Z3K0000000000000000002", "tags": ["architektur/events"] }
      """
    Then the tool call succeeds
    And the file "library/Alander/Event Sourcing in Practice/book.json" contains "architektur/events"

  Scenario: With mcp.read_only the tools that change something are not offered, and not callable
    Given the config file of the library "library" contains the line "mcp.read_only = true"
    And the MCP server is running over HTTP on the library "library"
    Then the server offers exactly the tools:
      | tool                 |
      | cite_passage         |
      | export_citations     |
      | find_duplicates      |
      | find_probable_duplicates |
      | get_annotations      |
      | get_book             |
      | list_tags            |
      | propose_organization |
      | read_passage         |
      | search_books         |
      | semantic_search      |
      | similar_books        |
      | suggest_tags         |
    And every tool is marked read-only
    And the server instructions mention "read-only"
    When I call the unknown tool "add_tags"
    Then the call fails with the protocol code -32602

  Scenario: An error of a tool does not end the server
    Given the MCP server is running over HTTP on the library "library"
    When I call the tool "get_book" with the arguments:
      """
      {}
      """
    Then the tool call fails
    When I call the tool "list_tags" with the arguments:
      """
      {}
      """
    Then the tool call succeeds
