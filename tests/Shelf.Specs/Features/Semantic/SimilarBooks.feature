@F-SEM-6 @M5
Feature: Finding similar books
  As a reader who liked a book
  I want to see the books of my library that are most like it
  So that I find the next one to read without knowing a word to search for

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
      { "schema": 1, "id": "01J9Z3K0000000000000000002", "title": "Event Sourcing in Practice", "authors": ["Jördis Ålander"], "language": "en", "year": 2021, "files": [{ "name": "es.pdf" }] }
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

  Scenario: The most similar work comes first and the work itself is not in the list
    When I run shelf with arguments "similar 01J9Z3K0000000000000000001 --library library"
    Then the exit code is 0
    And the first search result is "Cloud Patterns"
    And the search results have 3 entries
    And the first search result is not "Kubernetes Patterns"
    And standard output does not contain "Kubernetes Patterns"

  Scenario: Similar works are found without the embedding model
    When I run shelf with arguments "similar 01J9Z3K0000000000000000001 --library library"
    Then the embedder embedded no queries

  Scenario: The result as JSON names the work and gives a score for each similar work
    When I run shelf with arguments "similar 01J9Z3K0000000000000000001 --library library --json"
    Then the exit code is 0
    And the JSON of standard output has the string "id" equal to "01J9Z3K0000000000000000001"
    And the JSON of standard output has the string "title" equal to "Kubernetes Patterns"
    And the JSON of standard output has the number "total" equal to 3
    And the JSON of standard output has the string "results[0].title" equal to "Cloud Patterns"
    And the JSON of standard output has the string "results[0].id" equal to "01J9Z3K0000000000000000004"

  Scenario Outline: The number of works can be limited
    When I run shelf with arguments "similar 01J9Z3K0000000000000000001 --limit <limit> --library library"
    Then the exit code is 0
    And the search results have <count> entries

    Examples:
      | limit | count |
      | 1     | 1     |
      | 2     | 2     |
      | 10    | 3     |

  Scenario Outline: A limit outside 1 to 1000 is a usage error
    When I run shelf with arguments "similar 01J9Z3K0000000000000000001 --limit <limit> --library library"
    Then the exit code is 2
    And standard error contains "--limit must be between 1 and 1000"

    Examples:
      | limit |
      | 0     |
      | 1001  |

  Scenario: The work can be named by the start of its ID or by its folder
    When I run shelf with arguments "similar 01J9Z3K0000000000000000004 --library library --json"
    Then the JSON of standard output has the string "title" equal to "Cloud Patterns"
    When I run shelf with arguments "similar 'library/Mueller/Cloud Patterns' --library library --json"
    Then the JSON of standard output has the string "title" equal to "Cloud Patterns"

  Scenario Outline: A reference that names no work is an error
    When I run shelf with arguments "similar '<reference>' --library library"
    Then the exit code is 1
    And standard error starts with "error: <message>"
    And standard output is empty

    Examples:
      | reference                  | message                                      |
      | 01J9Z3K9000000000000000009 | no work matches '01J9Z3K9000000000000000009' |
      | 01J9Z3K                    | '01J9Z3K' matches 4 works                    |

  Scenario: A work that has no vector yet is reported, not guessed
    Given a sample PDF "library/Nobody/Fresh/fresh.pdf" with the title "Fresh Book", the author "N. Body" and the pages:
      | text           |
      | Nothing yet.   |
    And the sidecar "library/Nobody/Fresh/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000005", "title": "Fresh Book", "authors": ["N. Body"], "language": "en", "files": [{ "name": "fresh.pdf" }] }
      """
    And I have run shelf with arguments "scan --library library"
    When I run shelf with arguments "similar 01J9Z3K0000000000000000005 --library library"
    Then the exit code is 0
    And the search results have 0 entries
    And standard error contains "has no embedding yet"

  Scenario: A library without embeddings says how to make them
    Given an empty working directory
    And I have run shelf with arguments "init plain"
    And a sample PDF "plain/A/One/one.pdf" with the title "One", the author "A. Author" and the pages:
      | text    |
      | Hello.  |
    And the sidecar "plain/A/One/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000006", "title": "One", "authors": ["A. Author"], "language": "en", "files": [{ "name": "one.pdf" }] }
      """
    And I have run shelf with arguments "reindex --library plain"
    When I run shelf with arguments "similar 01J9Z3K0000000000000000006 --library plain"
    Then the exit code is 0
    And the search results have 0 entries
    And standard error contains "shelf reindex --embeddings"
