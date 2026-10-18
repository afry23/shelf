@F-SEM-1 @F-SEM-3 @AC-4 @M3
Feature: Finding English books with German questions
  As a German-speaking reader with a technical library
  I want to ask in German for books that are written in English
  So that I find them by what they are about

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

  Scenario Outline: A German question finds the English book it is about
    When I run shelf with arguments "semantic <question> --library library"
    Then the exit code is 0
    And the first search result is "<book>"

    Examples:
      | question                                         | book                            |
      | Bücher über Messaging zwischen Microservices     | Enterprise Integration Patterns |
      | Wie backe ich Brot mit Sauerteig                 | The Sourdough Handbook          |
      | Wie speichern Datenbanken Daten auf der Festplatte | Database Internals            |
      | Container auf einem Cluster betreiben            | Kubernetes in Action            |

  Scenario: The passage that answers the question is found with its location
    When I run shelf with arguments "semantic Wie werden Nachrichten asynchron zwischen Diensten verteilt --chunks --library library --limit 1"
    Then the exit code is 0
    And the first search result is "Enterprise Integration Patterns"
    And standard output contains "eip.epub:"
    And standard output contains "Message brokers and queues"
