@F-ANN-8 @M11
Feature: A passage as a quote with its citation
  As a person who writes about what they read
  I want a passage as a quote with its cite key and place
  So that I paste it into my text and the citation is right

  Background:
    Given an empty working directory
    And I have run shelf with arguments "init library"
    And a sample EPUB "library/Ibryam/Kubernetes Patterns/kp.epub" with the title "Kubernetes Patterns", the authors "Bilgin Ibryam" and the chapters:
      | title                 | text                                              |
      | Foundational Patterns | The sidecar container extends the main container. |
      | Behavioral Patterns   | A batch job runs to completion.                   |
    And the sidecar "library/Ibryam/Kubernetes Patterns/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000001", "title": "Kubernetes Patterns", "authors": ["Bilgin Ibryam"], "language": "en", "year": 2019, "citeKey": "ibryam2019kubernetes", "files": [{ "name": "kp.epub" }] }
      """
    And a sample PDF "library/Alander/Event Sourcing in Practice/es.pdf" with the title "Event Sourcing in Practice", the author "Jördis Ålander" and the pages:
      | text                                            |
      | Event sourcing stores every change as an event. |
      | A projection builds a read model.               |
    And the sidecar "library/Alander/Event Sourcing in Practice/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000002", "title": "Event Sourcing in Practice", "authors": ["Jördis Ålander"], "language": "en", "year": 2021, "citeKey": "alander2021event", "files": [{ "name": "es.pdf" }] }
      """
    And a sample PDF "library/Nobody/No Key/nk.pdf" with the title "No Key", the author "N. Body" and the pages:
      | text           |
      | A page of text. |
    And the sidecar "library/Nobody/No Key/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000003", "title": "No Key", "authors": ["N. Body"], "language": "en", "files": [{ "name": "nk.pdf" }] }
      """
    And I have run shelf with arguments "reindex --library library"

  Scenario Outline: A quote is a Markdown block quote followed by a citation with the place
    When I run shelf with arguments "cite <work> '<quote>' --locator '<locator>' --library library"
    Then the exit code is 0
    And standard output contains "> <quote>"
    And standard output contains "<citation>"

    Examples:
      | work                       | quote                              | locator                | citation                          |
      | 01J9Z3K0000000000000000002 | stores every change as an event.   | page=1                 | [@alander2021event, p. 1]         |
      | 01J9Z3K0000000000000000002 | builds a read model                | page=12                | [@alander2021event, p. 12]        |
      | 01J9Z3K0000000000000000001 | The sidecar container extends it.  | epubcfi(/6/4!/4/2/1:0) | [@ibryam2019kubernetes, sec. 2]   |

  Scenario: Without a place the citation names the work only
    When I run shelf with arguments "cite 01J9Z3K0000000000000000002 'stores every change' --library library"
    Then the exit code is 0
    And standard output contains "> stores every change"
    And standard output contains "[@alander2021event]"

  Scenario: The BibTeX entry of the work can be added
    When I run shelf with arguments "cite 01J9Z3K0000000000000000002 'stores every change' --locator page=1 --bibtex --library library"
    Then the exit code is 0
    And standard output contains "[@alander2021event, p. 1]"
    And standard output contains "alander2021event,"
    And standard output contains "Event Sourcing in Practice"

  Scenario: The result is available as JSON
    When I run shelf with arguments "cite 01J9Z3K0000000000000000002 'stores every change' --locator page=1 --json --library library"
    Then the exit code is 0
    And the JSON of standard output has the string "id" equal to "01J9Z3K0000000000000000002"
    And the JSON of standard output has the string "citeKey" equal to "alander2021event"
    And the JSON of standard output has the string "place" equal to "page 1"
    And the JSON of standard output has the string "text" containing "[@alander2021event, p. 1]"
    And the JSON of standard output has the string "bibtex" containing "alander2021event,"

  Scenario: A work with several files needs the file for a place
    Given a sample EPUB "library/Alander/Event Sourcing in Practice/es.epub" with the title "Event Sourcing in Practice", the authors "Jördis Ålander" and the chapters:
      | title | text              |
      | One   | Events are facts. |
    And the sidecar "library/Alander/Event Sourcing in Practice/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000002", "title": "Event Sourcing in Practice", "authors": ["Jördis Ålander"], "language": "en", "year": 2021, "citeKey": "alander2021event", "files": [{ "name": "es.pdf" }, { "name": "es.epub" }] }
      """
    And I have run shelf with arguments "reindex --library library"
    When I run shelf with arguments "cite 01J9Z3K0000000000000000002 'x' --locator page=1 --library library"
    Then the exit code is 2
    And standard error contains "--file"
    When I run shelf with arguments "cite 01J9Z3K0000000000000000002 'x' --locator page=1 --file es.pdf --library library"
    Then the exit code is 0

  Scenario Outline: A quote that cannot be made is refused and nothing changes
    Given I take a snapshot of the folder "library"
    When I run shelf with arguments "cite <arguments> --library library"
    Then the exit code is <code>
    And standard error contains "<message>"
    And the folder "library" equals the snapshot

    Examples:
      | arguments                                             | code | message                  |
      | 01J9Z3K0000000000000000003 'x' --locator page=1       | 1    | no cite key              |
      | 01J9Z3K0000000000000000002 '  '                       | 2    | quote                    |
      | 01J9Z3K0000000000000000002 'x' --locator 'chapter 2'  | 2    | page=N                   |
      | 01J9Z3K0000000000000000002 'x' --locator page=1 --file gone.pdf | 1 | gone.pdf         |
      | 01J9Z3K9000000000000000009 'x'                        | 1    | no work matches          |
