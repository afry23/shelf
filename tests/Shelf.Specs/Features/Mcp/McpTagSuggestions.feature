@F-TAG-6 @PRD-7.2 @M10
Feature: Tag suggestions through MCP
  As an AI assistant
  I want to see which tags the works most like a work carry
  So that I can tag a new work the way the person tagged similar ones

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
      { "schema": 1, "id": "01J9Z3K0000000000000000002", "title": "Event Sourcing in Practice", "authors": ["Jördis Ålander"], "language": "en", "year": 2021, "tags": ["architektur/ddd"], "files": [{ "name": "es.pdf" }] }
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

  Scenario: suggest_tags gives the tags of the most similar works, without the tags the work has
    When I call the tool "suggest_tags" with the arguments:
      """
      { "id": "01J9Z3K0000000000000000001" }
      """
    Then the tool call succeeds
    And the result has the string "id" equal to "01J9Z3K0000000000000000001"
    And the result has the number "total" equal to 2
    And the result has the string "suggestions[0].tag" equal to "cloud/aws"
    And the result has the number "suggestions[0].works" equal to 1

  Scenario: The number of suggestions can be limited
    When I call the tool "suggest_tags" with the arguments:
      """
      { "id": "01J9Z3K0000000000000000001", "limit": 1 }
      """
    Then the tool call succeeds
    And the result has the number "total" equal to 1

  Scenario: suggest_tags does not use the embedding model and changes nothing
    Given I take a snapshot of the folder "library"
    When I call the tool "suggest_tags" with the arguments:
      """
      { "id": "01J9Z3K0000000000000000001" }
      """
    Then the embedder embedded no queries
    And the folder "library" equals the snapshot

  Scenario Outline: The tool refuses what it cannot do
    When I call the tool "suggest_tags" with the arguments:
      """
      <arguments>
      """
    Then the tool call fails with the message containing "<message>"

    Examples:
      | arguments                                              | message |
      | { "id": "01ZZZZZZZZZZZZZZZZZZZZZZZZ" }                 | work    |
      | { "id": "01J9Z3K0000000000000000001", "limit": 0 }     | limit   |

  Scenario: suggest_tags only reads
    Then the tool "suggest_tags" is read-only
