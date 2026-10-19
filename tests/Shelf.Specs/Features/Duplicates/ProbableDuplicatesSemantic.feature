@F-DUP-3 @F-GRP-6 @M5
Feature: Finding probable duplicates by the meaning of the works
  As a user whose library holds the same book in two editions with different texts
  I want shelf dupes --probable to compare the vectors of the works as well
  So that two editions with the same title and description are found although their texts differ

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
    And a sample EPUB "library/a/a.epub" with the title "Kubernetes Patterns", the authors "Bilgin Ibryam" and the text seed 11
    And the sidecar "library/a/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K1000000000000000001", "title": "Kubernetes Patterns", "authors": ["Bilgin Ibryam"], "description": "Design patterns for containers", "tags": ["devops"], "files": [{ "name": "a.epub" }] }
      """
    And a sample EPUB "library/b/b.epub" with the title "Patterns for Kubernetes", the authors "B. Ibryam" and the text seed 12
    And the sidecar "library/b/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K2000000000000000002", "title": "Kubernetes Patterns", "authors": ["B. Ibryam"], "description": "Design patterns for containers", "tags": ["devops"], "files": [{ "name": "b.epub" }] }
      """
    And a sample EPUB "library/c/c.epub" with the title "Kubernetes Patterns Handbook", the authors "B. Ibryam" and the text seed 13
    And the sidecar "library/c/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K3000000000000000003", "title": "Kubernetes Patterns Handbook", "authors": ["B. Ibryam"], "description": "Design patterns for containers in the cloud", "tags": ["devops"], "files": [{ "name": "c.epub" }] }
      """
    And a sample EPUB "library/d/d.epub" with the title "Event Sourcing in Practice", the authors "Jördis Ålander" and the text seed 14
    And the sidecar "library/d/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K4000000000000000004", "title": "Event Sourcing in Practice", "authors": ["Jördis Ålander"], "description": "Append only logs", "tags": ["architecture"], "files": [{ "name": "d.epub" }] }
      """
    And I have run shelf with arguments "reindex --library library"

  Scenario: Works with the same title and description but different texts are found by their vectors
    Given I have run shelf with arguments "reindex --embeddings --library library"
    When I run shelf with arguments "dupes --probable --library library"
    Then the exit code is 0
    And standard output contains "similarity 1.00"
    And standard output contains "1 pair of probable duplicates"
    And standard output contains "[a]"
    And standard output contains "[b]"
    And standard output does not contain "[c]"
    And standard output does not contain "[d]"
    And standard output does not contain "text distance"

  Scenario Outline: The cosine threshold is a setting
    Given I have run shelf with arguments "reindex --embeddings --library library"
    And the global config file contains the line "grouping.probable_cosine = <threshold>"
    When I run shelf with arguments "dupes --probable --library library"
    Then standard output contains "<summary>"

    Examples:
      | threshold | summary                         | what                                                      |
      | 0.95      | 1 pair of probable duplicates   | the same words: similarity 1.00                           |
      | 0.8       | 3 pairs of probable duplicates  | one more word in the description: 0.85 is close enough     |
      | 1.0       | 1 pair of probable duplicates   | exactly the same vectors                                  |
      | 0.05      | 6 pairs of probable duplicates  | everything is close at a threshold this low                |

  Scenario: The JSON output has the similarity
    Given I have run shelf with arguments "reindex --embeddings --library library"
    When I run shelf with arguments "dupes --probable --json --library library"
    Then the JSON property "pairs" of standard output has 1 items
    And the JSON of standard output has the number "pairs[0].cosine" equal to 1

  Scenario: Without embeddings only the texts are compared, and shelf says so
    When I run shelf with arguments "dupes --probable --library library"
    Then the exit code is 0
    And standard output is "No probable duplicates."
    And standard error contains "note: no embeddings yet; run: shelf reindex --embeddings"

  Scenario: A pair that is marked as not a duplicate is not suggested by its vectors either
    Given I have run shelf with arguments "reindex --embeddings --library library"
    And the answers are "n"
    And I have run shelf with arguments "dupes --probable --interactive --library library"
    When I run shelf with arguments "dupes --probable --library library"
    Then standard output is "No probable duplicates."
