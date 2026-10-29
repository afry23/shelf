@F-TAG-6 @PRD-8.2 @M10
Feature: Tag suggestions over HTTP
  As the work view of the web interface
  I want the tags that the works most like the one on the screen carry
  So that a person can take them over with a click


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
    And the web server is running on the library "library"

  Scenario: The suggested tags of a work, best first, without the tags it has
    When I request "GET /api/v1/works/01J9Z3K0000000000000000001/tag-suggestions"
    Then the response status is 200
    And the JSON field "id" of the response is "01J9Z3K0000000000000000001"
    And the JSON field "total" of the response is 2
    And the JSON field "suggestions[0].tag" of the response is "cloud/aws"
    And the JSON field "suggestions[1].tag" of the response is "architektur/ddd"
    And the JSON field "suggestions[0].works" of the response is 1

  Scenario: The limit is a query parameter
    When I request "GET /api/v1/works/01J9Z3K0000000000000000001/tag-suggestions?limit=1"
    Then the response status is 200
    And the JSON field "total" of the response is 1

  Scenario: A request changes nothing
    Given I take a snapshot of the folder "library"
    When I request "GET /api/v1/works/01J9Z3K0000000000000000001/tag-suggestions"
    Then the folder "library" equals the snapshot

  Scenario Outline: A wrong request is a problem document
    When I request "GET <path>"
    Then the response status is <status>
    And the response header "Content-Type" starts with "application/problem+json"

    Examples:
      | path                                                                  | status |
      | /api/v1/works/01J9Z3K9000000000000000009/tag-suggestions              | 404    |
      | /api/v1/works/01J9Z3K0000000000000000001/tag-suggestions?limit=0      | 400    |
      | /api/v1/works/01J9Z3K0000000000000000001/tag-suggestions?limit=abc    | 400    |
      | /api/v1/works/01J9Z3K0000000000000000001/tag-suggestions?limit=101    | 400    |
