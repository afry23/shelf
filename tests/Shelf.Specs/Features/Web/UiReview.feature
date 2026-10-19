@ui @PRD-8.2 @F-IMP-7 @M5
Feature: The review queue in the browser
  As a person whose import was unsure about some files
  I want to see each uncertain file and decide it
  So that nothing stays filed wrongly

  Background:
    Given an empty working directory
    And I have run shelf with arguments "init library"
    And a sample EPUB "library/Ibryam, Bilgin/Kubernetes Patterns (2019)/kp.epub" with the title "Kubernetes Patterns", the authors "Bilgin Ibryam" and the text seed 1
    And the sidecar "library/Ibryam, Bilgin/Kubernetes Patterns (2019)/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K1000000000000000001", "title": "Kubernetes Patterns", "authors": ["Bilgin Ibryam"], "year": 2019, "files": [{ "name": "kp.epub" }] }
      """
    And a sample PDF "library/Ibryam, Bilgin/Kubernetes Patterns/kp.pdf" with the title "Kubernetes Patterns", the author "B. Ibryam" and the text seed 1
    And the sidecar "library/Ibryam, Bilgin/Kubernetes Patterns/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K3000000000000000003", "title": "Kubernetes Patterns", "authors": ["B. Ibryam"], "files": [{ "name": "kp.pdf" }] }
      """
    And a sample PDF "library/Ålander, Jördis/Event Sourcing in Practice/es.pdf" with the title "Event Sourcing in Practice", the author "Jördis Ålander" and the text seed 2
    And the sidecar "library/Ålander, Jördis/Event Sourcing in Practice/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K2000000000000000002", "title": "Event Sourcing in Practice", "authors": ["Jördis Ålander"], "files": [{ "name": "es.pdf" }] }
      """
    And I have run shelf with arguments "reindex --library library"


  Scenario: An empty queue says so
    Given the web server is running on the library "library"
    When I open "/review" in the browser
    Then the page shows the text "Nothing to review"

  Scenario: The header leads to the review
    Given the web server is running on the library "library"
    And I open "/library" in the browser
    When I click the "Review" link
    Then the browser address contains "/review"

  Scenario: An uncertain file is shown with the books it may belong to
    Given the review queue of "library" gets a grouping entry for the work "01J9Z3K3000000000000000003" about "Ibryam, Bilgin/Kubernetes Patterns/kp.pdf" with the candidate "01J9Z3K1000000000000000001" at 0.6 because "same title and author, year differs"
    And the web server is running on the library "library"
    When I open "/review" in the browser
    Then the page shows the text "1 entry to decide"
    And the page shows the text "Ibryam, Bilgin/Kubernetes Patterns/kp.pdf"
    And the page shows the text "Kubernetes Patterns (2019)"
    And the page shows the text "60 %"
    And the page shows the text "same title and author, year differs"

  Scenario: A merge needs a second click, and then happens
    Given the review queue of "library" gets a grouping entry for the work "01J9Z3K3000000000000000003" about "Ibryam, Bilgin/Kubernetes Patterns/kp.pdf" with the candidate "01J9Z3K1000000000000000001" at 0.6 because "same title and author, year differs"
    And the web server is running on the library "library"
    And I open "/review" in the browser
    When I click the "Merge into Kubernetes Patterns (2019)" button
    Then the page shows the text "Confirm: merge into this book"
    And the sidecar "library/Ibryam, Bilgin/Kubernetes Patterns (2019)/book.json" lists the files "kp.epub"
    When I click the "Confirm: merge into this book" button
    Then the page shows the text "Nothing to review"
    And the page shows the text "cannot be undone"
    And the sidecar "library/Ibryam, Bilgin/Kubernetes Patterns (2019)/book.json" lists the files "kp.epub, kp.pdf"

  Scenario: Cancel keeps the files where they are
    Given the review queue of "library" gets a grouping entry for the work "01J9Z3K3000000000000000003" about "Ibryam, Bilgin/Kubernetes Patterns/kp.pdf" with the candidate "01J9Z3K1000000000000000001" at 0.6 because "same title and author, year differs"
    And the web server is running on the library "library"
    And I open "/review" in the browser
    And I click the "Merge into Kubernetes Patterns (2019)" button
    When I click the "Cancel" button
    Then the page does not show the text "Confirm: merge into this book"
    And the sidecar "library/Ibryam, Bilgin/Kubernetes Patterns (2019)/book.json" lists the files "kp.epub"

  Scenario: Keeping apart is remembered
    Given the review queue of "library" gets a grouping entry for the work "01J9Z3K3000000000000000003" about "Ibryam, Bilgin/Kubernetes Patterns/kp.pdf" with the candidate "01J9Z3K1000000000000000001" at 0.6 because "same title and author, year differs"
    And the web server is running on the library "library"
    And I open "/review" in the browser
    When I click the "Keep apart" button
    Then the page shows the text "Nothing to review"
    And the page shows the text "not suggested again"
    And the sidecar "library/Ibryam, Bilgin/Kubernetes Patterns (2019)/book.json" read back marks 1 work as not duplicates

  Scenario: An entry that is no grouping is dismissed
    Given the review queue of "library" gets an entry of the kind "extraction-error" about "Ålander, Jördis/Event Sourcing in Practice/es.pdf" with the message "bad-pdf: not a PDF"
    And the web server is running on the library "library"
    And I open "/review" in the browser
    Then the page shows the text "bad-pdf: not a PDF"
    When I click the "Dismiss" button
    Then the page shows the text "Nothing to review"
