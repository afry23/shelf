@PRD-7.3 @PRD-8.2 @M4
Feature: The library and its works over HTTP
  As the web interface
  I want the list of works with filters, sorting, paging and facet counts, and the details of one work
  So that the library view and the work view can be drawn from the API

  Background:
    Given an empty working directory
    And I have run shelf with arguments "init library"
    And a sample EPUB "library/Ibryam/Kubernetes Patterns/kp.epub" with the title "Kubernetes Patterns", the authors "Bilgin Ibryam" and the chapters:
      | title                 | text                                                                                                    |
      | Foundational Patterns | The sidecar container extends the main container. Health probes tell the platform when a pod is alive. |
      | Behavioral Patterns   | A batch job runs to completion. A daemon service runs on every node of the cluster.                     |
    And the sidecar "library/Ibryam/Kubernetes Patterns/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000001", "title": "Kubernetes Patterns", "subtitle": "Reusable Elements", "authors": ["Bilgin Ibryam"], "language": "en", "publisher": "O'Reilly Media", "year": 2019, "description": "Design patterns for containers", "tags": ["cloud/kubernetes", "devops"], "readStatus": "read", "files": [{ "name": "kp.epub" }] }
      """
    And a sample PDF "library/Alander/Event Sourcing in Practice/es.pdf" with the title "Event Sourcing in Practice", the author "Jördis Ålander" and the pages:
      | text                                                                                              |
      | Event sourcing stores every change as an event. Aggregates replay events to rebuild their state.  |
      | A projection builds a read model from the event stream. Kubernetes is only mentioned once here.  |
    And the sidecar "library/Alander/Event Sourcing in Practice/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000002", "title": "Event Sourcing in Practice", "authors": ["Jördis Ålander"], "language": "en", "publisher": "Manning Publications", "year": 2021, "tags": ["architektur/ddd", "cloud/azure"], "files": [{ "name": "es.pdf" }] }
      """
    And a sample EPUB "library/Weiss/Verteilte Systeme/vs.epub" with the title "Verteilte Systeme verstehen", the authors "Lukas Weiß" and the chapters:
      | title   | text                                                                        |
      | Konsens | Ein Konsensprotokoll sorgt dafür, dass alle Knoten denselben Zustand sehen. |
    And the sidecar "library/Weiss/Verteilte Systeme/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000003", "title": "Verteilte Systeme verstehen", "authors": ["Lukas Weiß"], "language": "de", "publisher": "Manning Publications", "year": 2015, "tags": ["architektur/verteilte-systeme", "architektur/ddd"], "files": [{ "name": "vs.epub" }] }
      """
    And I have run shelf with arguments "reindex --library library"
    And the web server is running on the library "library"

  Scenario: The list shows all works, ordered by title
    When I request "GET /api/v1/works"
    Then the response status is 200
    And the JSON field "total" of the response is 3
    And the JSON field "offset" of the response is 0
    And the JSON field "limit" of the response is 50
    And the JSON array "items" of the response has 3 entries
    And the JSON field "items[0].title" of the response is "Event Sourcing in Practice"
    And the JSON field "items[1].title" of the response is "Kubernetes Patterns"
    And the JSON field "items[2].title" of the response is "Verteilte Systeme verstehen"

  Scenario Outline: The list can be sorted
    When I request "GET /api/v1/works?sort=<sort>&order=<order>"
    Then the response status is 200
    And the JSON field "items[0].title" of the response is "<first>"
    And the JSON field "items[2].title" of the response is "<last>"

    Examples:
      | sort   | order | first                       | last                        |
      | title  | asc   | Event Sourcing in Practice  | Verteilte Systeme verstehen |
      | title  | desc  | Verteilte Systeme verstehen | Event Sourcing in Practice  |
      | year   | asc   | Verteilte Systeme verstehen | Event Sourcing in Practice  |
      | year   | desc  | Event Sourcing in Practice  | Verteilte Systeme verstehen |
      | author | asc   | Event Sourcing in Practice  | Verteilte Systeme verstehen |
      | author | desc  | Verteilte Systeme verstehen | Event Sourcing in Practice  |

  Scenario Outline: The list can be filtered with the filters of the query language
    When I request "GET /api/v1/works?q=<q>"
    Then the response status is 200
    And the JSON field "total" of the response is <total>

    Examples:
      | q                   | total |
      | lang%3Ade           | 1     |
      | tag%3Aarchitektur   | 2     |
      | year%3A%3E%3D2019   | 2     |
      | format%3Apdf        | 1     |
      | status%3Aread       | 1     |
      | -tag%3Adevops       | 2     |
      | lang%3Aen+year%3A2021 | 1   |
      | author%3Aibryam     | 1     |
      | title%3Averteilte   | 1     |
      | publisher%3Amanning | 2     |
      | publisher%3Areilly  | 1     |
      | publisher%3A%22Manning+Publications%22+lang%3Aen | 1 |
      | publisher%3Amanning+format%3Aepub | 1 |
      | -publisher%3Amanning | 1    |

  Scenario: A filter that matches nothing gives an empty list
    When I request "GET /api/v1/works?q=lang%3Afr"
    Then the response status is 200
    And the JSON field "total" of the response is 0
    And the JSON array "items" of the response has 0 entries

  Scenario Outline: A query that is not a list of filters is refused with the reason
    When I request "GET /api/v1/works?q=<q>"
    Then the response status is 400
    And the response header "Content-Type" is "application/problem+json"
    And the JSON field "detail" of the response contains "<message>"

    Examples:
      | q                    | message                          |
      | kubernetes           | filters only                     |
      | tag%3Acloud+docker   | filters only                     |
      | year%3Aabc           | year                             |
      | status%3Anever       | status                           |

  Scenario Outline: The list is paged
    When I request "GET /api/v1/works?limit=<limit>&offset=<offset>"
    Then the response status is 200
    And the JSON field "total" of the response is 3
    And the JSON field "offset" of the response is <offset>
    And the JSON field "limit" of the response is <limit>
    And the JSON array "items" of the response has <count> entries

    Examples:
      | limit | offset | count |
      | 2     | 0      | 2     |
      | 2     | 2      | 1     |
      | 1     | 1      | 1     |
      | 100   | 0      | 3     |
      | 5     | 3      | 0     |

  Scenario Outline: Paging and sorting values out of range are refused
    When I request "GET /api/v1/works?<parameters>"
    Then the response status is 400
    And the JSON field "detail" of the response contains "<message>"

    Examples:
      | parameters      | message                          |
      | limit=0         | limit must be between 1 and 100  |
      | limit=101       | limit must be between 1 and 100  |
      | limit=many      | limit must be between 1 and 100  |
      | offset=-1       | offset must not be negative      |
      | sort=colour     | sort must be one of              |
      | order=sideways  | order must be asc or desc        |

  Scenario: The facets count the works of the library
    When I request "GET /api/v1/works"
    Then the JSON field "facets.tags[0].value" of the response is "architektur/ddd"
    And the JSON field "facets.tags[0].count" of the response is 2
    And the JSON array "facets.tags" of the response has 5 entries
    And the JSON field "facets.languages[0].value" of the response is "en"
    And the JSON field "facets.languages[0].count" of the response is 2
    And the JSON field "facets.languages[1].value" of the response is "de"
    And the JSON field "facets.formats[0].value" of the response is "epub"
    And the JSON field "facets.formats[0].count" of the response is 2
    And the JSON field "facets.formats[1].value" of the response is "pdf"
    And the JSON field "facets.statuses[0].value" of the response is "unread"
    And the JSON field "facets.statuses[0].count" of the response is 2
    And the JSON field "facets.years[0].value" of the response is "2021"
    And the JSON array "facets.years" of the response has 3 entries
    And the JSON field "facets.authors[0].value" of the response is "Bilgin Ibryam"
    And the JSON array "facets.authors" of the response has 3 entries
    And the JSON field "facets.publishers[0].value" of the response is "Manning Publications"
    And the JSON field "facets.publishers[0].count" of the response is 2
    And the JSON field "facets.publishers[1].value" of the response is "O'Reilly Media"
    And the JSON array "facets.publishers" of the response has 2 entries

  Scenario: The facets count only the works that the filter lets through
    When I request "GET /api/v1/works?q=lang%3Ade"
    Then the JSON array "facets.formats" of the response has 1 entries
    And the JSON field "facets.formats[0].value" of the response is "epub"
    And the JSON field "facets.formats[0].count" of the response is 1
    And the JSON array "facets.tags" of the response has 2 entries
    And the JSON array "facets.languages" of the response has 1 entries

  Scenario: The facets do not depend on the page
    When I request "GET /api/v1/works?limit=1"
    Then the JSON array "items" of the response has 1 entries
    And the JSON field "facets.formats[0].count" of the response is 2

  Scenario: An item of the list has the fields that the library view needs
    When I request "GET /api/v1/works?q=author%3Aibryam"
    Then the JSON field "items[0].id" of the response is "01J9Z3K0000000000000000001"
    And the JSON field "items[0].title" of the response is "Kubernetes Patterns"
    And the JSON field "items[0].subtitle" of the response is "Reusable Elements"
    And the JSON field "items[0].authors[0]" of the response is "Bilgin Ibryam"
    And the JSON field "items[0].year" of the response is 2019
    And the JSON field "items[0].language" of the response is "en"
    And the JSON field "items[0].formats[0]" of the response is "epub"
    And the JSON field "items[0].tags[0]" of the response is "cloud/kubernetes"
    And the JSON field "items[0].tags[1]" of the response is "devops"
    And the JSON field "items[0].readStatus" of the response is "read"

  Scenario: A work with an EPUB and a PDF is listed once with both formats
    Given a sample PDF "library/Ibryam/Kubernetes Patterns/kp.pdf" with the title "Kubernetes Patterns", the author "Bilgin Ibryam" and the pages:
      | text                                                                 |
      | The sidecar container extends the main container of the pod.         |
    And the sidecar "library/Ibryam/Kubernetes Patterns/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000001", "title": "Kubernetes Patterns", "authors": ["Bilgin Ibryam"], "language": "en", "year": 2019, "files": [{ "name": "kp.epub" }, { "name": "kp.pdf" }] }
      """
    And I have run shelf with arguments "reindex --library library"
    When I request "GET /api/v1/works?q=author%3Aibryam"
    Then the JSON field "total" of the response is 1
    And the JSON array "items" of the response has 1 entries
    And the JSON field "items[0].formats[0]" of the response is "epub"
    And the JSON field "items[0].formats[1]" of the response is "pdf"

  Scenario: A field without a value is null, not left out
    When I request "GET /api/v1/works?q=lang%3Ade"
    Then the JSON field "items[0].subtitle" of the response is null

  Scenario: The details of a work
    When I request "GET /api/v1/works/01J9Z3K0000000000000000001"
    Then the response status is 200
    And the JSON field "id" of the response is "01J9Z3K0000000000000000001"
    And the JSON field "title" of the response is "Kubernetes Patterns"
    And the JSON field "subtitle" of the response is "Reusable Elements"
    And the JSON field "authors[0]" of the response is "Bilgin Ibryam"
    And the JSON field "language" of the response is "en"
    And the JSON field "year" of the response is 2019
    And the JSON field "description" of the response is "Design patterns for containers"
    And the JSON field "tags[0]" of the response is "cloud/kubernetes"
    And the JSON field "readStatus" of the response is "read"
    And the JSON field "files[0].name" of the response is "kp.epub"
    And the JSON field "files[0].format" of the response is "epub"
    And the JSON field "toc[0].title" of the response is not empty

  Scenario: The details of a PDF give its pages
    When I request "GET /api/v1/works/01J9Z3K0000000000000000002"
    Then the JSON field "files[0].format" of the response is "pdf"
    And the JSON field "files[0].pages" of the response is 2

  Scenario Outline: The start of an ID names a work when it is unique
    When I request "GET /api/v1/works/<reference>"
    Then the response status is <status>
    And the JSON field "<field>" of the response <check>

    Examples:
      | reference                  | status | field  | check                                           |
      | 01J9Z3K00000000000000000   | 400    | detail | contains "matches 3 works"                      |
      | 01J9Z3K0000000000000000002 | 200    | title  | is "Event Sourcing in Practice"                 |
      | 01j9z3k0000000000000000003 | 200    | title  | is "Verteilte Systeme verstehen"                |
      | 01J9Z3K0000000000000000009 | 404    | detail | contains "no work matches"                      |

  Scenario Outline: Only an ID names a work, never a path
    When I request "GET /api/v1/works/<reference>"
    Then the response status is 404
    And the JSON field "detail" of the response contains "no work matches"

    Examples:
      | reference |
      | Ibryam    |
      | library   |
      | kp.epub   |

  Scenario Outline: A path with folders does not reach a work
    When I request "GET /api/v1/works/<reference>"
    Then the response status is 404

    Examples:
      | reference                                |
      | ..%2F..%2Fetc                            |
      | Ibryam%2FKubernetes%20Patterns%2Fkp.epub |
