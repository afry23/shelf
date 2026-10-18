@PRD-7.2 @AC-6 @M3
Feature: An assistant uses the library through a real shelf process
  As a user of Claude Desktop
  I want search_books, semantic_search and read_passage to work over standard input and output
  So that the assistant can answer a German question about English books, with no log output in the way

  Background:
    Given an empty working directory
    And I have run shelf with arguments "init library"
    And the real embedding model is installed in the library "library"
    And a sample EPUB "library/Hohpe/Enterprise Integration Patterns/eip.epub" with the title "Enterprise Integration Patterns", the authors "Gregor Hohpe" and the chapters:
      | title      | text                                                                                                                                   |
      | Messaging  | Message brokers and queues decouple microservices. A service publishes events to a topic and other services subscribe to it asynchronously. |
    And the sidecar "library/Hohpe/Enterprise Integration Patterns/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000011", "title": "Enterprise Integration Patterns", "authors": ["Gregor Hohpe"], "language": "en", "description": "Designing and building solutions with messaging", "files": [{ "name": "eip.epub" }] }
      """
    And a sample EPUB "library/Luksa/Kubernetes in Action/kia.epub" with the title "Kubernetes in Action", the authors "Marko Luksa" and the chapters:
      | title | text                                                                                          |
      | Pods  | A pod runs containers on a node of the cluster. A deployment rolls out new versions of an application. |
    And the sidecar "library/Luksa/Kubernetes in Action/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000012", "title": "Kubernetes in Action", "authors": ["Marko Luksa"], "language": "en", "description": "Running containers on a cluster", "files": [{ "name": "kia.epub" }] }
      """
    And a sample EPUB "library/Baker/The Sourdough Handbook/sh.epub" with the title "The Sourdough Handbook", the authors "Anna Baker" and the chapters:
      | title   | text                                                                                     |
      | Starter | Mix flour and water into a starter and let it ferment for a week before you bake bread. |
    And the sidecar "library/Baker/The Sourdough Handbook/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000013", "title": "The Sourdough Handbook", "authors": ["Anna Baker"], "language": "en", "description": "Baking bread at home", "files": [{ "name": "sh.epub" }] }
      """
    And a sample EPUB "library/Petrov/Database Internals/di.epub" with the title "Database Internals", the authors "Alex Petrov" and the chapters:
      | title   | text                                                                                              |
      | Storage | B-trees keep keys in sorted pages on disk. A write ahead log makes every commit durable.          |
    And the sidecar "library/Petrov/Database Internals/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000014", "title": "Database Internals", "authors": ["Alex Petrov"], "language": "en", "description": "How databases store and retrieve data", "files": [{ "name": "di.epub" }] }
      """
    And I have run shelf with arguments "reindex --library library"
    And I have run shelf with arguments "reindex --embeddings --library library"


  Scenario: A German question finds the English book, and its text can be read
    When I start "shelf mcp" as a process on the library "library" and send these requests:
      | request |
      | {"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-06-18","capabilities":{},"clientInfo":{"name":"spec","version":"1"}}} |
      | {"jsonrpc":"2.0","method":"notifications/initialized"} |
      | {"jsonrpc":"2.0","id":2,"method":"tools/call","params":{"name":"search_books","arguments":{"query":"messaging"}}} |
      | {"jsonrpc":"2.0","id":3,"method":"tools/call","params":{"name":"semantic_search","arguments":{"query":"Bücher über Messaging zwischen Microservices","limit":1}}} |
      | {"jsonrpc":"2.0","id":4,"method":"tools/call","params":{"name":"read_passage","arguments":{"id":"01J9Z3K0000000000000000011","location":"epubcfi(/6/2[chapter1]!)"}}} |
    And I close its input
    Then every line on its standard output is a JSON-RPC message
    And the response 2 of the process is a success
    And the response 2 of the process contains "Enterprise Integration Patterns"
    And the response 3 of the process is a success
    And the response 3 of the process contains "Enterprise Integration Patterns"
    And the response 3 of the process does not contain "Kubernetes in Action"
    And the response 4 of the process is a success
    And the response 4 of the process contains "Message brokers and queues decouple microservices"
    And its standard error is empty
    And the process ends with the exit code 0
