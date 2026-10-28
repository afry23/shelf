@F-SRC-1 @F-SRC-2 @F-SRC-3 @F-SRC-5 @M1
Feature: Searching the library
  As a user with a library of e-books
  I want to find works by their metadata and by what is written in them
  So that I can get to the right book and the right place in it

  Background:
    Given an empty working directory
    And I have run shelf with arguments "init library"
    And a sample EPUB "library/Ibryam/Kubernetes Patterns/kp.epub" with the title "Kubernetes Patterns", the authors "Bilgin Ibryam" and the chapters:
      | title               | text                                                                                      |
      | Foundational Patterns | The sidecar container extends the main container. Health probes tell the platform when a pod is alive. |
      | Behavioral Patterns   | A batch job runs to completion. A daemon service runs on every node of the cluster.        |
    And the sidecar "library/Ibryam/Kubernetes Patterns/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000001", "title": "Kubernetes Patterns", "subtitle": "Reusable Elements for Designing Cloud Native Applications", "authors": ["Bilgin Ibryam"], "language": "en", "publisher": "O'Reilly Media", "year": 2019, "tags": ["cloud/kubernetes", "devops"], "readStatus": "read", "files": [{ "name": "kp.epub" }] }
      """
    And a sample PDF "library/Alander/Event Sourcing in Practice/es.pdf" with the title "Event Sourcing in Practice", the author "Jördis Ålander" and the pages:
      | text                                                                                          |
      | Event sourcing stores every change as an event. Aggregates replay events to rebuild their state. |
      | A projection builds a read model from the event stream. Kubernetes is only mentioned once here. |
    And the sidecar "library/Alander/Event Sourcing in Practice/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000002", "title": "Event Sourcing in Practice", "authors": ["Jördis Ålander"], "language": "en", "publisher": "Manning Publications", "year": 2021, "tags": ["architektur/ddd", "cloud/azure"], "files": [{ "name": "es.pdf" }] }
      """
    And a sample EPUB "library/Weiss/Verteilte Systeme/vs.epub" with the title "Verteilte Systeme verstehen", the authors "Lukas Weiß" and the chapters:
      | title      | text                                                                                   |
      | Konsens    | Ein Konsensprotokoll sorgt dafür, dass alle Knoten denselben Zustand sehen.              |
    And the sidecar "library/Weiss/Verteilte Systeme/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000003", "title": "Verteilte Systeme verstehen", "authors": ["Lukas Weiß"], "language": "de", "publisher": "Verlag Zürich", "year": 2015, "tags": ["architektur/verteilte-systeme"], "readStatus": "reading", "files": [{ "name": "vs.epub" }] }
      """
    And a sample PDF "library/Mueller/Cloud Native/cn.pdf" with the title "Cloud Native", the author "Anna Müller" and the pages:
      | text                                             |
      | Serverless functions scale down to zero instances. |
    And the sidecar "library/Mueller/Cloud Native/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000004", "title": "Cloud Native", "authors": ["Anna Müller"], "language": "en", "publisher": "O'Reilly Media", "year": 2017, "tags": ["cloud/aws"], "files": [{ "name": "cn.pdf" }] }
      """
    And I have run shelf with arguments "reindex --library library"

  Scenario: Every result shows the ID of the work, so that other commands can be given it
    When I run shelf with arguments "search kubernetes --library library"
    Then standard output matches the pattern "(?m)^1\. Kubernetes Patterns - Bilgin Ibryam \(2019\) \[epub\]  01J9Z3K0000000000000000001$"

  Scenario: A word in the title ranks above a word that only appears in the text
    When I run shelf with arguments "search kubernetes --library library"
    Then the exit code is 0
    And the search results are in this order: "Kubernetes Patterns; Event Sourcing in Practice"

  Scenario Outline: Words are found in the metadata and in the content, with accents and case folded
    When I run shelf with arguments "search <query> --library library"
    Then the exit code is 0
    And the search results are, in any order: "<results>"

    Examples:
      | query           | results                                      | what                                   |
      | patterns        | Kubernetes Patterns                          | title                                  |
      | PATTERNS        | Kubernetes Patterns                          | case                                   |
      | reusable        | Kubernetes Patterns                          | subtitle                               |
      | ibryam          | Kubernetes Patterns                          | author                                 |
      | alander         | Event Sourcing in Practice                   | accent in the data, none in the query  |
      | ålander         | Event Sourcing in Practice                   | accent in both                         |
      | muller          | Cloud Native                                 | ü folded to u                          |
      | weiß            | Verteilte Systeme verstehen                  | ß as written                           |
      | sidecar         | Kubernetes Patterns                          | content of an EPUB                     |
      | projection      | Event Sourcing in Practice                   | content of a PDF                       |
      | konsensprotokoll | Verteilte Systeme verstehen                 | content in German                      |
      | architektur     | Event Sourcing in Practice; Verteilte Systeme verstehen | tags                        |
      | azure           | Event Sourcing in Practice                   | tags                                   |

  Scenario: The letter ß is not spelled out as ss
    When I run shelf with arguments "search weiss --library library"
    Then the exit code is 0
    And standard output is "No results."

  Scenario Outline: Phrases match the words next to each other in this order
    When I run shelf with arguments 'search "<query>" --library library'
    Then the search results are, in any order: "<results>"

    Examples:
      | query                         | results                    |
      | \"event sourcing\"            | Event Sourcing in Practice |
      | \"sourcing event\"            |                            |
      | \"sidecar container\"         | Kubernetes Patterns        |
      | \"container sidecar\"         |                            |
      | event \"read model\"          | Event Sourcing in Practice |

  Scenario Outline: A prefix matches the beginnings of words
    When I run shelf with arguments "search <query> --library library"
    Then the search results are, in any order: "<results>"

    Examples:
      | query   | results                                          |
      | kube*   | Kubernetes Patterns; Event Sourcing in Practice  |
      | serv*   | Kubernetes Patterns; Cloud Native                |
      | verteil* | Verteilte Systeme verstehen                     |
      | zzz*    |                                                  |

  Scenario Outline: Filters narrow the works by their data
    When I run shelf with arguments "search <query> --library library"
    Then the exit code is 0
    And the search results are, in any order: "<results>"

    Examples:
      | query                  | results                                                        |
      | author:ibryam          | Kubernetes Patterns                                            |
      | author:alander         | Event Sourcing in Practice                                     |
      | title:cloud            | Kubernetes Patterns; Cloud Native                              |
      | title:kube*            | Kubernetes Patterns                                            |
      | tag:cloud              | Kubernetes Patterns; Event Sourcing in Practice; Cloud Native  |
      | tag:cloud/azure        | Event Sourcing in Practice                                     |
      | tag:devops             | Kubernetes Patterns                                            |
      | lang:de                | Verteilte Systeme verstehen                                    |
      | lang:en                | Kubernetes Patterns; Event Sourcing in Practice; Cloud Native  |
      | lang:fr                |                                                                |
      | year:2015              | Verteilte Systeme verstehen                                    |
      | year:>2016             | Kubernetes Patterns; Event Sourcing in Practice; Cloud Native  |
      | year:>=2019            | Kubernetes Patterns; Event Sourcing in Practice                |
      | year:<2017             | Verteilte Systeme verstehen                                    |
      | year:<=2017            | Verteilte Systeme verstehen; Cloud Native                      |
      | year:2016..2019        | Kubernetes Patterns; Cloud Native                              |
      | format:pdf             | Event Sourcing in Practice; Cloud Native                       |
      | format:epub            | Kubernetes Patterns; Verteilte Systeme verstehen               |
      | status:read            | Kubernetes Patterns                                            |
      | status:reading         | Verteilte Systeme verstehen                                    |
      | status:unread          | Event Sourcing in Practice; Cloud Native                       |
      | publisher:manning      | Event Sourcing in Practice                                     |
      | publisher:MANNING      | Event Sourcing in Practice                                     |
      | publisher:manni*       | Event Sourcing in Practice                                     |
      | publisher:reilly       | Kubernetes Patterns; Cloud Native                              |
      | publisher:\"Manning Publications\" | Event Sourcing in Practice                           |
      | publisher:\"Publications Manning\" |                                                      |
      | publisher:zurich       | Verteilte Systeme verstehen                                    |
      | publisher:springer     |                                                                |

  Scenario Outline: Filters combine with each other and with words
    When I run shelf with arguments "search <query> --library library"
    Then the search results are, in any order: "<results>"

    Examples:
      | query                              | results                                           |
      | kubernetes status:read             | Kubernetes Patterns                               |
      | kubernetes format:pdf              | Event Sourcing in Practice                        |
      | cloud year:>2017                   | Kubernetes Patterns; Event Sourcing in Practice   |
      | tag:cloud lang:en format:epub      | Kubernetes Patterns                               |
      | tag:cloud year:>2030               |                                                   |
      | publisher:reilly format:pdf        | Cloud Native                                      |
      | publisher:reilly lang:en tag:devops | Kubernetes Patterns                              |
      | publisher:reilly publisher:manning |                                                   |
      | publisher:reilly cloud year:>2018  | Kubernetes Patterns                               |

  Scenario Outline: Negation removes works
    When I run shelf with arguments "search <query> --library library"
    Then the search results are, in any order: "<results>"

    Examples:
      | query                  | results                                                       |
      | cloud -azure           | Kubernetes Patterns; Cloud Native                             |
      | tag:cloud -tag:azure   | Kubernetes Patterns; Cloud Native                             |
      | -tag:cloud lang:en     |                                                               |
      | lang:en -publisher:reilly | Event Sourcing in Practice                                 |
      | kubernetes -sidecar    | Event Sourcing in Practice                                    |
      | -year:2015 tag:architektur | Event Sourcing in Practice                                |
      | -azure                 | Kubernetes Patterns; Verteilte Systeme verstehen; Cloud Native |

  Scenario: A content hit comes with a snippet and the page of a PDF
    When I run shelf with arguments "search projection --library library"
    Then the exit code is 0
    And standard output contains "1. Event Sourcing in Practice - Jördis Ålander (2021) [pdf]"
    And standard output contains "page 2  es.pdf: "
    And standard output contains "A [projection] builds a read model"

  Scenario: A content hit in an EPUB names the section and the chapter location
    When I run shelf with arguments "search sidecar container --library library"
    Then the exit code is 0
    And standard output contains "section 1  kp.epub: "
    And standard output contains "The [sidecar] [container] extends"

  Scenario: The JSON output has the works, their data and the locations of the hits
    When I run shelf with arguments "search sidecar container --json --library library"
    Then the exit code is 0
    And the JSON of standard output has the string "results[0].title" equal to "Kubernetes Patterns"
    And the JSON of standard output has the string "results[0].id" equal to "01J9Z3K0000000000000000001"
    And the JSON of standard output has the string "results[0].authors[0]" equal to "Bilgin Ibryam"
    And the JSON of standard output has the string "results[0].hits[0].file" equal to "Ibryam/Kubernetes Patterns/kp.epub"
    And the JSON of standard output has the string "results[0].hits[0].locator" equal to "epubcfi(/6/2[chapter1]!)"
    And the JSON of standard output has the string "results[0].hits[0].where" equal to "section 1"
    And the JSON of standard output has the string "results[0].hits[0].snippet" containing "[sidecar]"
    And the JSON of standard output has the number "total" equal to 1

  Scenario: The number of works is limited
    When I run shelf with arguments "search tag:cloud --limit 2 --library library"
    Then the exit code is 0
    And the search results have 2 entries

  Scenario: A search without a match is not an error
    When I run shelf with arguments "search nothingtobefoundhere --library library"
    Then the exit code is 0
    And standard output is "No results."

  Scenario: Metadata can be found when the content was not read
    Given I have run shelf with arguments "reindex --metadata-only --library library"
    When I run shelf with arguments "search kubernetes --library library"
    Then the search results are, in any order: "Kubernetes Patterns"

  Scenario: Content cannot be found when it was not read
    Given I have run shelf with arguments "reindex --metadata-only --library library"
    When I run shelf with arguments "search sidecar --library library"
    Then standard output is "No results."

  Scenario Outline: Characters with a meaning in the index are plain text
    When I run shelf with arguments '<arguments>'
    Then the exit code is 0

    Examples:
      | arguments                                       |
      | search "(kube" --library library                |
      | search "NEAR(a b)" --library library            |
      | search "a OR b AND c NOT d" --library library   |
      | search "^kube" --library library                |
      | search "kube:*" --library library               |
      | search "a:b:c" --library library                |
      | search "\"unterminated" --library library       |
      | search "x\"y" --library library                 |

  Scenario Outline: A query that cannot be understood is a usage error
    When I run shelf with arguments '<arguments>'
    Then the exit code is 2
    And standard output is empty
    And standard error contains "<message>"

    Examples:
      | arguments                                  | message                                            |
      | search year:abc --library library          | error: year: 'abc' is not a year                   |
      | search status:done --library library       | error: status: 'done' is not one of                |
      | search kubernetes --limit 0 --library library | --limit                                         |
      | search --library library                   | Required argument missing                          |
      | search "..." --library library             | error: the query has nothing to search for         |
      | search "" --library library                | error: the query has nothing to search for         |
      | search "title:" --library library          | error: title: needs a value                        |

  Scenario: Searching needs a library
    When I run shelf with arguments "search x --library nowhere"
    Then the exit code is 1
    And standard error contains "is not a shelf library"

  Scenario: A library whose index was deleted says what to do
    Given I delete the file "library/.shelf/index.db"
    When I run shelf with arguments "search kubernetes --library library"
    Then the exit code is 1
    And standard error contains "run 'shelf reindex'"
