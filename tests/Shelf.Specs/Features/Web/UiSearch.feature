@ui @F-SRC-5 @F-SEM-5 @PRD-8.2 @M4
Feature: Searching from the browser
  As a person who remembers a word or an idea from a book
  I want to search the whole library and land on the right page
  So that I do not have to remember which book it was

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

  Scenario: The search field of the header searches the library
    Given I open "/library" in the browser
    When I fill the "Search your library" field with "sidecar"
    And I press Enter in the "Search your library" field
    Then the browser address contains "/search?q=sidecar"
    And the list "Results" has 1 items
    And the first item of the list "Results" is "Kubernetes Patterns"

  Scenario: A hit shows the passage with the word marked, and leads to the place in an EPUB
    When I open "/search?q=sidecar" in the browser
    Then the text "sidecar" is highlighted
    And the link "Open at section 1" points to an address containing "/read/01J9Z3K0000000000000000001~kp.epub#cfi=epubcfi("

  Scenario: A hit in a PDF leads to its page
    When I open "/search?q=projection" in the browser
    Then the first item of the list "Results" is "Event Sourcing in Practice"
    And the link "Open at page 2" points to an address containing "/read/01J9Z3K0000000000000000002~es.pdf#page=2"

  Scenario: The search can be repeated with another word on the page itself
    Given I open "/search?q=sidecar" in the browser
    When I fill the "Search your library" field with "projection"
    And I press Enter in the "Search your library" field
    Then the first item of the list "Results" is "Event Sourcing in Practice"

  Scenario: Filters of the query language work in the search
    When I open "/search?q=event+lang%3Aen+year%3A%3E%3D2020" in the browser
    Then the list "Results" has 1 items
    And the first item of the list "Results" is "Event Sourcing in Practice"

  Scenario: Searching by meaning finds books by what they are about
    Given I open "/search?q=kubernetes+containers" in the browser
    When I click the "Meaning" button
    Then the "Meaning" button is pressed
    And the browser address contains "mode=semantic"
    And the first item of the list "Results" is "Kubernetes Patterns"
    And the list "Results" has 3 items

  Scenario: Searching by meaning can look for passages
    Given I open "/search?q=projection+read+model&mode=semantic" in the browser
    When I click the "Passages" button
    Then the browser address contains "level=chunk"
    And the first item of the list "Results" is "Event Sourcing in Practice"
    And the link "Open at page 2" points to an address containing "#page=2"

  Scenario: Both ways at once
    Given I open "/search?q=sidecar+container" in the browser
    When I click the "Both" button
    Then the browser address contains "mode=hybrid"
    And the first item of the list "Results" is "Kubernetes Patterns"

  Scenario: The mode is kept when the page is opened again
    When I open "/search?q=kubernetes&mode=semantic" in the browser
    Then the "Meaning" button is pressed

  Scenario: The books can be searched for passages only with a model
    Given I have run shelf with arguments "reindex --library library"
    And I open "/search?q=kubernetes&mode=semantic" in the browser
    Then the page shows the text "no embeddings yet"
    And the page shows the text "shelf reindex --embeddings"

  Scenario: Nothing found
    When I open "/search?q=zzzzzzzz" in the browser
    Then the page shows the text "Nothing found for zzzzzzzz"

  Scenario: A query that the server refuses is explained
    When I open "/search?q=year%3Aabc" in the browser
    Then an alert says "year"

  Scenario: An empty search asks for a word
    When I open "/search" in the browser
    Then the page shows the text "Type a word or a phrase"
