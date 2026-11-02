@F-DOC-2 @M21
Feature: The health check and the embeddings
  As a person who uses semantic search
  I want to be told when files have no embeddings yet
  So that the search by meaning does not silently miss books

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
    And a sample PDF "library/Ibryam/Kubernetes Patterns/kp.pdf" with the title "Kubernetes Patterns", the author "Bilgin Ibryam" and the text seed 1
    And the sidecar "library/Ibryam/Kubernetes Patterns/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000001", "title": "Kubernetes Patterns", "authors": ["Bilgin Ibryam"], "language": "en", "year": 2019, "files": [{ "name": "kp.pdf" }] }
      """
    And I have run shelf with arguments "reindex --library library"
    And I have run shelf with arguments "reindex --embeddings --library library"

  Scenario: A library with all embeddings is fine
    When I run shelf with arguments "doctor --library library"
    Then the exit code is 0
    And standard output contains "[ok] embeddings"

  Scenario: A file without embeddings is a warning with the command to run
    Given a sample PDF "library/Weiss/Verteilte Systeme/vs.pdf" with the title "Verteilte Systeme verstehen", the author "Lukas Weiß" and the text seed 3
    And the sidecar "library/Weiss/Verteilte Systeme/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000003", "title": "Verteilte Systeme verstehen", "authors": ["Lukas Weiß"], "language": "de", "year": 2015, "files": [{ "name": "vs.pdf" }] }
      """
    And I have run shelf with arguments "scan --library library"
    When I run shelf with arguments "doctor --library library"
    Then standard output contains "[warn] embeddings"
    And standard output contains "shelf reindex --embeddings"
