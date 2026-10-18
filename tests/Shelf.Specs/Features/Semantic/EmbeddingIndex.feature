@F-SEM-2 @F-SEM-3 @F-SEM-7 @M3
Feature: Building the embedding index
  As a user with a library
  I want shelf to embed my works and their passages in the background
  So that I can search by meaning, and can stop and continue at any time

  Background:
    Given an empty working directory
    And I have run shelf with arguments "init library"
    And the model catalog "multilingual-e5-small" has the files:
      | file                    | content        |
      | model.onnx              | onnx weights   |
      | sentencepiece.bpe.model | spm vocabulary |
    And the folder "offline" holds the model files
    And I have run shelf with arguments "model install offline --library library"
    And the embedder is a word-hashing fake of 16 dimensions
    And a sample EPUB "library/Ibryam/Kubernetes Patterns/kp.epub" with the title "Kubernetes Patterns", the authors "Bilgin Ibryam" and the chapters:
      | title                 | text                                                       |
      | Foundational Patterns | The sidecar container extends the main container.          |
      | Behavioral Patterns   | A batch job runs to completion on the cluster.             |
    And the sidecar "library/Ibryam/Kubernetes Patterns/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000001", "title": "Kubernetes Patterns", "subtitle": "Reusable Elements", "authors": ["Bilgin Ibryam"], "language": "en", "description": "Design patterns for containers", "tags": ["cloud/kubernetes", "devops"], "files": [{ "name": "kp.epub" }] }
      """
    And a sample EPUB "library/Weiss/Verteilte Systeme/vs.epub" with the title "Verteilte Systeme verstehen", the authors "Lukas Weiß" and the chapters:
      | title   | text                                                          |
      | Konsens | Ein Konsensprotokoll sorgt dafür, dass alle Knoten einig sind. |
    And the sidecar "library/Weiss/Verteilte Systeme/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000002", "title": "Verteilte Systeme verstehen", "authors": ["Lukas Weiß"], "language": "de", "files": [{ "name": "vs.epub" }] }
      """
    And I have run shelf with arguments "reindex --library library"

  Scenario: Works and passages are embedded
    When I run shelf with arguments "reindex --embeddings --library library"
    Then the exit code is 0
    And standard output contains "Embedded 2 works and 3 passages from 2 files"
    And standard error contains "files 2/2"

  Scenario: JSON output counts what was done
    When I run shelf with arguments "reindex --embeddings --library library --json"
    Then the exit code is 0
    And the JSON integer property "works" of standard output is 2
    And the JSON integer property "passages" of standard output is 3
    And the JSON integer property "files" of standard output is 2
    And the JSON integer property "remaining" of standard output is 0

  Scenario: A work is embedded from its title, subtitle, description and tags
    When I run shelf with arguments "reindex --embeddings --library library"
    Then the embedder was given the text containing "Kubernetes Patterns"
    And the embedder was given the text containing "Reusable Elements"
    And the embedder was given the text containing "Design patterns for containers"
    And the embedder was given the text containing "cloud/kubernetes"

  Scenario: Works and passages are embedded as passages, never as queries
    When I run shelf with arguments "reindex --embeddings --library library"
    Then the embedder embedded no queries

  Scenario: Running again with nothing new does no work
    Given I have run shelf with arguments "reindex --embeddings --library library"
    And the embedder calls are forgotten
    When I run shelf with arguments "reindex --embeddings --library library"
    Then the exit code is 0
    And standard output contains "Nothing to embed"
    And the embedder was called 0 times

  Scenario: A stopped run continues where it left off
    Given the embedder stops the run after 3 embeddings
    And I have tried shelf with arguments "reindex --embeddings --library library"
    And the embedder calls are forgotten
    And the embedder no longer stops the run
    When I run shelf with arguments "reindex --embeddings --library library --json"
    Then the exit code is 0
    And the JSON integer property "remaining" of standard output is 0
    And the embedder was called fewer than 5 times

  Scenario: A stopped run says so and exits with an error
    Given the embedder stops the run after 3 embeddings
    When I run shelf with arguments "reindex --embeddings --library library"
    Then the exit code is 1
    And standard error contains "interrupted"
    And standard error contains "run the same command again to continue"

  Scenario: A changed file is embedded again
    Given I have run shelf with arguments "reindex --embeddings --library library"
    And a sample EPUB "library/Weiss/Verteilte Systeme/vs.epub" with the title "Verteilte Systeme verstehen", the authors "Lukas Weiß" and the chapters:
      | title   | text                                                 |
      | Konsens | Paxos und Raft sind Konsensprotokolle für Cluster.   |
    And I have run shelf with arguments "scan --library library"
    And the embedder calls are forgotten
    When I run shelf with arguments "reindex --embeddings --library library --json"
    Then the JSON integer property "files" of standard output is 1
    And the JSON integer property "passages" of standard output is 1
    And the JSON integer property "works" of standard output is 0
    And the embedder was given the text containing "Paxos und Raft"
    And the embedder was not given the text containing "sidecar container"

  Scenario: A changed description embeds the work again
    Given I have run shelf with arguments "reindex --embeddings --library library"
    And the embedder calls are forgotten
    And the sidecar "library/Weiss/Verteilte Systeme/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000002", "title": "Verteilte Systeme verstehen", "authors": ["Lukas Weiß"], "language": "de", "description": "Konsens und Replikation", "files": [{ "name": "vs.epub" }] }
      """
    And I have run shelf with arguments "scan --library library"
    When I run shelf with arguments "reindex --embeddings --library library --json"
    Then the JSON integer property "works" of standard output is 1
    And the JSON integer property "passages" of standard output is 0
    And the embedder was given the text containing "Konsens und Replikation"

  Scenario: A removed work leaves no vectors behind
    Given I have run shelf with arguments "reindex --embeddings --library library"
    And I delete the folder "library/Weiss"
    And I have run shelf with arguments "scan --library library"
    When I run shelf with arguments "reindex --embeddings --library library"
    Then the exit code is 0
    And the index holds 1 work vector and 2 passage vectors

  Scenario Outline: A long passage is embedded in overlapping windows
    Given a sample EPUB "library/Long/Long Book/long.epub" with the title "Long Book" and one chapter of <words> words
    And I have run shelf with arguments "scan --library library"
    When I run shelf with arguments "reindex --embeddings --library library"
    Then the index holds 3 work vectors and <total> passage vectors

    Examples:
      | words | total |
      | 100   | 4     |
      | 280   | 4     |
      | 281   | 5     |
      | 520   | 5     |
      | 521   | 6     |
      | 600   | 6     |

  Scenario: A rebuild of the index removes the embeddings, --embeddings makes them again
    Given I have run shelf with arguments "reindex --embeddings --library library"
    And I have run shelf with arguments "reindex --library library"
    Then the index holds 0 work vectors and 0 passage vectors
    When I run shelf with arguments "reindex --embeddings --library library"
    Then the index holds 2 work vectors and 3 passage vectors

  Scenario: The model is downloaded first when it is missing
    Given I delete the folder "library/.shelf/models"
    And the download serves the model files
    When I run shelf with arguments "reindex --embeddings --library library"
    Then the exit code is 0
    And standard error contains "downloading model.onnx"
    And standard output contains "Embedded 2 works and 3 passages"

  Scenario: Without the model and without a network nothing is touched
    Given I delete the folder "library/.shelf/models"
    And the download fails with "connection refused"
    When I run shelf with arguments "reindex --embeddings --library library"
    Then the exit code is 1
    And standard error contains "download failed: connection refused"
    And standard error contains "shelf model install <folder>"

  Scenario: Files without text are left out
    Given a work folder "library/Scan/Scanned" with the fixture files "scanned-page.pdf=derived/scanned-page.pdf" and the sidecar:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000003", "title": "Scanned", "authors": ["Nobody"], "files": [{ "name": "scanned-page.pdf" }] }
      """
    And I have run shelf with arguments "scan --library library"
    When I run shelf with arguments "reindex --embeddings --library library --json"
    Then the JSON integer property "files" of standard output is 2
    And the JSON integer property "works" of standard output is 3
    And the JSON integer property "remaining" of standard output is 0

  Scenario: Metadata-only and embeddings cannot be combined
    When I run shelf with arguments "reindex --embeddings --metadata-only --library library"
    Then the exit code is 2
    And standard error contains "--embeddings"
