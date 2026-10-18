@PRD-7.2 @F-SEM-1 @F-SEM-5 @M3
Feature: Searching by meaning through MCP
  As an AI assistant
  I want to find works and passages by what they are about
  So that I can answer questions in any language

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
      | title                 | text                                                       |
      | Foundational Patterns | The sidecar container extends the main container.          |
      | Behavioral Patterns   | A batch job runs to completion on the cluster.             |
    And the sidecar "library/Ibryam/Kubernetes Patterns/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000001", "title": "Kubernetes Patterns", "subtitle": "Reusable Elements", "authors": ["Bilgin Ibryam"], "language": "en", "year": 2019, "description": "Design patterns for containers", "tags": ["cloud/kubernetes", "devops"], "files": [{ "name": "kp.epub" }] }
      """
    And a sample PDF "library/Alander/Event Sourcing in Practice/es.pdf" with the title "Event Sourcing in Practice", the author "Jördis Ålander" and the pages:
      | text                                                                   |
      | Event sourcing stores every change as an event in an append only log.  |
      | A projection builds a read model from the event stream.                |
    And the sidecar "library/Alander/Event Sourcing in Practice/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000002", "title": "Event Sourcing in Practice", "authors": ["Jördis Ålander"], "language": "en", "year": 2021, "files": [{ "name": "es.pdf" }] }
      """
    And a sample EPUB "library/Weiss/Verteilte Systeme/vs.epub" with the title "Verteilte Systeme verstehen", the authors "Lukas Weiß" and the chapters:
      | title   | text                                                          |
      | Konsens | Ein Konsensprotokoll sorgt dafür, dass alle Knoten einig sind. |
    And the sidecar "library/Weiss/Verteilte Systeme/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000003", "title": "Verteilte Systeme verstehen", "authors": ["Lukas Weiß"], "language": "de", "year": 2015, "files": [{ "name": "vs.epub" }] }
      """
    And I have run shelf with arguments "reindex --library library"
    And I have run shelf with arguments "reindex --embeddings --library library"
    And the embedder calls are forgotten
    And the MCP server is running on the library "library"


  Scenario Outline: semantic_search finds a work by the words of its title, description and tags
    When I call the tool "semantic_search" with the arguments:
      """
      { "query": "<query>" }
      """
    Then the tool call succeeds
    And the result has the string "level" equal to "work"
    And the result has the string "mode" equal to "semantic"
    And the result has the number "total" equal to 3
    And the result has the string "results[0].title" equal to "<first>"
    And the result has the property "results[0].passage" set to null

    Examples:
      | query                 | first                       |
      | kubernetes containers | Kubernetes Patterns         |
      | event sourcing        | Event Sourcing in Practice  |
      | verteilte systeme     | Verteilte Systeme verstehen |

  Scenario: semantic_search finds passages with locations that read_passage takes
    Given I call the tool "semantic_search" with the arguments:
      """
      { "query": "projection read model", "level": "chunk", "limit": 1 }
      """
    Then the result has the string "level" equal to "chunk"
    And the result has the string "results[0].title" equal to "Event Sourcing in Practice"
    And the result has the string "results[0].passage.location" equal to "page=2"
    And the result has the string "results[0].passage.where" equal to "page 2"
    And the result has the string "results[0].passage.file" equal to "Alander/Event Sourcing in Practice/es.pdf"
    And the result has the string "results[0].passage.snippet" containing "A projection builds a read model"

  Scenario: The location of a passage can be read with read_passage
    Given I call the tool "semantic_search" with the arguments:
      """
      { "query": "batch job completion", "level": "chunk", "limit": 1 }
      """
    When I read the passage at the location "results[0].passage.location" of the work "results[0].id"
    Then the result has the string "text" containing "A batch job runs to completion"

  Scenario: semantic_search can be combined with full text
    When I call the tool "semantic_search" with the arguments:
      """
      { "query": "kubern*", "hybrid": true }
      """
    Then the result has the string "mode" equal to "hybrid"
    And the result has the string "results[0].title" equal to "Kubernetes Patterns"

  Scenario: The query is embedded as a query
    When I call the tool "semantic_search" with the arguments:
      """
      { "query": "kubernetes containers" }
      """
    Then the embedder embedded exactly one query "kubernetes containers"

  Scenario Outline: The number of hits can be limited
    When I call the tool "semantic_search" with the arguments:
      """
      { "query": "patterns", "level": "<level>", "limit": <limit> }
      """
    Then the result has the number "total" equal to <total>

    Examples:
      | level | limit | total |
      | work  | 1     | 1     |
      | work  | 50    | 3     |
      | chunk | 3     | 3     |
      | chunk | 50    | 5     |

  Scenario Outline: semantic_search refuses what it cannot do
    When I call the tool "semantic_search" with the arguments:
      """
      <arguments>
      """
    Then the tool call fails with the message containing "<message>"

    Examples:
      | arguments                                         | message                                        |
      | { "query": " " }                                  | the query has nothing to search for            |
      | { "query": "x", "limit": 0 }                      | limit must be between 1 and 100                |
      | { "query": "x", "limit": 101 }                    | limit must be between 1 and 100                |
      | { "query": "x", "level": "book" }                 | level must be 'work' or 'chunk'                |
      | { "query": "author:ibryam", "hybrid": true }      | hybrid takes words and phrases only            |
      | { "query": "-azure kubernetes", "hybrid": true }  | hybrid takes words and phrases only            |

  Scenario: A library without embeddings says how to make them
    Given I have run shelf with arguments "reindex --library library"
    And the MCP server is running on the library "library"
    When I call the tool "semantic_search" with the arguments:
      """
      { "query": "kubernetes" }
      """
    Then the tool call succeeds
    And the result has the number "total" equal to 0
    And the result has the string "note" containing "no embeddings yet; run: shelf reindex --embeddings"

  Scenario: Files that wait for their embeddings are reported with the result
    Given a sample EPUB "library/Ibryam/Kubernetes Patterns/kp.epub" with the title "Kubernetes Patterns", the authors "Bilgin Ibryam" and the chapters:
      | title                 | text                                         |
      | Foundational Patterns | Operators extend the cluster with new rules. |
    And I have run shelf with arguments "scan --library library"
    When I call the tool "semantic_search" with the arguments:
      """
      { "query": "operators", "level": "chunk" }
      """
    Then the result has the number "pendingFiles" equal to 1
    And the result has the string "note" containing "1 file still waits for its embeddings"

  Scenario: A complete index has no note
    When I call the tool "semantic_search" with the arguments:
      """
      { "query": "kubernetes" }
      """
    Then the result has the number "pendingFiles" equal to 0
    And the result has the property "note" set to null

  Scenario: Without the model there is no query embedding
    Given I delete the folder "library/.shelf/models"
    And the MCP server is running on the library "library"
    When I call the tool "semantic_search" with the arguments:
      """
      { "query": "kubernetes" }
      """
    Then the tool call fails with the message containing "multilingual-e5-small is not installed; run: shelf model install"

  Scenario: The model is loaded once for many searches
    When I call the tool "semantic_search" with the arguments:
      """
      { "query": "kubernetes" }
      """
    And I call the tool "semantic_search" with the arguments:
      """
      { "query": "event sourcing" }
      """
    Then the embedder was opened 1 time

  Scenario: A search never downloads anything
    Given the download fails with "no network"
    When I call the tool "semantic_search" with the arguments:
      """
      { "query": "kubernetes" }
      """
    Then the tool call succeeds
    And no download was started
