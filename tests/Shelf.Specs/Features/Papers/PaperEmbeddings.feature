@F-SEM-3 @F-PAP-7 @M9
Feature: The abstract of a paper stands for the paper in the search by meaning
  As a researcher who searches the library by what a paper is about
  I want the abstract to be part of the embedding of the whole paper
  So that I find a paper by its content even if its title does not say it

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
    And a sample PDF "library/Roe/A Note on Quantities/note.pdf" with the title "A Note on Quantities", the author "Jane Roe" and the pages:
      | text                                         |
      | Nothing about the topic is said in this text. |
    And the sidecar "library/Roe/A Note on Quantities/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000001", "kind": "paper", "title": "A Note on Quantities", "authors": ["Jane Roe"], "language": "en", "year": 2020, "abstract": "We prove convergence of stochastic gradient descent for nonconvex objectives.", "files": [{ "name": "note.pdf" }] }
      """
    And a sample PDF "library/Doe/Another Short Note/other.pdf" with the title "Another Short Note", the author "John Doe" and the pages:
      | text                                         |
      | Nothing about the topic is said in this text. |
    And the sidecar "library/Doe/Another Short Note/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000002", "kind": "paper", "title": "Another Short Note", "authors": ["John Doe"], "language": "en", "year": 2021, "abstract": "A survey of sourdough fermentation in home kitchens.", "files": [{ "name": "other.pdf" }] }
      """
    And I have run shelf with arguments "reindex --library library"
    And I have run shelf with arguments "reindex --embeddings --library library"
    And the embedder calls are forgotten

  Scenario Outline: A paper is found by the words of its abstract, which neither its title nor its text contain
    When I run shelf with arguments "semantic <query> --library library --json"
    Then the exit code is 0
    And the JSON of standard output has the string "results[0].title" equal to "<first>"
    And the JSON score "results[0].score" is at least 0.38

    Examples:
      | query                                   | first                |
      | stochastic gradient descent             | A Note on Quantities |
      | sourdough fermentation kitchens         | Another Short Note   |

  Scenario: The embedding of the paper holds its title and its abstract
    Given the sidecar "library/Roe/A Note on Quantities/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000001", "kind": "paper", "title": "A Note on Quantities", "authors": ["Jane Roe"], "language": "en", "year": 2020, "abstract": "Convergence of mirror descent.", "description": "Short communication", "files": [{ "name": "note.pdf" }] }
      """
    And I have run shelf with arguments "scan --library library"
    When I run shelf with arguments "reindex --embeddings --library library --json"
    Then the JSON integer property "works" of standard output is 1
    And the embedder was given the text containing "A Note on Quantities"
    And the embedder was given the text containing "Short communication"
    And the embedder was given the text containing "Convergence of mirror descent."

  Scenario: A changed abstract embeds only that paper again
    Given the sidecar "library/Roe/A Note on Quantities/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000001", "kind": "paper", "title": "A Note on Quantities", "authors": ["Jane Roe"], "language": "en", "year": 2020, "abstract": "We prove convergence of mirror descent.", "files": [{ "name": "note.pdf" }] }
      """
    And I have run shelf with arguments "scan --library library"
    When I run shelf with arguments "reindex --embeddings --library library --json"
    Then the JSON integer property "works" of standard output is 1
    And the JSON integer property "passages" of standard output is 0
    And the embedder was given the text containing "mirror descent"
    And the embedder was not given the text containing "sourdough"

  Scenario: Papers without a change are not embedded again
    When I run shelf with arguments "reindex --embeddings --library library --json"
    Then the JSON integer property "works" of standard output is 0
    And the embedder was called 0 times

  Scenario: The abstract that enrich fills in is embedded
    Given the online source "arxiv" holds the paper:
      | field    | value                                          |
      | arxiv    | 1706.03762                                     |
      | title    | Another Short Note                             |
      | authors  | John Doe                                       |
      | abstract | Attention without recurrence for translation.  |
    And the sidecar "library/Doe/Another Short Note/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000002", "kind": "paper", "title": "Another Short Note", "authors": ["John Doe"], "language": "en", "year": 2021, "files": [{ "name": "other.pdf" }] }
      """
    And I have run shelf with arguments "scan --library library"
    And I have run shelf with arguments "reindex --embeddings --library library"
    And the embedder calls are forgotten
    When I run shelf with arguments "enrich 'library/Doe/Another Short Note' --apply --library library"
    And I run shelf with arguments "reindex --embeddings --library library --json"
    Then the JSON integer property "works" of standard output is 1
    And the embedder was given the text containing "Attention without recurrence"
