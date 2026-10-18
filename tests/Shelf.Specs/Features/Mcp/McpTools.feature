@PRD-7.2 @M3
Feature: Looking into the library through MCP tools
  As an AI assistant
  I want to search the library, read about a work and read passages
  So that I can answer questions about the books

  Background:
    Given an empty working directory
    And I have run shelf with arguments "init library"
    And a sample EPUB "library/Ibryam/Kubernetes Patterns/kp.epub" with the title "Kubernetes Patterns", the authors "Bilgin Ibryam" and the chapters:
      | title                 | text                                                                                       |
      | Foundational Patterns | The sidecar container extends the main container. Health probes tell the platform when a pod is alive. |
      | Behavioral Patterns   | A batch job runs to completion. A daemon service runs on every node of the cluster.        |
    And the sidecar "library/Ibryam/Kubernetes Patterns/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000001", "title": "Kubernetes Patterns", "subtitle": "Reusable Elements", "authors": ["Bilgin Ibryam"], "language": "en", "year": 2019, "description": "Design patterns for containers", "tags": ["cloud/kubernetes", "devops"], "readStatus": "read", "files": [{ "name": "kp.epub" }] }
      """
    And a sample PDF "library/Alander/Event Sourcing in Practice/es.pdf" with the title "Event Sourcing in Practice", the author "Jördis Ålander" and the pages:
      | text                                                                                          |
      | Event sourcing stores every change as an event. Aggregates replay events to rebuild their state. |
      | A projection builds a read model from the event stream. Kubernetes is only mentioned once here. |
    And the sidecar "library/Alander/Event Sourcing in Practice/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000002", "title": "Event Sourcing in Practice", "authors": ["Jördis Ålander"], "language": "en", "year": 2021, "tags": ["architektur/ddd", "cloud/azure"], "files": [{ "name": "es.pdf" }] }
      """
    And a sample EPUB "library/Weiss/Verteilte Systeme/vs.epub" with the title "Verteilte Systeme verstehen", the authors "Lukas Weiß" and the chapters:
      | title   | text                                                                      |
      | Konsens | Ein Konsensprotokoll sorgt dafür, dass alle Knoten denselben Zustand sehen. |
    And the sidecar "library/Weiss/Verteilte Systeme/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000003", "title": "Verteilte Systeme verstehen", "authors": ["Lukas Weiß"], "language": "de", "year": 2015, "tags": ["architektur/verteilte-systeme", "architektur/ddd"], "files": [{ "name": "vs.epub" }] }
      """
    And I have run shelf with arguments "reindex --library library"
    And the MCP server is running on the library "library"

  Scenario Outline: search_books finds works with the query language of the CLI
    When I call the tool "search_books" with the arguments:
      """
      { "query": "<query>" }
      """
    Then the tool call succeeds
    And the result has the number "total" equal to <total>
    And the result has the string "results[0].title" equal to "<first>"

    Examples:
      | query                       | total | first                       |
      | kubernetes tag:devops       | 1     | Kubernetes Patterns         |
      | tag:cloud year:>=2020       | 1     | Event Sourcing in Practice  |
      | lang:de                     | 1     | Verteilte Systeme verstehen |
      | \"read model\"              | 1     | Event Sourcing in Practice  |
      | sidecar -azure              | 1     | Kubernetes Patterns         |

  Scenario: search_books finds every work that has the word
    When I call the tool "search_books" with the arguments:
      """
      { "query": "kubernetes" }
      """
    Then the result has the number "total" equal to 2

  Scenario: search_books names the places in the text that read_passage can read
    When I call the tool "search_books" with the arguments:
      """
      { "query": "projection" }
      """
    Then the result has the string "results[0].id" equal to "01J9Z3K0000000000000000002"
    And the result has the string "results[0].passages[0].location" equal to "page=2"
    And the result has the string "results[0].passages[0].where" equal to "page 2"
    And the result has the string "results[0].passages[0].file" equal to "Alander/Event Sourcing in Practice/es.pdf"
    And the result has the string "results[0].passages[0].snippet" containing "[projection]"

  Scenario: search_books limits the number of works
    When I call the tool "search_books" with the arguments:
      """
      { "query": "kubernetes", "limit": 1 }
      """
    Then the result has the number "total" equal to 1

  Scenario Outline: search_books explains what is wrong with the query
    When I call the tool "search_books" with the arguments:
      """
      { "query": "<query>"<more> }
      """
    Then the tool call fails with the message containing "<message>"

    Examples:
      | query        | more            | message                                    |
      | year:abc     |                 | year: 'abc' is not a year                  |
      | ...          |                 | the query has nothing to search for        |
      | kubernetes   | , "limit": 0    | limit must be between 1 and 100            |
      | kubernetes   | , "limit": 101  | limit must be between 1 and 100            |

  Scenario: get_book gives the metadata, the files and the tags
    When I call the tool "get_book" with the arguments:
      """
      { "id": "01J9Z3K0000000000000000001" }
      """
    Then the result has the string "title" equal to "Kubernetes Patterns"
    And the result has the string "subtitle" equal to "Reusable Elements"
    And the result has the string "authors[0]" equal to "Bilgin Ibryam"
    And the result has the number "year" equal to 2019
    And the result has the string "language" equal to "en"
    And the result has the string "description" equal to "Design patterns for containers"
    And the result has the string "readStatus" equal to "read"
    And the result has the array "tags" with 2 items
    And the result has the string "files[0].name" equal to "kp.epub"
    And the result has the string "files[0].format" equal to "epub"
    And the result has the property "files[0].pages" set to null

  Scenario: get_book gives the page count of a PDF
    When I call the tool "get_book" with the arguments:
      """
      { "id": "01J9Z3K0000000000000000002" }
      """
    Then the result has the number "files[0].pages" equal to 2
    And the result has the property "subtitle" set to null
    And the result has the array "toc" with 0 items

  Scenario: get_book lists the chapters with locations for read_passage
    When I call the tool "get_book" with the arguments:
      """
      { "id": "01J9Z3K0000000000000000001" }
      """
    Then the result has the array "toc" with 3 items
    And the result has the string "toc[0].title" equal to "Contents"
    And the result has the number "toc[0].level" equal to 1
    And the result has the property "toc[0].location" set to null
    And the result has the string "toc[1].title" equal to "Foundational Patterns"
    And the result has the number "toc[1].level" equal to 2
    And the result has the string "toc[1].location" containing "epubcfi("

  Scenario: get_book takes the ID of a work
    When I call the tool "get_book" with the arguments:
      """
      { "id": "01J9Z3K0000000000000000003" }
      """
    Then the result has the string "id" equal to "01J9Z3K0000000000000000003"

  Scenario: get_book says when the start of an ID names several works
    When I call the tool "get_book" with the arguments:
      """
      { "id": "01J9Z3K00000000000000000" }
      """
    Then the tool call fails with the message containing "matches 3 works; use more of the ID"

  Scenario Outline: get_book and read_passage say when there is no such work
    When I call the tool "<tool>" with the arguments:
      """
      <arguments>
      """
    Then the tool call fails with the message containing "<message>"

    Examples:
      | tool         | arguments                                   | message |
      | get_book     | { "id": "01ZZZZZZZZZZZZZZZZZZZZZZZZ" }      | work    |
      | read_passage | { "id": "01ZZZZZZZZZZZZZZZZZZZZZZZZ", "location": "page=1" } | work |

  Scenario: read_passage returns the text at a location from the results
    When I call the tool "read_passage" with the arguments:
      """
      { "id": "01J9Z3K0000000000000000002", "location": "page=2" }
      """
    Then the result has the string "text" equal to "A projection builds a read model from the event stream. Kubernetes is only mentioned once here."
    And the result has the string "file" equal to "Alander/Event Sourcing in Practice/es.pdf"
    And the result has the string "where" equal to "page 2"
    And the result has the number "offset" equal to 0
    And the result has the number "totalChars" equal to 95
    And the result has the boolean "truncated" equal to false

  Scenario: read_passage takes a page number for a PDF
    When I call the tool "read_passage" with the arguments:
      """
      { "id": "01J9Z3K0000000000000000002", "location": "1" }
      """
    Then the result has the string "location" equal to "page=1"
    And the result has the string "text" containing "Event sourcing stores every change"

  Scenario: read_passage reads the chapter that the table of contents names
    Given I call the tool "get_book" with the arguments:
      """
      { "id": "01J9Z3K0000000000000000001" }
      """
    When I read the passage at the location "toc[2].location" of that book
    Then the result has the string "text" containing "A batch job runs to completion"

  Scenario Outline: read_passage cuts a long text and reads on with offset
    When I call the tool "read_passage" with the arguments:
      """
      { "id": "01J9Z3K0000000000000000002", "location": "page=2", "maxChars": <max>, "offset": <offset> }
      """
    Then the result has the string "text" equal to "<text>"
    And the result has the boolean "truncated" equal to <truncated>
    And the result has the number "offset" equal to <offset>

    Examples:
      | max | offset | text                    | truncated |
      | 10  | 0      | A projecti              | true      |
      | 6   | 13     | builds                 | true      |
      | 500 | 90     | here.                  | false     |
      | 95  | 0      | A projection builds a read model from the event stream. Kubernetes is only mentioned once here. | false |

  Scenario Outline: read_passage refuses what it cannot or must not do
    When I call the tool "read_passage" with the arguments:
      """
      { "id": "01J9Z3K0000000000000000002", "location": "<location>"<more> }
      """
    Then the tool call fails with the message containing "<message>"

    Examples:
      | location | more                  | message                                  |
      | page=99  |                       | no text at 'page=99'                     |
      | nonsense |                       | no text at 'nonsense'                    |
      | page=2   | , "maxChars": 0       | maxChars must be between 1 and 20000     |
      | page=2   | , "maxChars": 20001   | maxChars must be between 1 and 20000     |
      | page=2   | , "offset": -1        | offset must not be negative              |
      | page=2   | , "offset": 95        | beyond the end of the text (95 characters) |
      | page=2   | , "file": "other.pdf" | no text at 'page=2'                      |

  Scenario: The hard maximum of read_passage can be set in the config
    Given the config file of the library "library" contains the line "mcp.max_chars = 50"
    And the MCP server is running on the library "library"
    When I call the tool "read_passage" with the arguments:
      """
      { "id": "01J9Z3K0000000000000000002", "location": "page=2", "maxChars": 51 }
      """
    Then the tool call fails with the message containing "maxChars must be between 1 and 50"
    When I call the tool "read_passage" with the arguments:
      """
      { "id": "01J9Z3K0000000000000000002", "location": "page=2" }
      """
    Then the result has the string "text" containing "A projection builds a read model"
    And the result has the boolean "truncated" equal to true
    And the result has the number "totalChars" equal to 95

  Scenario: list_tags gives the tree with the number of works under each tag
    When I call the tool "list_tags" with the arguments:
      """
      {}
      """
    Then the result has the number "tags" equal to 5
    And the result has the string "roots[0].name" equal to "architektur"
    And the result has the number "roots[0].works" equal to 2
    And the result has the number "roots[0].direct" equal to 0
    And the result has the string "roots[0].children[0].path" equal to "architektur/ddd"
    And the result has the number "roots[0].children[0].works" equal to 2
    And the result has the number "roots[0].children[0].direct" equal to 2
    And the result has the string "roots[1].name" equal to "cloud"
    And the result has the number "roots[1].works" equal to 2
    And the result has the string "roots[2].name" equal to "devops"
    And the result has the array "roots[2].children" with 0 items

  Scenario: find_duplicates finds files that are the same and changes nothing
    Given the file "library/Ibryam/Kubernetes Patterns/kp.epub" is copied to "library/Ibryam/Kubernetes Patterns/kp (1).epub"
    And I have run shelf with arguments "scan --library library"
    And I take a snapshot of the folder "library"
    When I call the tool "find_duplicates" with the arguments:
      """
      {}
      """
    Then the result has the number "total" equal to 1
    And the result has the string "sets[0].kind" equal to "identical"
    And the result has the string "sets[0].format" equal to "epub"
    And the result has the array "sets[0].files" with 2 items
    And the folder "library" equals the snapshot

  Scenario: find_duplicates without duplicates is empty
    When I call the tool "find_duplicates" with the arguments:
      """
      {}
      """
    Then the result has the number "total" equal to 0

  Scenario: propose_organization shows the moves and moves nothing
    Given I take a snapshot of the folder "library"
    When I call the tool "propose_organization" with the arguments:
      """
      { "pattern": "{author_sort}/{title}.{ext}" }
      """
    Then the result has the string "pattern" equal to "{author_sort}/{title}.{ext}"
    And the result has the number "files" equal to 3
    And the result has the boolean "truncated" equal to false
    And the result has the array "moves" with 3 items
    And the result has the string "moves[0].from" containing "/"
    And the folder "library" equals the snapshot

  Scenario Outline: propose_organization rejects a pattern that cannot work
    When I call the tool "propose_organization" with the arguments:
      """
      { "pattern": "<pattern>" }
      """
    Then the tool call fails with the message containing "pattern:"

    Examples:
      | pattern        |
      | {nope}.{ext}   |
      | {title}        |
