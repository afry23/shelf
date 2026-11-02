@PRD-7.2 @F-SEM-11 @M18
Feature: Finding passages to answer a question through MCP
  As an AI assistant that answers questions about a person's books
  I want the passages that fit a question, with their text, their work and a citation
  So that I can answer and cite without reading each hit with another call

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

  Scenario: find_passages gives the text of the best passage with its work and place
    When I call the tool "find_passages" with the arguments:
      """
      { "question": "projection read model", "limit": 1 }
      """
    Then the tool call succeeds
    And the result has the string "query" equal to "projection read model"
    And the result has the array "passages" with 1 items
    And the result has the string "passages[0].title" equal to "Event Sourcing in Practice"
    And the result has the string "passages[0].workId" equal to "01J9Z3K0000000000000000002"
    And the result has the string "passages[0].location" equal to "page=2"
    And the result has the string "passages[0].where" equal to "page 2"
    And the result has the string "passages[0].text" containing "A projection builds a read model"
    And the result has the boolean "passages[0].truncated" equal to false

  Scenario: The passages are found by full text and by meaning together
    When I call the tool "find_passages" with the arguments:
      """
      { "question": "batch job completion" }
      """
    Then the result has the string "mode" equal to "hybrid"
    And the result has the string "passages[0].title" equal to "Kubernetes Patterns"
    And the result has the string "passages[0].text" containing "A batch job runs to completion"

  Scenario: A question with a filter still finds passages by meaning
    When I call the tool "find_passages" with the arguments:
      """
      { "question": "tag:devops batch job completion", "limit": 1 }
      """
    Then the tool call succeeds
    And the result has the string "mode" equal to "semantic"
    And the result has the array "passages" with 1 items

  Scenario: A work with a cite key gives a citation with the place
    Given the sidecar "library/Alander/Event Sourcing in Practice/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000002", "title": "Event Sourcing in Practice", "authors": ["Jördis Ålander"], "language": "en", "year": 2021, "citeKey": "alander2021event", "files": [{ "name": "es.pdf" }] }
      """
    And I have run shelf with arguments "reindex --library library"
    When I call the tool "find_passages" with the arguments:
      """
      { "question": "projection read model", "limit": 1 }
      """
    Then the result has the string "passages[0].citeKey" equal to "alander2021event"
    And the result has the string "passages[0].citation" equal to "[@alander2021event, p. 2]"

  Scenario: A work without a cite key has no citation
    When I call the tool "find_passages" with the arguments:
      """
      { "question": "projection read model", "limit": 1 }
      """
    Then the result has the property "passages[0].citeKey" set to null
    And the result has the property "passages[0].citation" set to null

  Scenario: The search can be limited to one work
    When I call the tool "find_passages" with the arguments:
      """
      { "question": "projection read model", "id": "01J9Z3K0000000000000000001" }
      """
    Then the result has the string "passages[0].title" equal to "Kubernetes Patterns"
    And the result has the array "passages" with 2 items

  Scenario: A long passage is cut at maxChars and says so
    When I call the tool "find_passages" with the arguments:
      """
      { "question": "projection read model", "limit": 1, "maxChars": 10 }
      """
    Then the result has the boolean "passages[0].truncated" equal to true

  Scenario Outline: The number of passages is limited
    When I call the tool "find_passages" with the arguments:
      """
      { "question": "patterns events consensus", "limit": <limit> }
      """
    Then the result has the array "passages" with <count> items

    Examples:
      | limit | count |
      | 1     | 1     |
      | 3     | 3     |
      | 20    | 5     |

  Scenario Outline: The tool refuses what it cannot do
    When I call the tool "find_passages" with the arguments:
      """
      <arguments>
      """
    Then the tool call fails with the message containing "<message>"

    Examples:
      | arguments                                                          | message              |
      | { "question": "   " }                                              | nothing to search    |
      | { "question": "x", "limit": 0 }                                    | limit must be        |
      | { "question": "x", "limit": 21 }                                   | limit must be        |
      | { "question": "x", "maxChars": 0 }                                 | maxChars must be     |
      | { "question": "x", "id": "01J9Z3K0000000000000000009" }            | no work              |
