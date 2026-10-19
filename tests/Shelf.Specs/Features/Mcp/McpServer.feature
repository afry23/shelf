@PRD-7.2 @AC-6 @M3
Feature: The MCP server
  As a user of an AI assistant such as Claude Desktop
  I want the assistant to look into my library through MCP
  So that it can find books and read passages for me

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
    And the MCP server is running on the library "library"

  Scenario: The server introduces itself
    Then the server is called "shelf"
    And the server instructions mention "search_books"

  Scenario: The tools are offered, and only two of them change anything
    Then the server offers exactly the tools:
      | tool                 |
      | add_tags             |
      | find_duplicates      |
      | find_probable_duplicates |
      | get_book             |
      | list_tags            |
      | propose_organization |
      | read_passage         |
      | remove_tags          |
      | search_books         |
      | semantic_search      |
      | similar_books        |
    And every tool except "add_tags" and "remove_tags" is marked read-only
    And no tool is marked destructive
    And every tool has a description
    And no tool name starts with "delete", "move", "trash" or "apply"

  Scenario: The tools say what they need
    Then the tool "search_books" requires the arguments "query"
    And the tool "get_book" requires the arguments "id"
    And the tool "read_passage" requires the arguments "id, location"
    And the tool "semantic_search" requires the arguments "query"
    And the tool "similar_books" requires the arguments "id"
    And the tool "add_tags" requires the arguments "id, tags"
    And the tool "remove_tags" requires the arguments "id, tags"
    And the tool "list_tags" requires no arguments
    And the tool "find_probable_duplicates" requires no arguments
    And the tool "propose_organization" requires no arguments

  Scenario Outline: Every argument has a description
    Then every argument of the tool "<tool>" has a description

    Examples:
      | tool                 |
      | search_books         |
      | semantic_search      |
      | similar_books        |
      | get_book             |
      | read_passage         |
      | add_tags             |
      | remove_tags          |
      | propose_organization |

  Scenario: Two kinds of resources are offered
    Then the server offers the resource templates:
      | uri                   |
      | shelf://book/{id}     |
      | shelf://book/{id}/toc |

  Scenario Outline: A result carries every property that its schema requires
    When I call the tool "<tool>" with the arguments:
      """
      <arguments>
      """
    Then the tool call succeeds
    And the result has every property that the output schema of "<tool>" requires

    Examples:
      | tool                 | arguments                                         |
      | search_books         | { "query": "kubernetes" }                         |
      | semantic_search      | { "query": "kubernetes" }                         |
      | similar_books        | { "id": "01J9Z3K0000000000000000002" }            |
      | add_tags             | { "id": "01J9Z3K0000000000000000002", "tags": ["x"] } |
      | remove_tags          | { "id": "01J9Z3K0000000000000000002", "tags": ["x"] } |
      | get_book             | { "id": "01J9Z3K0000000000000000002" }            |
      | get_book             | { "id": "01J9Z3K0000000000000000001" }            |
      | read_passage         | { "id": "01J9Z3K0000000000000000002", "location": "page=1" } |
      | list_tags            | {}                                                |
      | find_duplicates      | {}                                                |
      | find_probable_duplicates | {}                                            |
      | propose_organization | {}                                                |

  Scenario: An unknown tool is an error, and the server goes on
    When I call the unknown tool "delete_book"
    Then the call fails with the protocol code -32602
    When I call the tool "list_tags" with the arguments:
      """
      {}
      """
    Then the tool call succeeds

  Scenario: An argument of the wrong type is an error of the tool, and the server goes on
    When I call the tool "search_books" with the arguments:
      """
      { "query": "kubernetes", "limit": "many" }
      """
    Then the tool call fails
    When I call the tool "search_books" with the arguments:
      """
      { "query": "kubernetes" }
      """
    Then the tool call succeeds

  Scenario: A missing argument is an error of the tool
    When I call the tool "get_book" with the arguments:
      """
      {}
      """
    Then the tool call fails

  Scenario: The listing does not depend on the order in which the server sends it
    Then the tool names sorted are "add_tags, find_duplicates, find_probable_duplicates, get_book, list_tags, propose_organization, read_passage, remove_tags, search_books, semantic_search, similar_books"
