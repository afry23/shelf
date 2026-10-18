@F-SEM-5 @M3
Feature: Hybrid search
  As a user who knows some words of a book and some of its meaning
  I want full-text and semantic results combined
  So that an exact word and a similar meaning both count

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

  Scenario: A prefix that only full text understands still finds the work
    When I run shelf with arguments "semantic kubern* --hybrid --library library"
    Then the exit code is 0
    And the first search result is "Kubernetes Patterns"

  Scenario: Semantic search alone does not know that prefix
    When I run shelf with arguments "semantic kubern* --library library"
    Then the exit code is 0
    And the first search result is not "Kubernetes Patterns"

  Scenario: A work found by both searches comes first with the best score
    When I run shelf with arguments "semantic sidecar container --hybrid --library library --json"
    Then the JSON of standard output has the string "results[0].title" equal to "Kubernetes Patterns"
    And the JSON of standard output has the decimal "results[0].score" equal to 1.0
    And the JSON of standard output has the string "mode" equal to "hybrid"

  Scenario: Scores stay between 0 and 1 and fall
    When I run shelf with arguments "semantic patterns --hybrid --library library --json"
    Then the JSON score "results[0].score" is greater than the JSON score "results[1].score"
    And the JSON score "results[1].score" is at least 0

  Scenario: Passages are combined by their place
    When I run shelf with arguments "semantic projection read model --chunks --hybrid --library library --limit 1"
    Then the exit code is 0
    And standard output contains "page 2  es.pdf:"

  Scenario: A passage found by both searches is shown once, each of the five only once
    When I run shelf with arguments "semantic projection read model --chunks --hybrid --library library --json"
    Then the JSON of standard output has the string "results[0].locator" equal to "page=2"
    And the JSON of standard output has the number "total" equal to 5

  Scenario Outline: Hybrid takes words and phrases, not the filter language
    When I run shelf with arguments "semantic <query> --hybrid --library library"
    Then the exit code is 2
    And standard error contains "--hybrid takes words and phrases only"

    Examples:
      | query           |
      | author:ibryam   |
      | kubernetes -azure |
      | year:>2015      |

  Scenario: Without embeddings the full-text results are still shown
    Given I have run shelf with arguments "reindex --library library"
    When I run shelf with arguments "semantic kubernetes --hybrid --library library"
    Then the exit code is 0
    And the first search result is "Kubernetes Patterns"
    And standard error contains "no embeddings yet; run: shelf reindex --embeddings"

  Scenario: Hybrid needs the model like any semantic search
    Given I delete the folder "library/.shelf/models"
    When I run shelf with arguments "semantic kubernetes --hybrid --library library"
    Then the exit code is 1
    And standard error contains "multilingual-e5-small is not installed; run: shelf model install"

  Scenario: A search that is not hybrid says so
    When I run shelf with arguments "semantic kubernetes containers --library library --json"
    Then the JSON of standard output has the string "mode" equal to "semantic"
