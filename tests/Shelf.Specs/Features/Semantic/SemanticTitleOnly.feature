@F-SEM-10 @M3
Feature: Searching by meaning in titles only
  As a user who looks for a book by what its name says
  I want a semantic search that compares the query with titles and subtitles only
  So that descriptions, tags and the text of the book do not pull in other works

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

  Scenario: The query is compared with the title and the subtitle, not with the description
    When I run shelf with arguments "semantic design containers --title-only --library library --json"
    Then the JSON of standard output has the string "level" equal to "work"
    And the JSON of standard output has the decimal "results[0].score" equal to 0

  Scenario Outline: A work is found by the words of its title and subtitle
    When I run shelf with arguments "semantic <query> --title-only --library library"
    Then the exit code is 0
    And the first search result is "<first>"
    And the search results have 3 entries

    Examples:
      | query                  | first                       |
      | kubernetes patterns    | Kubernetes Patterns         |
      | reusable elements      | Kubernetes Patterns         |
      | event sourcing         | Event Sourcing in Practice  |
      | verteilte systeme      | Verteilte Systeme verstehen |

  Scenario: --title-only works together with --hybrid
    When I run shelf with arguments "semantic event sourcing --title-only --hybrid --library library"
    Then the exit code is 0
    And the first search result is "Event Sourcing in Practice"

  Scenario: Passages have no titles
    When I run shelf with arguments "semantic kubernetes --title-only --chunks --library library"
    Then the exit code is 2
    And standard error contains "--title-only cannot be combined with --chunks"

  Scenario: The query is embedded as a query
    When I run shelf with arguments "semantic kubernetes --title-only --library library"
    Then the embedder embedded exactly one query "kubernetes"

  Scenario: Changing a title embeds the title again, and nothing else
    Given the sidecar "library/Ibryam/Kubernetes Patterns/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000001", "title": "Kubernetes Recipes", "subtitle": "Reusable Elements", "authors": ["Bilgin Ibryam"], "language": "en", "year": 2019, "files": [{ "name": "kp.epub" }] }
      """
    And I have run shelf with arguments "reindex --library library"
    And I have run shelf with arguments "reindex --embeddings --library library"
    When I run shelf with arguments "semantic recipes --title-only --library library"
    Then the first search result is "Kubernetes Recipes"

  Scenario: A library without embeddings says how to make them
    Given I have run shelf with arguments "reindex --library library"
    When I run shelf with arguments "semantic kubernetes --title-only --library library"
    Then the exit code is 0
    And standard output contains "No results."
    And standard error contains "no embeddings yet; run: shelf reindex --embeddings"
