@PRD-7.1 @F-LIB-5 @M5
Feature: Generating citation keys
  As a user who cites books
  I want shelf to build a citation key from author, year and title
  So that every work has a key for BibTeX without my typing it

  Background:
    Given an empty working directory
    And I have run shelf with arguments "init library"

  Scenario Outline: The key is the surname, the year and the first significant word of the title
    Given a sample EPUB "library/Author/Book/b.epub" with the title "<title>", the authors "<author>" and the text seed 1
    And the sidecar "library/Author/Book/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K1000000000000000001", "title": "<title>", "authors": ["<author>"], <year>"files": [{ "name": "b.epub" }] }
      """
    And I have run shelf with arguments "reindex --library library"
    When I run shelf with arguments "citekeys --library library"
    Then the exit code is 0
    And standard output is "Generated 1 cite key."
    When I run shelf with arguments "show 01J9Z3K1 --library library --json"
    Then the JSON of standard output has the string "citeKey" equal to "<key>"

    Examples:
      | author           | title                            | year            | key                   |
      | Bilgin Ibryam    | Kubernetes Patterns              | "year": 2019,   | ibryam2019kubernetes  |
      | Jördis Ålander   | Event Streams in Practice        | "year": 2021,   | alander2021event      |
      | Jan van der Berg | The Art of Computer Programming  | "year": 1968,   | berg1968art           |
      | Robert Musil     | Der Mann ohne Eigenschaften      | "year": 1930,   | musil1930mann         |
      | Lukas Weiß       | Über die Brücke (2nd Edition)    | "year": 2015,   | weiss2015uber         |
      | Douglas Adams    | A Guide to the Galaxy            | "year": 1979,   | adams1979guide        |
      | Frank Herbert    | Dune: Messiah                    | "year": 1969,   | herbert1969dune       |
      | Edwin A. Abbott  | Flatland                         |                 | abbottflatland        |

  Scenario: A work with an unknown author gets no key
    Given a sample EPUB "library/Author/Book/b.epub" with the title "Flatland", the authors "Unknown Author" and the text seed 1
    And the sidecar "library/Author/Book/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K1000000000000000001", "title": "Flatland", "authors": ["Unknown Author"], "files": [{ "name": "b.epub" }] }
      """
    And I have run shelf with arguments "reindex --library library"
    When I run shelf with arguments "citekeys --library library"
    Then the exit code is 0
    And standard output is "No works without a cite key."

  Rule: Keys are unique in the library, and existing keys are kept

    Background:
      Given a sample EPUB "library/Ibryam/Patterns/kp.epub" with the title "Kubernetes Patterns", the authors "Bilgin Ibryam" and the text seed 1
      And the sidecar "library/Ibryam/Patterns/book.json" is replaced by:
        """
        { "schema": 1, "id": "01J9Z3K1000000000000000001", "title": "Kubernetes Patterns", "authors": ["Bilgin Ibryam"], "year": 2019, "files": [{ "name": "kp.epub" }] }
        """
      And a sample EPUB "library/Ibryam/Operators/ko.epub" with the title "Kubernetes Operators", the authors "Bilgin Ibryam" and the text seed 2
      And the sidecar "library/Ibryam/Operators/book.json" is replaced by:
        """
        { "schema": 1, "id": "01J9Z3K2000000000000000002", "title": "Kubernetes Operators", "authors": ["Bilgin Ibryam"], "year": 2019, "files": [{ "name": "ko.epub" }] }
        """
      And I have run shelf with arguments "reindex --library library"

    Scenario: A key that is already taken gets a letter
      When I run shelf with arguments "citekeys --library library"
      Then the exit code is 0
      And standard output is "Generated 2 cite keys."
      When I run shelf with arguments "show 01J9Z3K1 --library library --json"
      Then the JSON of standard output has the string "citeKey" equal to "ibryam2019kubernetes"
      When I run shelf with arguments "show 01J9Z3K2 --library library --json"
      Then the JSON of standard output has the string "citeKey" equal to "ibryam2019kubernetesa"

    Scenario: A key that is set stays, and the next work avoids it
      Given I have run shelf with arguments "edit 01J9Z3K2 --cite-key ibryam2019kubernetes --library library"
      When I run shelf with arguments "citekeys --library library"
      Then standard output is "Generated 1 cite key."
      When I run shelf with arguments "show 01J9Z3K1 --library library --json"
      Then the JSON of standard output has the string "citeKey" equal to "ibryam2019kubernetesa"
      When I run shelf with arguments "show 01J9Z3K2 --library library --json"
      Then the JSON of standard output has the string "citeKey" equal to "ibryam2019kubernetes"

    Scenario: Without anything to do the command says so
      Given I have run shelf with arguments "citekeys --library library"
      When I run shelf with arguments "citekeys --library library"
      Then the exit code is 0
      And standard output is "No works without a cite key."

    Scenario: Naming a work replaces its key, and its own key does not count as taken
      Given I have run shelf with arguments "edit 01J9Z3K1 --cite-key mine --library library"
      When I run shelf with arguments "citekeys 01J9Z3K1 --library library"
      Then the exit code is 0
      And standard output is "Cite key of 01J9Z3K1000000000000000001: ibryam2019kubernetes"
      When I run shelf with arguments "citekeys 01J9Z3K1 --library library"
      Then standard output is "Cite key of 01J9Z3K1000000000000000001: ibryam2019kubernetes"

    Scenario: The JSON form lists the keys
      When I run shelf with arguments "citekeys --library library --json"
      Then the JSON of standard output has the number "generated" equal to 2
      And the JSON of standard output has the string "works[0].id" equal to "01J9Z3K1000000000000000001"
      And the JSON of standard output has the string "works[0].citeKey" equal to "ibryam2019kubernetes"

    Scenario: An unknown work is an error
      When I run shelf with arguments "citekeys 01J9Z3K9 --library library"
      Then the exit code is 1

  Scenario: A work that is imported gets a key
    Given the folder "incoming" contains the fixtures "derived/epub3-isbn-series.epub"
    When I run shelf with arguments "import incoming --library library"
    Then the exit code is 0
    And the file "library/Ålander, Jördis/Event Streams in Practice/book.json" contains "alander"
    And the file "library/Ålander, Jördis/Event Streams in Practice/book.json" contains "citeKey"
