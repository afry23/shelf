@ui @F-TAG-6 @PRD-8.2 @M10
Feature: Tag suggestions in the book view
  As a person who looks at one book
  I want to take over the tags of similar books with a click
  So that I tag a book the way I tagged the ones like it

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
      { "schema": 1, "id": "01J9Z3K0000000000000000002", "title": "Event Sourcing in Practice", "authors": ["Jördis Ålander"], "language": "en", "year": 2021, "tags": ["architektur/ddd"], "files": [{ "name": "es.pdf" }] }
      """
    And a sample EPUB "library/Weiss/Verteilte Systeme/vs.epub" with the title "Verteilte Systeme verstehen", the authors "Lukas Weiß" and the chapters:
      | title   | text                                                           |
      | Konsens | Ein Konsensprotokoll sorgt dafür, dass alle Knoten einig sind. |
    And the sidecar "library/Weiss/Verteilte Systeme/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000003", "title": "Verteilte Systeme verstehen", "authors": ["Lukas Weiß"], "language": "de", "year": 2015, "files": [{ "name": "vs.epub" }] }
      """
    And a sample PDF "library/Mueller/Cloud Patterns/cp.pdf" with the title "Cloud Patterns", the author "Anna Müller" and the pages:
      | text                                         |
      | Serverless functions scale down to zero.     |
    And the sidecar "library/Mueller/Cloud Patterns/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000004", "title": "Cloud Patterns", "subtitle": "Reusable Elements", "authors": ["Anna Müller"], "language": "en", "year": 2020, "description": "Design patterns for containers in the cloud", "tags": ["cloud/aws", "devops"], "files": [{ "name": "cp.pdf" }] }
      """
    And I have run shelf with arguments "reindex --library library"
    And I have run shelf with arguments "reindex --embeddings --library library"
    And the embedder calls are forgotten
    And the web server is running on the library "library"

  Scenario: The tags of similar books are offered
    When I open "/works/01J9Z3K0000000000000000001" in the browser
    Then the page shows the text "Suggested tags"
    And the "Add tag cloud/aws" button is shown
    And the "Add tag architektur/ddd" button is shown
    And the "Add tag devops" button is not shown

  Scenario: A click takes the tag over and the suggestion goes
    Given I open "/works/01J9Z3K0000000000000000001" in the browser
    When I click the "Add tag cloud/aws" button
    Then the "Remove tag cloud/aws" button is shown
    And the "Add tag cloud/aws" button is not shown
    And the file "library/Ibryam/Kubernetes Patterns/book.json" soon contains "cloud/aws"

  Scenario: A book without suggestions has no such section
    Given I send "PUT /api/v1/works/01J9Z3K0000000000000000001/tags" with the JSON:
      """
      { "tags": ["cloud/kubernetes", "devops", "cloud/aws", "architektur/ddd"] }
      """
    When I open "/works/01J9Z3K0000000000000000001" in the browser
    Then the page shows the text "Kubernetes Patterns"
    And the page does not show the text "Suggested tags"

  Scenario: Without embeddings there is no such section
    Given I have run shelf with arguments "reindex --library library"
    When I open "/works/01J9Z3K0000000000000000001" in the browser
    Then the page shows the text "Kubernetes Patterns"
    And the page does not show the text "Suggested tags"
    And the browser reported no errors
