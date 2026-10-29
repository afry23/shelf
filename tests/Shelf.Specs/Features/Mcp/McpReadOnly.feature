@PRD-7.2 @M3
Feature: A read-only MCP server
  As a user who lets an assistant look into the library
  I want to switch off every tool that changes something
  So that the assistant cannot change my library

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


  Scenario: By default the tag tools are offered
    Given the MCP server is running on the library "library"
    Then the server offers the tools "add_tags" and "remove_tags"
    And the server offers the tools "add_note" and "get_annotations"
    And the server offers the tools "add_bookmark" and "get_book"

  Scenario Outline: With mcp.read_only they are not offered, in the library config or in the global config
    Given <where> contains the line "mcp.read_only = true"
    And the MCP server is running on the library "library"
    Then the server offers exactly the tools:
      | tool                 |
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
    And every tool is marked read-only
    And the server instructions mention "read-only"

    Examples:
      | where                                     |
      | the config file of the library "library"  |
      | the global config file                    |

  Scenario Outline: A tool that is not offered cannot be called
    Given the config file of the library "library" contains the line "mcp.read_only = true"
    And the MCP server is running on the library "library"
    And I take a snapshot of the folder "library"
    When I call the unknown tool "<tool>"
    Then the call fails with the protocol code -32602
    And the folder "library" equals the snapshot

    Examples:
      | tool        |
      | add_bookmark |
      | add_note    |
      | add_tags    |
      | remove_tags |

  Scenario: The read tools keep working
    Given the config file of the library "library" contains the line "mcp.read_only = true"
    And the MCP server is running on the library "library"
    When I call the tool "search_books" with the arguments:
      """
      { "query": "kubernetes" }
      """
    Then the result has the number "total" equal to 2

  Scenario: mcp.read_only = false offers the tools
    Given the config file of the library "library" contains the line "mcp.read_only = false"
    And the MCP server is running on the library "library"
    Then the server offers the tools "add_tags" and "remove_tags"
    And the server offers the tools "add_note" and "get_annotations"
