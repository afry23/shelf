@ui @F-SEM-6 @PRD-8.2 @M5
Feature: Similar books in the book view
  As a person who looks at one book
  I want to see the books of the library that are most like it
  So that I can move on to the next one with a click

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
    And the sidecar "library/Ibryam/Kubernetes Patterns/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000001", "title": "Kubernetes Patterns", "subtitle": "Reusable Elements", "authors": ["Bilgin Ibryam"], "language": "en", "year": 2019, "description": "Design patterns for containers", "tags": ["cloud/kubernetes", "devops"], "files": [{ "name": "kp.epub" }] }
      """
    And a sample PDF "library/Alander/Event Sourcing in Practice/es.pdf" with the title "Event Sourcing in Practice", the author "Jördis Ålander" and the pages:
      | text                                                                  |
      | Event sourcing stores every change as an event in an append only log. |
    And the sidecar "library/Alander/Event Sourcing in Practice/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000002", "title": "Event Sourcing in Practice", "authors": ["Jördis Ålander"], "language": "en", "year": 2021, "files": [{ "name": "es.pdf" }] }
      """
    And a sample PDF "library/Mueller/Cloud Patterns/cp.pdf" with the title "Cloud Patterns", the author "Anna Müller" and the pages:
      | text                                     |
      | Serverless functions scale down to zero. |
    And the sidecar "library/Mueller/Cloud Patterns/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000004", "title": "Cloud Patterns", "subtitle": "Reusable Elements", "authors": ["Anna Müller"], "language": "en", "year": 2020, "description": "Design patterns for containers in the cloud", "tags": ["cloud/aws", "devops"], "files": [{ "name": "cp.pdf" }] }
      """
    And I have run shelf with arguments "reindex --library library"
    And I have run shelf with arguments "reindex --embeddings --library library"

  Scenario: The most similar book is listed first, and the book itself is not
    Given the web server is running on the library "library"
    When I open "/works/01J9Z3K0000000000000000001" in the browser
    Then the page shows the text "Similar books"
    And the list "Similar books" has 2 items
    And the first item of the list "Similar books" is "Cloud Patterns"
    And the browser reported no errors

  Scenario: A click on a similar book opens that book
    Given the web server is running on the library "library"
    And I open "/works/01J9Z3K0000000000000000001" in the browser
    When I click the "Cloud Patterns" link
    Then the browser address contains "/works/01J9Z3K0000000000000000004"
    And the heading "Cloud Patterns" is shown
    And the list "Similar books" has 2 items

  Scenario: Without embeddings the book view has no such section
    Given I have run shelf with arguments "reindex --library library"
    And the web server is running on the library "library"
    When I open "/works/01J9Z3K0000000000000000001" in the browser
    Then the heading "Kubernetes Patterns" is shown
    And the page does not show the text "Similar books"
    And the browser reported no errors
