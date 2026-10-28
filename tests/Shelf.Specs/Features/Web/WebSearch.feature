@F-SRC-1 @F-SRC-5 @F-SRC-7 @F-SEM-1 @F-SEM-5 @F-SEM-10 @PRD-8.2 @M4
Feature: Searching over HTTP
  As the search view of the web interface
  I want one search for words, for meaning and for both
  So that a hit shows where in a book it is and the reader can open it there

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
      | Behavioral Patterns   | A batch job runs to completion on the cluster.    |
    And the sidecar "library/Ibryam/Kubernetes Patterns/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000001", "title": "Kubernetes Patterns", "subtitle": "Reusable Elements", "authors": ["Bilgin Ibryam"], "language": "en", "year": 2019, "description": "Design patterns for containers", "tags": ["cloud/kubernetes", "devops"], "files": [{ "name": "kp.epub" }] }
      """
    And a sample PDF "library/Alander/Event Sourcing in Practice/es.pdf" with the title "Event Sourcing in Practice", the author "Jördis Ålander" and the pages:
      | text                                                                  |
      | Event sourcing stores every change as an event in an append only log. |
      | A projection builds a read model from the event stream.               |
    And the sidecar "library/Alander/Event Sourcing in Practice/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000002", "title": "Event Sourcing in Practice", "authors": ["Jördis Ålander"], "language": "en", "year": 2021, "files": [{ "name": "es.pdf" }] }
      """
    And a sample EPUB "library/Weiss/Verteilte Systeme/vs.epub" with the title "Verteilte Systeme verstehen", the authors "Lukas Weiß" and the chapters:
      | title   | text                                                            |
      | Konsens | Ein Konsensprotokoll sorgt dafür, dass alle Knoten einig sind.  |
    And the sidecar "library/Weiss/Verteilte Systeme/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000003", "title": "Verteilte Systeme verstehen", "authors": ["Lukas Weiß"], "language": "de", "year": 2015, "files": [{ "name": "vs.epub" }] }
      """
    And I have run shelf with arguments "reindex --library library"
    And I have run shelf with arguments "reindex --embeddings --library library"
    And the web server is running on the library "library"

  Scenario: A full-text search finds works and the places in their text
    When I request "GET /api/v1/search?q=sidecar"
    Then the response status is 200
    And the JSON field "mode" of the response is "fulltext"
    And the JSON field "total" of the response is 1
    And the JSON field "results[0].id" of the response is "01J9Z3K0000000000000000001"
    And the JSON field "results[0].title" of the response is "Kubernetes Patterns"
    And the JSON field "results[0].passages[0].format" of the response is "epub"
    And the JSON field "results[0].passages[0].fileId" of the response is "01J9Z3K0000000000000000001~kp.epub"
    And the JSON field "results[0].passages[0].location" of the response is not empty
    And the JSON field "results[0].passages[0].snippet" of the response contains "[sidecar]"

  Scenario: A full-text search takes the filters of the query language
    When I request "GET /api/v1/search?q=event+lang%3Aen+year%3A%3E%3D2020"
    Then the JSON field "total" of the response is 1
    And the JSON field "results[0].title" of the response is "Event Sourcing in Practice"
    And the JSON field "results[0].passages[0].where" of the response is "page 1"

  Scenario Outline: A full-text search can be limited
    When I request "GET /api/v1/search?q=the&limit=<limit>"
    Then the JSON array "results" of the response has <count> entries

    Examples:
      | limit | count |
      | 1     | 1     |
      | 5     | 2     |

  Scenario Outline: A semantic search finds works by what they are about
    When I request "GET /api/v1/search?q=<q>&mode=semantic"
    Then the response status is 200
    And the JSON field "mode" of the response is "semantic"
    And the JSON field "level" of the response is "work"
    And the JSON field "total" of the response is 3
    And the JSON field "results[0].title" of the response is "<first>"
    And the JSON array "results[0].passages" of the response has 0 entries

    Examples:
      | q                     | first                       |
      | kubernetes+containers | Kubernetes Patterns         |
      | event+sourcing        | Event Sourcing in Practice  |
      | verteilte+systeme     | Verteilte Systeme verstehen |

  Scenario: A semantic search at the level of passages gives a place to open
    When I request "GET /api/v1/search?q=projection+read+model&mode=semantic&level=chunk&limit=1"
    Then the JSON field "level" of the response is "chunk"
    And the JSON field "results[0].title" of the response is "Event Sourcing in Practice"
    And the JSON field "results[0].passages[0].location" of the response is "page=2"
    And the JSON field "results[0].passages[0].where" of the response is "page 2"
    And the JSON field "results[0].passages[0].format" of the response is "pdf"

  Scenario: A hybrid search fuses words and meaning
    When I request "GET /api/v1/search?q=sidecar+container&mode=hybrid"
    Then the response status is 200
    And the JSON field "mode" of the response is "hybrid"
    And the JSON field "results[0].title" of the response is "Kubernetes Patterns"

  Scenario: A hybrid search at the level of passages
    When I request "GET /api/v1/search?q=projection&mode=hybrid&level=chunk"
    Then the JSON field "level" of the response is "chunk"
    And the JSON field "results[0].passages[0].location" of the response is not empty

  Scenario: The answer says when the embeddings are not complete
    Given I have run shelf with arguments "reindex --library library"
    When I request "GET /api/v1/search?q=kubernetes&mode=semantic"
    Then the response status is 200
    And the JSON field "total" of the response is 0
    And the JSON field "note" of the response contains "no embeddings yet; run: shelf reindex --embeddings"

  Scenario Outline: A search that cannot be answered is refused with the reason
    When I request "GET /api/v1/search?<parameters>"
    Then the response status is 400
    And the response header "Content-Type" is "application/problem+json"
    And the JSON field "detail" of the response contains "<message>"

    Examples:
      | parameters                          | message                                    |
      |                                     | q is required                              |
      | q=                                  | q is required                              |
      | q=%20%20                            | nothing to search for                      |
      | q=event&mode=magic                  | mode must be fulltext, semantic or hybrid  |
      | q=event&mode=semantic&level=book    | level must be 'work' or 'chunk'            |
      | q=event&limit=0                     | limit must be between 1 and 100            |
      | q=event&limit=101                   | limit must be between 1 and 100            |
      | q=event&limit=many                  | limit must be between 1 and 100            |
      | q=tag%3Acloud&mode=hybrid           | words and phrases only                     |
      | q=event+tag%3Acloud&mode=hybrid     | words and phrases only                     |
      | q=year%3Aabc                        | year                                       |

  Scenario: A search needs the access token like every call of the API
    Given the web server is running on the library "library" with the arguments "--token s3cret"
    When I request "GET /api/v1/search?q=event"
    Then the response status is 401

  Scenario: At the level of passages a book appears once, with all its passages
    When I request "GET /api/v1/search?q=event+sourcing+projection+read+model&mode=semantic&level=chunk"
    Then the response status is 200
    And the JSON field "level" of the response is "chunk"
    And the JSON field "total" of the response is 3
    And the JSON array "results" of the response has 3 entries
    And the JSON field "results[0].title" of the response is "Event Sourcing in Practice"
    And the JSON array "results[0].passages" of the response has 2 entries

  Scenario: A full-text search can look at titles only
    When I request "GET /api/v1/search?q=sidecar&scope=title"
    Then the response status is 200
    And the JSON field "total" of the response is 0
    When I request "GET /api/v1/search?q=kubernetes&scope=title"
    Then the JSON field "total" of the response is 1
    And the JSON field "results[0].title" of the response is "Kubernetes Patterns"
    And the JSON array "results[0].passages" of the response has 0 entries

  Scenario Outline: A semantic or hybrid search can look at titles only
    When I request "GET /api/v1/search?q=<q>&mode=<mode>&scope=title"
    Then the response status is 200
    And the JSON field "level" of the response is "work"
    And the JSON field "results[0].title" of the response is "<first>"

    Examples:
      | q              | mode     | first                      |
      | event+sourcing | semantic | Event Sourcing in Practice |
      | event+sourcing | hybrid   | Event Sourcing in Practice |

  Scenario Outline: Titles have no passages, and the scope must be known
    When I request "GET /api/v1/search?q=kubernetes&mode=<mode>&<params>"
    Then the response status is 400

    Examples:
      | mode     | params                |
      | semantic | scope=title&level=chunk |
      | hybrid   | scope=title&level=chunk |
      | semantic | scope=authors         |
