@F-ANN-1 @F-ANN-2 @F-ANN-3 @M6
Feature: Highlights and Markdown notes of a file over HTTP
  As the reader of the web interface
  I want to keep highlights and notes for a file
  So that I find what I marked and wrote when I come back to the book

  Background:
    Given an empty working directory
    And I have run shelf with arguments "init library"
    And a sample EPUB "library/Ibryam/Kubernetes Patterns/kp.epub" with the title "Kubernetes Patterns", the authors "Bilgin Ibryam" and the chapters:
      | title                 | text                                              |
      | Foundational Patterns | The sidecar container extends the main container. |
      | Behavioral Patterns   | A batch job runs to completion.                   |
    And the sidecar "library/Ibryam/Kubernetes Patterns/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000001", "title": "Kubernetes Patterns", "authors": ["Bilgin Ibryam"], "language": "en", "year": 2019, "files": [{ "name": "kp.epub" }] }
      """
    And a sample PDF "library/Alander/Event Sourcing in Practice/es.pdf" with the title "Event Sourcing in Practice", the author "Jördis Ålander" and the pages:
      | text                                            |
      | Event sourcing stores every change as an event. |
      | A projection builds a read model.               |
    And the sidecar "library/Alander/Event Sourcing in Practice/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000002", "title": "Event Sourcing in Practice", "authors": ["Jördis Ålander"], "language": "en", "year": 2021, "files": [{ "name": "es.pdf" }] }
      """
    And I have run shelf with arguments "reindex --library library"

  # ---- F-ANN-1 / F-ANN-2: creating ----

  Scenario: A file has no annotations until a reader creates one
    Given the web server is running on the library "library"
    When I request "GET /api/v1/files/01J9Z3K0000000000000000002~es.pdf/annotations"
    Then the response status is 200
    And the JSON array "" of the response has 0 entries

  Scenario Outline: A highlight is stored in annotations.json next to book.json
    Given the web server is running on the library "library"
    When I send "POST /api/v1/files/<file>/annotations" with the JSON:
      """
      { "kind": "highlight", "locator": "<locator>", "quote": { "exact": "<quote>", "prefix": "before ", "suffix": " after" }, "color": "<color>" }
      """
    Then the response status is 201
    And the JSON field "kind" of the response is "highlight"
    And the JSON field "locator" of the response is "<locator>"
    And the JSON field "quote.exact" of the response is "<quote>"
    And the JSON field "color" of the response is "<color>"
    And the JSON field "note" of the response is null
    And the JSON field "id" of the response is not empty
    And the JSON field "createdAt" of the response is not empty
    And the file "library/<folder>/annotations.json" contains "<quote>"
    And the file "library/<folder>/book.json" does not contain "<quote>"
    When I request "GET /api/v1/files/<file>/annotations"
    Then the JSON array "" of the response has 1 entries
    And the JSON field "[0].quote.exact" of the response is "<quote>"

    Examples:
      | file                               | folder                             | locator                      | quote                         | color  |
      | 01J9Z3K0000000000000000001~kp.epub | Ibryam/Kubernetes Patterns         | epubcfi(/6/2!/4/2/1:0,/1:7)  | The sidecar container extends | yellow |
      | 01J9Z3K0000000000000000002~es.pdf  | Alander/Event Sourcing in Practice | page=1                       | stores every change           | green  |

  Scenario: A highlight in a PDF keeps the rectangles that were marked on the page
    Given the web server is running on the library "library"
    When I send "POST /api/v1/files/01J9Z3K0000000000000000002~es.pdf/annotations" with the JSON:
      """
      { "kind": "highlight", "locator": "page=1", "quote": { "exact": "stores every change" }, "rects": [[0.1, 0.2, 0.5, 0.02], [0.1, 0.22, 0.3, 0.02]] }
      """
    Then the response status is 201
    And the JSON array "rects" of the response has 2 entries
    And the JSON field "rects[0][2]" of the response is 0.5
    And the file "library/Alander/Event Sourcing in Practice/annotations.json" contains "0.22"
    When I run shelf with arguments "reindex --library library"
    And I request "GET /api/v1/files/01J9Z3K0000000000000000002~es.pdf/annotations"
    Then the JSON array "[0].rects" of the response has 2 entries

  Scenario: A highlight without rectangles has none
    Given the web server is running on the library "library"
    When I send "POST /api/v1/files/01J9Z3K0000000000000000002~es.pdf/annotations" with the JSON:
      """
      { "kind": "highlight", "locator": "page=1", "quote": { "exact": "stores every change" } }
      """
    Then the JSON field "rects" of the response is null

  Scenario: A highlight without a color is yellow
    Given the web server is running on the library "library"
    When I send "POST /api/v1/files/01J9Z3K0000000000000000002~es.pdf/annotations" with the JSON:
      """
      { "kind": "highlight", "locator": "page=2", "quote": { "exact": "A projection builds a read model." } }
      """
    Then the response status is 201
    And the JSON field "color" of the response is "yellow"

  Scenario: A highlight can carry a note in Markdown
    Given the web server is running on the library "library"
    When I send "POST /api/v1/files/01J9Z3K0000000000000000002~es.pdf/annotations" with the JSON:
      """
      { "kind": "highlight", "locator": "page=1", "quote": { "exact": "every change" }, "note": "# Key idea\n\n* an **event** is a fact" }
      """
    Then the response status is 201
    And the JSON field "note" of the response contains "**event**"
    And the file "library/Alander/Event Sourcing in Practice/annotations.json" contains "Key idea"

  Scenario: A note without a place in the text belongs to the file
    Given the web server is running on the library "library"
    When I send "POST /api/v1/files/01J9Z3K0000000000000000001~kp.epub/annotations" with the JSON:
      """
      { "kind": "note", "note": "Read again before the **workshop**." }
      """
    Then the response status is 201
    And the JSON field "kind" of the response is "note"
    And the JSON field "locator" of the response is null
    And the JSON field "quote" of the response is null
    And the JSON field "color" of the response is null

  Scenario: The annotations of a work list the annotations of all its files
    Given the web server is running on the library "library"
    And I send "POST /api/v1/files/01J9Z3K0000000000000000001~kp.epub/annotations" with the JSON:
      """
      { "kind": "note", "note": "first" }
      """
    And I send "POST /api/v1/files/01J9Z3K0000000000000000002~es.pdf/annotations" with the JSON:
      """
      { "kind": "note", "note": "other work" }
      """
    When I request "GET /api/v1/works/01J9Z3K0000000000000000001/annotations"
    Then the JSON array "" of the response has 1 entries
    And the JSON field "[0].file" of the response is "01J9Z3K0000000000000000001~kp.epub"
    And the JSON field "[0].note" of the response is "first"

  # ---- editing and deleting ----

  Scenario: The note and the color of a highlight can be changed
    Given the web server is running on the library "library"
    And I send "POST /api/v1/files/01J9Z3K0000000000000000002~es.pdf/annotations" with the JSON:
      """
      { "kind": "highlight", "locator": "page=1", "quote": { "exact": "every change" } }
      """
    And I note the JSON field "id" of the response as the entry
    When I send "PATCH /api/v1/files/01J9Z3K0000000000000000002~es.pdf/annotations/{entry}" with the JSON:
      """
      { "note": "An *event* is a fact.", "color": "pink" }
      """
    Then the response status is 200
    And the JSON field "note" of the response is "An *event* is a fact."
    And the JSON field "color" of the response is "pink"
    And the JSON field "quote.exact" of the response is "every change"
    When I request "GET /api/v1/files/01J9Z3K0000000000000000002~es.pdf/annotations"
    Then the JSON field "[0].color" of the response is "pink"

  Scenario: A null note removes the note
    Given the web server is running on the library "library"
    And I send "POST /api/v1/files/01J9Z3K0000000000000000002~es.pdf/annotations" with the JSON:
      """
      { "kind": "highlight", "locator": "page=1", "quote": { "exact": "every change" }, "note": "temporary" }
      """
    And I note the JSON field "id" of the response as the entry
    When I send "PATCH /api/v1/files/01J9Z3K0000000000000000002~es.pdf/annotations/{entry}" with the JSON:
      """
      { "note": null }
      """
    Then the response status is 200
    And the JSON field "note" of the response is null

  Scenario: A deleted annotation is gone from the file
    Given the web server is running on the library "library"
    And I send "POST /api/v1/files/01J9Z3K0000000000000000002~es.pdf/annotations" with the JSON:
      """
      { "kind": "note", "note": "delete me" }
      """
    And I note the JSON field "id" of the response as the entry
    When I send "DELETE /api/v1/files/01J9Z3K0000000000000000002~es.pdf/annotations/{entry}" with the JSON:
      """
      {}
      """
    Then the response status is 204
    When I request "GET /api/v1/files/01J9Z3K0000000000000000002~es.pdf/annotations"
    Then the JSON array "" of the response has 0 entries
    And the file "library/Alander/Event Sourcing in Practice/annotations.json" does not exist

  # ---- refused input ----

  Scenario Outline: Input that is not an annotation of this kind of file is refused and changes nothing
    Given the web server is running on the library "library"
    And I take a snapshot of the folder "library"
    When I send "POST /api/v1/files/<file>/annotations" with the JSON:
      """
      <body>
      """
    Then the response status is 400
    And the JSON field "detail" of the response contains "<reason>"
    And the folder "library" equals the snapshot

    Examples:
      | file                               | body                                                                                         | reason  |
      | 01J9Z3K0000000000000000002~es.pdf  | {}                                                                                           | kind    |
      | 01J9Z3K0000000000000000002~es.pdf  | { "kind": "sticker" }                                                                        | kind    |
      | 01J9Z3K0000000000000000002~es.pdf  | { "kind": "highlight", "quote": { "exact": "x" } }                                           | locator |
      | 01J9Z3K0000000000000000002~es.pdf  | { "kind": "highlight", "locator": "page=0", "quote": { "exact": "x" } }                      | page    |
      | 01J9Z3K0000000000000000001~kp.epub | { "kind": "highlight", "locator": "page=2", "quote": { "exact": "x" } }                      | epubcfi |
      | 01J9Z3K0000000000000000002~es.pdf  | { "kind": "highlight", "locator": "page=1" }                                                 | quote   |
      | 01J9Z3K0000000000000000002~es.pdf  | { "kind": "highlight", "locator": "page=1", "quote": { "exact": "" } }                       | quote   |
      | 01J9Z3K0000000000000000002~es.pdf  | { "kind": "highlight", "locator": "page=1", "quote": { "exact": "x" }, "color": "purple" }    | color   |
      | 01J9Z3K0000000000000000002~es.pdf  | { "kind": "highlight", "locator": "page=1", "quote": { "exact": "x" }, "note": 3 }           | note    |
      | 01J9Z3K0000000000000000002~es.pdf  | { "kind": "highlight", "locator": "page=1", "quote": { "exact": "x" }, "rects": [[0.1, 0.2, 0.5]] }          | rects   |
      | 01J9Z3K0000000000000000002~es.pdf  | { "kind": "highlight", "locator": "page=1", "quote": { "exact": "x" }, "rects": [[0.1, 0.2, 1.5, 0.1]] }     | rects   |
      | 01J9Z3K0000000000000000002~es.pdf  | { "kind": "highlight", "locator": "page=1", "quote": { "exact": "x" }, "rects": "all" }                      | rects   |
      | 01J9Z3K0000000000000000002~es.pdf  | { "kind": "note", "note": "x", "rects": [[0.1, 0.2, 0.5, 0.1]] }                                              | rects   |
      | 01J9Z3K0000000000000000002~es.pdf  | { "kind": "note" }                                                                           | note    |
      | 01J9Z3K0000000000000000002~es.pdf  | { "kind": "note", "note": "  " }                                                             | note    |
      | 01J9Z3K0000000000000000002~es.pdf  | { "kind": "note", "note": "x", "locator": "page=1" }                                         | locator |

  Scenario Outline: A file or annotation that is not in the library has no annotations
    Given the web server is running on the library "library"
    When I send "<method> <path>" with the JSON:
      """
      { "note": "x" }
      """
    Then the response status is 404

    Examples:
      | method | path                                                                                          |
      | POST   | /api/v1/files/01J9Z3K0000000000000000002~gone.pdf/annotations                                 |
      | GET    | /api/v1/files/01J9Z3K0000000000000000009~es.pdf/annotations                                   |
      | GET    | /api/v1/works/01J9Z3K0000000000000000009/annotations                                          |
      | PATCH  | /api/v1/files/01J9Z3K0000000000000000002~es.pdf/annotations/01J9Z3K00000000000000000AA        |
      | DELETE | /api/v1/files/01J9Z3K0000000000000000002~es.pdf/annotations/01J9Z3K00000000000000000AA        |

  # ---- F-ANN-3: the file is the source of truth ----

  Scenario: The annotations survive a reindex and are mirrored in the index
    Given the web server is running on the library "library"
    And I send "POST /api/v1/files/01J9Z3K0000000000000000002~es.pdf/annotations" with the JSON:
      """
      { "kind": "highlight", "locator": "page=1", "quote": { "exact": "every change" }, "note": "a fact" }
      """
    When I run shelf with arguments "reindex --library library"
    Then the table "annotations" of the index has 1 rows
    And the table "annotations_fts" of the index has 1 rows
    When I request "GET /api/v1/files/01J9Z3K0000000000000000002~es.pdf/annotations"
    Then the JSON field "[0].note" of the response is "a fact"

  Scenario: An annotations.json that was written by hand is read by reindex
    Given the sidecar "library/Alander/Event Sourcing in Practice/annotations.json" is replaced by:
      """
      { "schema": 1, "annotations": [{ "id": "01J9Z3K00000000000000000A1", "file": "es.pdf", "kind": "note", "note": "written by hand", "createdAt": "2026-10-25T10:00:00Z", "updatedAt": "2026-10-25T10:00:00Z" }] }
      """
    When I run shelf with arguments "reindex --library library"
    Then the table "annotations" of the index has 1 rows
    When the web server is running on the library "library"
    And I request "GET /api/v1/files/01J9Z3K0000000000000000002~es.pdf/annotations"
    Then the JSON field "[0].note" of the response is "written by hand"

  Scenario: The annotations follow their file when the library is organized
    Given the web server is running on the library "library"
    And I send "POST /api/v1/files/01J9Z3K0000000000000000002~es.pdf/annotations" with the JSON:
      """
      { "kind": "note", "note": "moves along" }
      """
    When I run shelf with arguments "organize --apply --pattern '{author}/{title}.{ext}' --library library"
    Then the exit code is 0
    When I request "GET /api/v1/files/01J9Z3K0000000000000000002~Event Sourcing in Practice.pdf/annotations"
    Then the JSON field "[0].note" of the response is "moves along"
    And the JSON field "[0].file" of the response is "01J9Z3K0000000000000000002~Event Sourcing in Practice.pdf"
    When I run shelf with arguments "reindex --library library"
    Then the table "annotations" of the index has 1 rows

  Scenario: The annotations of a file go with it when it is split off its work
    Given a sample PDF "library/Ibryam/Kubernetes Patterns/kp.pdf" with the title "Kubernetes Patterns", the author "Bilgin Ibryam" and the pages:
      | text                 |
      | The sidecar pattern. |
    And the sidecar "library/Ibryam/Kubernetes Patterns/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000001", "title": "Kubernetes Patterns", "authors": ["Bilgin Ibryam"], "language": "en", "year": 2019, "files": [{ "name": "kp.pdf" }, { "name": "kp.epub" }] }
      """
    And I have run shelf with arguments "reindex --library library"
    And the web server is running on the library "library"
    And I send "POST /api/v1/files/01J9Z3K0000000000000000001~kp.epub/annotations" with the JSON:
      """
      { "kind": "note", "note": "about the epub" }
      """
    When I send "POST /api/v1/files/01J9Z3K0000000000000000001~kp.epub/split" with the JSON:
      """
      {}
      """
    Then the response status is 200
    When I request "GET /api/v1/works/01J9Z3K0000000000000000001/annotations"
    Then the JSON array "" of the response has 0 entries
    When I run shelf with arguments "reindex --library library"
    Then the table "annotations" of the index has 1 rows
