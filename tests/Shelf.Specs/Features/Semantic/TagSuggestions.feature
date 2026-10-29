@F-TAG-6 @M10
Feature: Tag suggestions from similar works
  As a person who tags a growing library
  I want to see which tags the works most like a work carry
  So that I tag in the way I tagged before without remembering every tag


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
      { "schema": 1, "id": "01J9Z3K0000000000000000002", "title": "Event Sourcing in Practice", "authors": ["Jördis Ålander"], "language": "en", "year": 2021, "tags": ["architektur/ddd"], "files": [{ "name": "es.pdf" }] }
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

  Scenario: The tags of similar works are suggested, best first, without the tags the work has
    When I run shelf with arguments "tag suggest 01J9Z3K0000000000000000001 --library library"
    Then the exit code is 0
    And standard output contains "cloud/aws"
    And standard output contains "architektur/ddd"
    And standard output does not contain "devops"
    And standard output does not contain "cloud/kubernetes"
    And "cloud/aws" stands before "architektur/ddd" in standard output

  Scenario: A tag that several similar works carry comes first
    When I run shelf with arguments "tag suggest 01J9Z3K0000000000000000002 --library library --json"
    Then the exit code is 0
    And the JSON of standard output has the string "id" equal to "01J9Z3K0000000000000000002"
    And the JSON of standard output has the string "suggestions[0].tag" equal to "devops"
    And the JSON of standard output has the number "suggestions[0].works" equal to 2

  Scenario: Suggestions are found without the embedding model
    When I run shelf with arguments "tag suggest 01J9Z3K0000000000000000001 --library library"
    Then the embedder embedded no queries

  Scenario Outline: The number of suggestions can be limited
    When I run shelf with arguments "tag suggest 01J9Z3K0000000000000000001 --limit <limit> --library library --json"
    Then the exit code is 0
    And the JSON of standard output has the number "total" equal to <count>

    Examples:
      | limit | count |
      | 1     | 1     |
      | 10    | 2     |

  Scenario Outline: A limit outside 1 to 100 is a usage error
    When I run shelf with arguments "tag suggest 01J9Z3K0000000000000000001 --limit <limit> --library library"
    Then the exit code is 2
    And standard error contains "--limit must be between 1 and 100"

    Examples:
      | limit |
      | 0     |
      | 101   |

  Scenario: Nothing changes unless the suggestions are applied
    Given I take a snapshot of the folder "library"
    When I run shelf with arguments "tag suggest 01J9Z3K0000000000000000001 --library library"
    Then the folder "library" equals the snapshot

  Scenario: The suggestions can be applied as tags and undone
    When I run shelf with arguments "tag suggest 01J9Z3K0000000000000000001 --apply --library library"
    Then the exit code is 0
    And standard output contains "Added 2 tags"
    And the file "library/Ibryam/Kubernetes Patterns/book.json" contains "cloud/aws"
    And the file "library/Ibryam/Kubernetes Patterns/book.json" contains "architektur/ddd"
    When I run shelf with arguments "undo --library library"
    Then the exit code is 0
    And the file "library/Ibryam/Kubernetes Patterns/book.json" does not contain "cloud/aws"

  Scenario: A work without suggestions says so
    Given I run shelf with arguments "tag add cloud/aws 01J9Z3K0000000000000000001 --library library"
    And I run shelf with arguments "tag add architektur/ddd 01J9Z3K0000000000000000001 --library library"
    When I run shelf with arguments "tag suggest 01J9Z3K0000000000000000001 --library library"
    Then the exit code is 0
    And standard output contains "No tag suggestions"

  Scenario: A reference that names no work is an error
    When I run shelf with arguments "tag suggest 01J9Z3K9000000000000000009 --library library"
    Then the exit code is 1
    And standard error starts with "error: no work matches '01J9Z3K9000000000000000009'"

  Scenario: A work that has no vector yet is reported, not guessed
    Given a sample PDF "library/Nobody/Fresh/fresh.pdf" with the title "Fresh Book", the author "N. Body" and the pages:
      | text         |
      | Nothing yet. |
    And the sidecar "library/Nobody/Fresh/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000005", "title": "Fresh Book", "authors": ["N. Body"], "language": "en", "files": [{ "name": "fresh.pdf" }] }
      """
    And I have run shelf with arguments "scan --library library"
    When I run shelf with arguments "tag suggest 01J9Z3K0000000000000000005 --library library"
    Then the exit code is 0
    And standard error contains "has no embedding yet"

  Scenario: A library without embeddings says how to make them
    Given an empty working directory
    And I have run shelf with arguments "init plain"
    And a sample PDF "plain/A/One/one.pdf" with the title "One", the author "A. Author" and the pages:
      | text   |
      | Hello. |
    And the sidecar "plain/A/One/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000006", "title": "One", "authors": ["A. Author"], "language": "en", "files": [{ "name": "one.pdf" }] }
      """
    And I have run shelf with arguments "reindex --library plain"
    When I run shelf with arguments "tag suggest 01J9Z3K0000000000000000006 --library plain"
    Then the exit code is 0
    And standard error contains "shelf reindex --embeddings"
