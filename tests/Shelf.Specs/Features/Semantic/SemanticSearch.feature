@F-SEM-1 @F-SEM-2 @F-SEM-3 @M3
Feature: Searching by meaning
  As a user with a library
  I want to find works and passages by what they are about
  So that I find a book even when I do not know its words

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

  Scenario Outline: A work is found by the words of its title, description and tags
    When I run shelf with arguments "semantic <query> --library library"
    Then the exit code is 0
    And the first search result is "<first>"
    And the search results have 3 entries

    Examples:
      | query                 | first                       |
      | kubernetes containers | Kubernetes Patterns         |
      | event sourcing        | Event Sourcing in Practice  |
      | verteilte systeme     | Verteilte Systeme verstehen |

  Scenario: Hits carry a score between 0 and 1, best first
    When I run shelf with arguments "semantic kubernetes containers --library library --json"
    Then the JSON of standard output has the string "level" equal to "work"
    And the JSON of standard output has the string "results[0].title" equal to "Kubernetes Patterns"
    And the JSON of standard output has the string "results[0].id" equal to "01J9Z3K0000000000000000001"
    And the JSON of standard output has the number "total" equal to 3
    And the JSON score "results[0].score" is greater than the JSON score "results[1].score"
    And the JSON score "results[2].score" is at least 0

  Scenario: The query is embedded as a query
    When I run shelf with arguments "semantic kubernetes containers --library library"
    Then the embedder embedded exactly one query "kubernetes containers"

  Scenario Outline: Passages come with the place where they are
    When I run shelf with arguments "semantic <query> --chunks --library library --limit 1"
    Then the exit code is 0
    And standard output contains "<place>"
    And standard output contains "<text>"

    Examples:
      | query                  | place                  | text                          |
      | batch job completion   | kp.epub:               | A batch job runs to completion |
      | projection read model  | page 2  es.pdf:        | A projection builds a read model |
      | konsensprotokoll knoten | section 1  vs.epub:    | Ein Konsensprotokoll          |

  Scenario: Passage hits in JSON name the work, the file and the locator
    When I run shelf with arguments "semantic projection read model --chunks --library library --limit 2 --json"
    Then the JSON of standard output has the string "level" equal to "chunk"
    And the JSON of standard output has the string "results[0].title" equal to "Event Sourcing in Practice"
    And the JSON of standard output has the string "results[0].file" equal to "Alander/Event Sourcing in Practice/es.pdf"
    And the JSON of standard output has the string "results[0].format" equal to "pdf"
    And the JSON of standard output has the string "results[0].locator" equal to "page=2"
    And the JSON of standard output has the string "results[0].where" equal to "page 2"
    And the JSON of standard output has the string "results[0].snippet" containing "A projection builds a read model"
    And the JSON of standard output has the number "total" equal to 2

  Scenario Outline: The number of hits can be limited
    When I run shelf with arguments "semantic patterns <level> --library library --limit <limit> --json"
    Then the JSON of standard output has the number "total" equal to <total>

    Examples:
      | level    | limit | total |
      |          | 1     | 1     |
      |          | 2     | 2     |
      |          | 50    | 3     |
      | --chunks | 3     | 3     |
      | --chunks | 50    | 5     |

  Scenario Outline: An invalid limit is a usage error
    When I run shelf with arguments "semantic patterns --library library --limit <limit>"
    Then the exit code is 2
    And standard error contains "--limit must be between 1 and 1000"

    Examples:
      | limit |
      | 0     |
      | 1001  |

  Scenario: Nothing to search for is a usage error
    When I run shelf with arguments "semantic ' ' --library library"
    Then the exit code is 2
    And standard error contains "the query has nothing to search for"

  Scenario: A library without embeddings says how to make them
    Given I have run shelf with arguments "reindex --library library"
    When I run shelf with arguments "semantic kubernetes --library library"
    Then the exit code is 0
    And standard output contains "No results."
    And standard error contains "no embeddings yet; run: shelf reindex --embeddings"

  Scenario: Files that wait for their embeddings are mentioned
    Given a sample EPUB "library/Ibryam/Kubernetes Patterns/kp.epub" with the title "Kubernetes Patterns", the authors "Bilgin Ibryam" and the chapters:
      | title                 | text                                         |
      | Foundational Patterns | Operators extend the cluster with new rules. |
    And I have run shelf with arguments "scan --library library"
    When I run shelf with arguments "semantic operators --chunks --library library"
    Then the exit code is 0
    And standard error contains "1 file still waits for its embeddings; run: shelf reindex --embeddings"
    And standard output does not contain "Operators extend"

  Scenario: A search never downloads anything
    Given the download fails with "no network"
    When I run shelf with arguments "semantic kubernetes --library library"
    Then the exit code is 0
    And no download was started

  Scenario: Without the model there is no query embedding
    Given I delete the folder "library/.shelf/models"
    When I run shelf with arguments "semantic kubernetes --library library"
    Then the exit code is 1
    And standard error contains "multilingual-e5-small is not installed; run: shelf model install"

  Scenario: Vectors of another model are not mixed in
    Given the config file of the library "library" contains the line "semantic.model = 'other-model'"
    And the model catalog "other-model" has the files:
      | file                    | content        |
      | model.onnx              | onnx weights   |
      | sentencepiece.bpe.model | spm vocabulary |
    And I have run shelf with arguments "model install offline --library library"
    When I run shelf with arguments "semantic kubernetes --library library"
    Then the exit code is 1
    And standard error contains "were made with multilingual-e5-small"
