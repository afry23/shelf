@PRD-7.2 @F-SEM-6 @M5
Feature: Similar books through MCP
  As an AI assistant
  I want to ask for the works that are most like a work
  So that I can suggest what to read next

  Background:
    Given an empty working directory
    And I have run shelf with arguments "init library"
    And the model catalog "multilingual-e5-small" has the files:
      | file                    | content        |
      | model.onnx              | onnx weights   |
      | sentencepiece.bpe.model | spm vocabulary |
    And the folder "offline" holds the model files
    And I have run shelf with arguments "model install offline --library library"
    And the embedder is a word-hashing fake of 64 dimensions
    And a sample EPUB "library/Ibryam/Kubernetes Patterns/kp.epub" with the title "Kubernetes Patterns", the authors "Bilgin Ibryam" and the chapters:
      | title                 | text                                              |
      | Foundational Patterns | The sidecar container extends the main container. |
    And the sidecar "library/Ibryam/Kubernetes Patterns/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000001", "title": "Kubernetes Patterns", "subtitle": "Reusable Elements", "authors": ["Bilgin Ibryam"], "language": "en", "year": 2019, "description": "Design patterns for containers", "tags": ["cloud/kubernetes", "devops"], "files": [{ "name": "kp.epub" }] }
      """
    And a sample PDF "library/Alander/Event Sourcing in Practice/es.pdf" with the title "Event Sourcing in Practice", the author "Jördis Ålander" and the pages:
      | text                                                                  |
      | Event sourcing stores every change as an event in an append only log. |
    And the sidecar "library/Alander/Event Sourcing in Practice/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000002", "title": "Event Sourcing in Practice", "authors": ["Jördis Ålander"], "language": "en", "year": 2021, "files": [{ "name": "es.pdf" }] }
      """
    And a sample EPUB "library/Weiss/Verteilte Systeme/vs.epub" with the title "Verteilte Systeme verstehen", the authors "Lukas Weiß" and the chapters:
      | title   | text                                                           |
      | Konsens | Ein Konsensprotokoll sorgt dafür, dass alle Knoten einig sind. |
    And the sidecar "library/Weiss/Verteilte Systeme/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000003", "title": "Verteilte Systeme verstehen", "authors": ["Lukas Weiß"], "language": "de", "year": 2015, "files": [{ "name": "vs.epub" }] }
      """
    And a sample PDF "library/Mueller/Cloud Patterns/cp.pdf" with the title "Cloud Patterns", the author "Anna Müller" and the pages:
      | text                                         |
      | Serverless functions scale down to zero.     |
    And the sidecar "library/Mueller/Cloud Patterns/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000004", "title": "Cloud Patterns", "subtitle": "Reusable Elements", "authors": ["Anna Müller"], "language": "en", "year": 2020, "description": "Design patterns for containers in the cloud", "tags": ["cloud/aws", "devops"], "files": [{ "name": "cp.pdf" }] }
      """
    And I have run shelf with arguments "reindex --library library"
    And I have run shelf with arguments "reindex --embeddings --library library"
    And the embedder calls are forgotten
    And the MCP server is running on the library "library"

  Scenario: similar_books gives the works nearest to a work, without the work itself
    When I call the tool "similar_books" with the arguments:
      """
      { "id": "01J9Z3K0000000000000000001" }
      """
    Then the tool call succeeds
    And the result has the string "id" equal to "01J9Z3K0000000000000000001"
    And the result has the string "title" equal to "Kubernetes Patterns"
    And the result has the number "total" equal to 3
    And the result has the string "results[0].title" equal to "Cloud Patterns"
    And the result has the property "results[0].passage" set to null

  Scenario: The number of works can be limited and the ID can be shortened to what names one work
    When I call the tool "similar_books" with the arguments:
      """
      { "id": "01J9Z3K0000000000000000004", "limit": 1 }
      """
    Then the tool call succeeds
    And the result has the number "total" equal to 1

  Scenario: similar_books does not use the embedding model
    When I call the tool "similar_books" with the arguments:
      """
      { "id": "01J9Z3K0000000000000000001" }
      """
    Then the embedder embedded no queries

  Scenario Outline: A wrong call is a tool error that the assistant can read
    When I call the tool "similar_books" with the arguments:
      """
      <arguments>
      """
    Then the tool call fails with the message containing "<message>"

    Examples:
      | arguments                                          | message                |
      | { "id": "01J9Z3K9000000000000000009" }             | no work matches        |
      | { "id": "01J9Z3K" }                                | matches 4 works        |
      | { "id": "01J9Z3K0000000000000000001", "limit": 0 } | limit must be between  |

  Scenario: similar_books is a read-only tool
    Then the tool "similar_books" requires the arguments "id"
    And every argument of the tool "similar_books" has a description
