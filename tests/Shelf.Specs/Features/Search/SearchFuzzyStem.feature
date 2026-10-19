@F-SRC-4 @F-SRC-6 @M5
Feature: Finding works despite typos and word endings
  As a user who does not remember exact spellings
  I want a search to find "Pattern" for "patterns" and "Kubernetes" for "kubrnetes"
  So that small differences in the query do not hide the book

  Background:
    Given an empty working directory
    And I have run shelf with arguments "init library"
    And a sample EPUB "library/Ibryam/Kubernetes Patterns/kp.epub" with the title "Kubernetes Patterns", the authors "Bilgin Ibryam" and the chapters:
      | title                 | text                                                       |
      | Foundational Patterns | The sidecar container extends the main container.          |
    And the sidecar "library/Ibryam/Kubernetes Patterns/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000001", "title": "Kubernetes Patterns", "authors": ["Bilgin Ibryam"], "language": "en", "year": 2019, "files": [{ "name": "kp.epub" }] }
      """
    And a sample PDF "library/Alander/Event Sourcing in Practice/es.pdf" with the title "Event Sourcing in Practice", the author "Jördis Ålander" and the pages:
      | text                                              |
      | Event sourcing stores every change as an event.   |
    And the sidecar "library/Alander/Event Sourcing in Practice/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000002", "title": "Event Sourcing in Practice", "authors": ["Jördis Ålander"], "language": "en", "year": 2021, "files": [{ "name": "es.pdf" }] }
      """
    And a sample EPUB "library/Weiss/Verteilte Systeme/vs.epub" with the title "Verteilte Systeme verstehen", the authors "Lukas Weiß" and the chapters:
      | title   | text                                                          |
      | Konsens | Ein Konsensprotokoll sorgt dafür, dass alle Knoten gleich sind. |
    And the sidecar "library/Weiss/Verteilte Systeme/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000003", "title": "Verteilte Systeme verstehen", "authors": ["Lukas Weiß"], "language": "de", "year": 2015, "files": [{ "name": "vs.epub" }] }
      """
    And I have run shelf with arguments "reindex --library library"

  # ---- F-SRC-6: stemming of the metadata, by the language of the work ----

  Scenario Outline: A word with another ending finds the metadata of a work in the same language
    When I run shelf with arguments "search <query> --library library"
    Then the exit code is 0
    And the search results are, in any order: "<results>"
    And standard error does not contain "No exact match"

    Examples:
      | query            | results                     | what                              |
      | pattern          | Kubernetes Patterns         | English plural in the title       |
      | events           | Event Sourcing in Practice  | English plural in the query       |
      | sourced          | Event Sourcing in Practice  | English -ing / -ed                |
      | system           | Verteilte Systeme verstehen | German plural in the title        |
      | verteiltes       | Verteilte Systeme verstehen | German ending                     |
      | verstehe         | Verteilte Systeme verstehen | German verb ending                |

  Scenario: A quoted phrase is exact: it is neither stemmed nor fuzzy
    When I run shelf with arguments "search \"pattern\" --library library"
    Then the exit code is 0
    And standard output does not contain "Kubernetes Patterns"
    And standard error does not contain "No exact match"

  Scenario: Stemming does not apply to the text of the books
    # the content index is unchanged (ADR 0004): "containers" is not found where only "container" is written
    When I run shelf with arguments "search containers --library library"
    Then the exit code is 0
    And standard output does not contain "Kubernetes Patterns"

  # ---- F-SRC-4: fuzzy search, only when the exact search finds nothing ----

  Scenario Outline: A typo in the title or the author finds the work when nothing matches exactly
    When I run shelf with arguments "search <query> --library library"
    Then the exit code is 0
    And the search results are, in any order: "<results>"
    And standard error contains "No exact match"

    Examples:
      | query        | results                     | what                        |
      | kubrnetes    | Kubernetes Patterns         | missing letter in the title |
      | kuberentes   | Kubernetes Patterns         | swapped letters             |
      | ibryan       | Kubernetes Patterns         | similar author name         |
      | sourcng      | Event Sourcing in Practice  | missing letter              |
      | alender      | Event Sourcing in Practice  | accent folded, one letter wrong |
      | verteilet    | Verteilte Systeme verstehen | German title                |

  Scenario: Every word of the query must be close to a word of the same work
    When I run shelf with arguments "search kubernets ibryan --library library"
    Then the search results are, in any order: "Kubernetes Patterns"
    When I run shelf with arguments "search kubernets alender --library library"
    Then the search results have 0 entries

  Scenario: A search with an exact match shows no fuzzy results
    When I run shelf with arguments "search kubernetes --library library"
    Then the search results are, in any order: "Kubernetes Patterns"
    And standard error does not contain "No exact match"

  Scenario Outline: Short or unrelated words are never fuzzy
    When I run shelf with arguments "search <query> --library library"
    Then the search results have 0 entries

    Examples:
      | query       | what                                  |
      | kub         | fewer than 4 letters: no fuzzy        |
      | xylophone   | nothing is similar                    |

  Scenario: Fuzzy search is also offered in the JSON output
    When I run shelf with arguments "search kubrnetes --json --library library"
    Then the exit code is 0
    And the JSON of standard output has the boolean "fuzzy" equal to "true"

  Scenario: A filter keeps its exact meaning when the search is fuzzy
    When I run shelf with arguments "search kubernets lang:de --library library"
    Then the search results have 0 entries
