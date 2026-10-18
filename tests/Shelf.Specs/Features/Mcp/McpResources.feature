@PRD-7.2 @M3
Feature: Reading about a work through MCP resources
  As an AI assistant
  I want to read the metadata and the table of contents of a work as resources
  So that I can keep them in my context

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

  Scenario: The metadata of a work is a resource
    When I read the resource "shelf://book/01J9Z3K0000000000000000001"
    Then the resource has the type "application/json"
    And the result has the string "title" equal to "Kubernetes Patterns"
    And the result has the array "toc" with 0 items
    And the result has the string "files[0].name" equal to "kp.epub"

  Scenario: The table of contents of a work is a resource
    When I read the resource "shelf://book/01J9Z3K0000000000000000001/toc"
    Then the resource has the type "application/json"
    And the resource is a list with 3 items
    And the result has the string "[1].title" equal to "Foundational Patterns"
    And the result has the string "[2].title" equal to "Behavioral Patterns"
    And the result has the string "[1].location" containing "epubcfi("

  Scenario Outline: An unknown work is a protocol error and the server goes on
    When I read the resource "<uri>"
    Then the call fails with the protocol code -32602
    When I read the resource "shelf://book/01J9Z3K0000000000000000002"
    Then the result has the string "title" equal to "Event Sourcing in Practice"

    Examples:
      | uri                                          |
      | shelf://book/01ZZZZZZZZZZZZZZZZZZZZZZZZ     |
      | shelf://book/01ZZZZZZZZZZZZZZZZZZZZZZZZ/toc |
