@F-SEM-8 @M3
Feature: Switching the embedding model
  As a user who wants to try another embedding model
  I want shelf to make all embeddings again when the model changes
  So that vectors of two models are never mixed

  Background:
    Given an empty working directory
    And I have run shelf with arguments "init library"
    And the model catalog "multilingual-e5-small" has the files:
      | file                    | content        |
      | model.onnx              | onnx weights   |
      | sentencepiece.bpe.model | spm vocabulary |
    And the model catalog also has the model "small-v2"
    And the folder "offline" holds the model files
    And I have run shelf with arguments "model install offline --library library"
    And the embedder is a word-hashing fake of 16 dimensions
    And the model "small-v2" embeds with 24 dimensions
    And a sample EPUB "library/Ibryam/Kubernetes Patterns/kp.epub" with the title "Kubernetes Patterns", the authors "Bilgin Ibryam" and the chapters:
      | title                 | text                                              |
      | Foundational Patterns | The sidecar container extends the main container. |
      | Behavioral Patterns   | A batch job runs to completion on the cluster.    |
    And the sidecar "library/Ibryam/Kubernetes Patterns/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000001", "title": "Kubernetes Patterns", "authors": ["Bilgin Ibryam"], "language": "en", "files": [{ "name": "kp.epub" }] }
      """
    And a sample EPUB "library/Weiss/Verteilte Systeme/vs.epub" with the title "Verteilte Systeme verstehen", the authors "Lukas Weiß" and the chapters:
      | title   | text                                                          |
      | Konsens | Ein Konsensprotokoll sorgt dafür, dass alle Knoten einig sind. |
    And the sidecar "library/Weiss/Verteilte Systeme/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000002", "title": "Verteilte Systeme verstehen", "authors": ["Lukas Weiß"], "language": "de", "files": [{ "name": "vs.epub" }] }
      """
    And I have run shelf with arguments "reindex --library library"
    And I have run shelf with arguments "reindex --embeddings --library library"
    And the library "library" uses the model "small-v2"
    And I have run shelf with arguments "model install offline --library library"
    And the embedder calls are forgotten

  Scenario: Another model makes every embedding again, and says so
    When I run shelf with arguments "reindex --embeddings --library library --json"
    Then the exit code is 0
    And standard error contains "the embeddings were made with multilingual-e5-small (16 dimensions); switching to small-v2 makes them all again"
    And the JSON integer property "works" of standard output is 2
    And the JSON integer property "passages" of standard output is 3
    And the JSON integer property "files" of standard output is 2
    And the JSON integer property "remaining" of standard output is 0
    And the index holds 2 work vectors and 3 passage vectors of 24 dimensions

  Scenario: A search after the switch uses the new model
    Given I have run shelf with arguments "reindex --embeddings --library library"
    When I run shelf with arguments "semantic kubernetes patterns --library library"
    Then the exit code is 0
    And the first search result is "Kubernetes Patterns"
    And the embedder embedded exactly one query "kubernetes patterns"

  Scenario: Once switched, the next run has nothing to do and says nothing about models
    Given I have run shelf with arguments "reindex --embeddings --library library"
    And the embedder calls are forgotten
    When I run shelf with arguments "reindex --embeddings --library library"
    Then the exit code is 0
    And standard output contains "Nothing to embed"
    And standard error does not contain "switching"
    And the embedder was called 0 times

  Scenario: A switch that is stopped continues with the new model
    Given the embedder stops the run after 3 embeddings
    And I have tried shelf with arguments "reindex --embeddings --library library"
    And the embedder no longer stops the run
    And the embedder calls are forgotten
    When I run shelf with arguments "reindex --embeddings --library library --json"
    Then the exit code is 0
    And the JSON integer property "remaining" of standard output is 0
    And standard error does not contain "switching"
    And the embedder was called fewer than 5 times
    And the index holds 2 work vectors and 3 passage vectors of 24 dimensions

  Scenario: Going back to the first model makes the embeddings again
    Given I have run shelf with arguments "reindex --embeddings --library library"
    And the library "library" uses the model "multilingual-e5-small"
    When I run shelf with arguments "reindex --embeddings --library library --json"
    Then the exit code is 0
    And standard error contains "switching to multilingual-e5-small"
    And the JSON integer property "passages" of standard output is 3
    And the index holds 2 work vectors and 3 passage vectors of 16 dimensions

  Scenario: A search before the switch refuses to mix the models
    When I run shelf with arguments "semantic kubernetes --library library"
    Then the exit code is 1
    And standard error contains "were made with multilingual-e5-small"
    And standard error contains "shelf reindex --embeddings"

  Scenario: A model that shelf does not know is named
    Given the library "library" uses the model "bge-m3"
    When I run shelf with arguments "reindex --embeddings --library library"
    Then the exit code is 1
    And standard error contains "unknown model 'bge-m3'; known models: multilingual-e5-small, small-v2"
