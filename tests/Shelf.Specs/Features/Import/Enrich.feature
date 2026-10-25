@F-IMP-6 @M5
Feature: Loading book metadata from the internet
  As a user whose e-books carry poor metadata
  I want shelf enrich to look a work up in Open Library and Google Books
  So that publisher, year, description and cover are filled in, but only when I say so

  Background:
    Given an empty working directory
    And I have run shelf with arguments "init library"
    And a sample EPUB "library/Ibryam, Bilgin/Kubernetes Patterns (2019)/kp.epub" with the title "Kubernetes Patterns", the authors "Bilgin Ibryam" and the text seed 1
    And the sidecar "library/Ibryam, Bilgin/Kubernetes Patterns (2019)/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K1000000000000000001", "title": "Kubernetes Patterns", "authors": ["Bilgin Ibryam"], "isbn13": "9781492034926", "year": 2019, "files": [{ "name": "kp.epub" }] }
      """
    And I have run shelf with arguments "reindex --library library"
    And the online source "openlibrary" holds the book:
      | field       | value                                 |
      | isbn13      | 9781492034926                         |
      | title       | Kubernetes Patterns                   |
      | subtitle    | Reusable Elements for Designing Cloud-Native Applications |
      | authors     | Bilgin Ibryam, Roland Huß             |
      | publisher   | O'Reilly Media                        |
      | year        | 2020                                  |
      | language    | en                                    |
      | description | Design patterns for Kubernetes.       |

  Scenario: The preview shows what would change and writes nothing
    When I run shelf with arguments "enrich 01J9Z3K1 --library library"
    Then the exit code is 0
    And standard output contains "from openlibrary"
    And standard output contains "+ publisher: O'Reilly Media"
    And standard output contains "+ language: en"
    And standard output contains "= year: 2019 (kept, online: 2020)"
    And standard output contains "Nothing written; run again with --apply"
    And the sidecar "library/Ibryam, Bilgin/Kubernetes Patterns (2019)/book.json" has the title "Kubernetes Patterns" and the authors "Bilgin Ibryam"
    And the work "01J9Z3K1000000000000000001" in the index has no publisher

  Scenario: Applying fills only the empty fields
    When I run shelf with arguments "enrich 01J9Z3K1 --apply --library library"
    Then the exit code is 0
    And standard output contains "Updated 01J9Z3K1000000000000000001: subtitle, publisher, language, description"
    When I run shelf with arguments "show 01J9Z3K1 --library library --json"
    Then the JSON of standard output has the string "publisher" equal to "O'Reilly Media"
    And the JSON of standard output has the number "year" equal to 2019
    And the JSON of standard output has the string "language" equal to "en"

  Scenario: --overwrite replaces values that are already set
    When I run shelf with arguments "enrich 01J9Z3K1 --apply --overwrite --library library"
    Then the exit code is 0
    And standard output contains "year"
    When I run shelf with arguments "show 01J9Z3K1 --library library --json"
    Then the JSON of standard output has the number "year" equal to 2020

  Scenario: The result is searchable and survives a reindex
    Given I have run shelf with arguments "enrich 01J9Z3K1 --apply --library library"
    And I have run shelf with arguments "reindex --library library"
    When I run shelf with arguments "search 'cloud-native applications' --library library"
    Then the search results are in this order: "Kubernetes Patterns"

  Scenario: The preview is available as JSON
    When I run shelf with arguments "enrich 01J9Z3K1 --json --library library"
    Then the exit code is 0
    And the JSON of standard output has the string "source" equal to "openlibrary"
    And the JSON of standard output has the boolean "applied" equal to "false"
    And the JSON property "changes" of standard output has 4 items

  Scenario: Google Books answers when Open Library knows nothing
    Given the online source "openlibrary" holds no books
    And the online source "google" holds the book:
      | field     | value               |
      | isbn13    | 9781492034926       |
      | title     | Kubernetes Patterns |
      | authors   | Bilgin Ibryam       |
      | publisher | O'Reilly Media      |
    When I run shelf with arguments "enrich 01J9Z3K1 --apply --library library"
    Then the exit code is 0
    And standard output contains "from google"
    And the online source "openlibrary" was queried before the online source "google"
    And the work "01J9Z3K1000000000000000001" in the index has the publisher "O'Reilly Media"

  Scenario: --source limits the lookup to one source
    Given the online source "openlibrary" holds no books
    And the online source "google" holds the book:
      | field     | value               |
      | isbn13    | 9781492034926       |
      | title     | Kubernetes Patterns |
      | authors   | Bilgin Ibryam       |
      | publisher | O'Reilly Media      |
    When I run shelf with arguments "enrich 01J9Z3K1 --source openlibrary --library library"
    Then the exit code is 1
    And standard error contains "no online source knows this book"
    And the online source "google" was not queried

  Scenario: Nothing is found anywhere
    Given the online source "openlibrary" holds no books
    When I run shelf with arguments "enrich 01J9Z3K1 --apply --library library"
    Then the exit code is 1
    And standard error contains "no online source knows this book"
    And the work "01J9Z3K1000000000000000001" in the index has no publisher

  Scenario Outline: A work without ISBN is searched by title and author
    Given the sidecar "library/Ibryam, Bilgin/Kubernetes Patterns (2019)/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K1000000000000000001", "title": "<title>", "authors": ["Bilgin Ibryam"], "files": [{ "name": "kp.epub" }] }
      """
    And I have run shelf with arguments "reindex --library library"
    When I run shelf with arguments "enrich 01J9Z3K1 --library library"
    Then the exit code is <exit>
    And <outcome>

    Examples:
      | title                    | exit | outcome                                                    |
      | Kubernetes Patterns      | 0    | standard output contains "matched by title and author"     |
      | kubernetes patterns      | 0    | standard output contains "matched by title and author"     |
      | Kubernetes               | 1    | standard error contains "no clear match"                   |

  Scenario: An ambiguous search lists candidates and writes nothing
    Given the sidecar "library/Ibryam, Bilgin/Kubernetes Patterns (2019)/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K1000000000000000001", "title": "Kubernetes Patterns", "authors": ["Bilgin Ibryam"], "files": [{ "name": "kp.epub" }] }
      """
    And I have run shelf with arguments "reindex --library library"
    And the online source "openlibrary" holds the book:
      | field     | value                       |
      | title     | Kubernetes Patterns         |
      | authors   | Bilgin Ibryam               |
      | publisher | O'Reilly Media              |
      | year      | 2019                        |
    When I run shelf with arguments "enrich 01J9Z3K1 --apply --library library"
    Then the exit code is 1
    And standard error contains "2 candidates; pick one with --pick"
    And standard output contains "1. Kubernetes Patterns"
    And standard output contains "2. Kubernetes Patterns"
    And the work "01J9Z3K1000000000000000001" in the index has no publisher

  Scenario: --pick chooses one of the candidates
    Given the sidecar "library/Ibryam, Bilgin/Kubernetes Patterns (2019)/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K1000000000000000001", "title": "Kubernetes Patterns", "authors": ["Bilgin Ibryam"], "files": [{ "name": "kp.epub" }] }
      """
    And I have run shelf with arguments "reindex --library library"
    And the online source "openlibrary" holds the book:
      | field     | value                       |
      | title     | Kubernetes Patterns         |
      | authors   | Bilgin Ibryam               |
      | publisher | Manning                     |
      | year      | 2019                        |
    When I run shelf with arguments "enrich 01J9Z3K1 --apply --pick 2 --library library"
    Then the exit code is 0
    And the work "01J9Z3K1000000000000000001" in the index has the publisher "Manning"

  Scenario: The cover is loaded when the work has none
    Given the online source "openlibrary" has a cover of type "image/jpeg" for the ISBN "9781492034926"
    When I run shelf with arguments "enrich 01J9Z3K1 --apply --library library"
    Then the exit code is 0
    And standard output contains "cover.jpg"
    And the file "library/Ibryam, Bilgin/Kubernetes Patterns (2019)/cover.jpg" exists

  Scenario: An existing cover is kept
    Given the online source "openlibrary" has a cover of type "image/jpeg" for the ISBN "9781492034926"
    And the file "library/Ibryam, Bilgin/Kubernetes Patterns (2019)/cover.png" has the content "old"
    And I have run shelf with arguments "reindex --library library"
    When I run shelf with arguments "enrich 01J9Z3K1 --apply --library library"
    Then the exit code is 0
    And the file "library/Ibryam, Bilgin/Kubernetes Patterns (2019)/cover.jpg" does not exist
    And the file "library/Ibryam, Bilgin/Kubernetes Patterns (2019)/cover.png" has the content "old"

  Scenario: --no-cover skips the cover download
    Given the online source "openlibrary" has a cover of type "image/jpeg" for the ISBN "9781492034926"
    When I run shelf with arguments "enrich 01J9Z3K1 --apply --no-cover --library library"
    Then the exit code is 0
    And the file "library/Ibryam, Bilgin/Kubernetes Patterns (2019)/cover.jpg" does not exist

  Scenario Outline: A failing source is reported and the work stays unchanged
    Given the online source "openlibrary" fails with "<failure>"
    When I run shelf with arguments "enrich 01J9Z3K1 --apply --library library"
    Then the exit code is 1
    And standard error contains "<message>"
    And the work "01J9Z3K1000000000000000001" in the index has no publisher

    Examples:
      | failure                 | message                           |
      | connection refused      | openlibrary: connection refused   |
      | HTTP 429 Too Many Requests | openlibrary: HTTP 429 Too Many Requests |

  Scenario: A failing first source does not stop the second one
    Given the online source "openlibrary" fails with "connection refused"
    And the online source "google" holds the book:
      | field     | value               |
      | isbn13    | 9781492034926       |
      | title     | Kubernetes Patterns |
      | authors   | Bilgin Ibryam       |
      | publisher | O'Reilly Media      |
    When I run shelf with arguments "enrich 01J9Z3K1 --apply --library library"
    Then the exit code is 0
    And standard error contains "openlibrary: connection refused"
    And the work "01J9Z3K1000000000000000001" in the index has the publisher "O'Reilly Media"

  Scenario Outline: Only enrich goes online
    When I run shelf with arguments "<arguments>"
    Then no online source was queried

    Examples:
      | arguments                                       |
      | reindex --library library                       |
      | scan --library library                          |
      | search kubernetes --library library             |
      | show 01J9Z3K1 --library library                 |
      | edit 01J9Z3K1 --year 2021 --library library     |

  Scenario Outline: Unusable arguments are refused before a source is queried
    When I run shelf with arguments "<arguments>"
    Then the exit code is 2
    And standard error contains "<message>"
    And no online source was queried

    Examples:
      | arguments                                              | message                                  |
      | enrich 01J9Z3K1 --source bing --library library        | --source must be openlibrary or google   |
      | enrich 01J9Z3K1 --pick 0 --library library             | --pick must be 1 or greater              |
      | enrich 01J9Z3K1 --overwrite --library library          | --overwrite needs --apply                |
