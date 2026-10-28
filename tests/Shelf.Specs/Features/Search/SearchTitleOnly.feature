@F-SRC-7 @M1
Feature: Searching titles only
  As a user who knows the name of a book
  I want a search that looks only at titles and subtitles
  So that a word inside the books or in a description does not bury the book I mean

  Background:
    Given an empty working directory
    And I have run shelf with arguments "init library"
    And a sample EPUB "library/Ibryam/Kubernetes Patterns/kp.epub" with the title "Kubernetes Patterns", the authors "Bilgin Ibryam" and the chapters:
      | title               | text                                                                                      |
      | Foundational Patterns | The sidecar container extends the main container. Health probes tell the platform when a pod is alive. |
      | Behavioral Patterns   | A batch job runs to completion. A daemon service runs on every node of the cluster.        |
    And the sidecar "library/Ibryam/Kubernetes Patterns/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000001", "title": "Kubernetes Patterns", "subtitle": "Reusable Elements for Designing Cloud Native Applications", "authors": ["Bilgin Ibryam"], "language": "en", "year": 2019, "tags": ["cloud/kubernetes", "devops"], "readStatus": "read", "files": [{ "name": "kp.epub" }] }
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
      | title      | text                                                                                   |
      | Konsens    | Ein Konsensprotokoll sorgt dafür, dass alle Knoten denselben Zustand sehen.              |
    And the sidecar "library/Weiss/Verteilte Systeme/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000003", "title": "Verteilte Systeme verstehen", "authors": ["Lukas Weiß"], "language": "de", "year": 2015, "tags": ["architektur/verteilte-systeme"], "readStatus": "reading", "files": [{ "name": "vs.epub" }] }
      """
    And a sample PDF "library/Mueller/Cloud Native/cn.pdf" with the title "Cloud Native", the author "Anna Müller" and the pages:
      | text                                             |
      | Serverless functions scale down to zero instances. |
    And the sidecar "library/Mueller/Cloud Native/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000004", "title": "Cloud Native", "authors": ["Anna Müller"], "language": "en", "year": 2017, "tags": ["cloud/aws"], "files": [{ "name": "cn.pdf" }] }
      """
    And I have run shelf with arguments "reindex --library library"

  Scenario Outline: With --title-only only the title and the subtitle count
    When I run shelf with arguments "search <query> --title-only --library library"
    Then the exit code is 0
    And the search results are, in any order: "<results>"

    Examples:
      | query       | results                      | what                              |
      | kubernetes  | Kubernetes Patterns          | title; the text of the PDF is ignored |
      | cloud       | Kubernetes Patterns; Cloud Native | subtitle and title; tags are ignored |
      | reusable    | Kubernetes Patterns          | subtitle                          |
      | sidecar     |                              | content only                      |
      | ibryam      |                              | author only                       |
      | azure       |                              | tag only                          |
      | PATTERN*    | Kubernetes Patterns          | prefix with case folded           |
      | muller      |                              | author only, accent folded        |
      | weiß        |                              | author only                       |
      | verteilte   | Verteilte Systeme verstehen  | title in German                   |

  Scenario Outline: All words must be in the title or the subtitle
    When I run shelf with arguments 'search <query> --title-only --library library'
    Then the search results are, in any order: "<results>"

    Examples:
      | query                          | results                    |
      | event practice                 | Event Sourcing in Practice |
      | event kubernetes               |                            |
      | \"event sourcing\"             | Event Sourcing in Practice |
      | \"sourcing event\"             |                            |

  Scenario: Filters work together with --title-only
    When I run shelf with arguments "search cloud --title-only year:>2018 --library library"
    Then the search results are, in any order: "Kubernetes Patterns"

  Scenario: Hits of a title search carry no passages
    When I run shelf with arguments "search kubernetes --title-only --library library --json"
    Then the JSON of standard output has the number "total" equal to 1
    And the JSON of standard output has no property "results[0].passages[0]"

  Scenario: Without --title-only a word from the text still finds the work
    When I run shelf with arguments "search sidecar --library library"
    Then the search results are, in any order: "Kubernetes Patterns"
