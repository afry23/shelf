@ui @F-LNK-7 @M22
Feature: Links in the browser
  As a person who looks at one work
  I want to see the works I linked with it and to link more
  So that I can follow my own connections with a click

  Background:
    Given an empty working directory
    And I have run shelf with arguments "init library"
    And a sample PDF "library/A/Kubernetes/kp.pdf" with the title "Kubernetes Patterns", the author "Bilgin Ibryam" and the pages:
      | text |
      | One. |
    And the sidecar "library/A/Kubernetes/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000001", "title": "Kubernetes Patterns", "authors": ["Bilgin Ibryam"], "language": "en", "citeKey": "ibryam2019", "files": [{ "name": "kp.pdf" }] }
      """
    And a sample PDF "library/B/Attention/at.pdf" with the title "Attention Is All You Need", the author "Ashish Vaswani" and the pages:
      | text |
      | Two. |
    And the sidecar "library/B/Attention/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000002", "kind": "paper", "title": "Attention Is All You Need", "authors": ["Ashish Vaswani"], "language": "en", "files": [{ "name": "at.pdf" }] }
      """
    And a sample PDF "library/C/Containers/ct.pdf" with the title "Container Security", the author "Liz Rice" and the pages:
      | text  |
      | Three. |
    And the sidecar "library/C/Containers/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000003", "title": "Container Security", "authors": ["Liz Rice"], "language": "en", "files": [{ "name": "ct.pdf" }] }
      """
    And I have run shelf with arguments "reindex --library library"
    And the sidecar "library/A/Kubernetes/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000001", "title": "Kubernetes Patterns", "authors": ["Bilgin Ibryam"], "language": "en", "links": [{ "work": "01J9Z3K0000000000000000002", "note": "background reading" }], "files": [{ "name": "kp.pdf" }] }
      """
    And the sidecar "library/B/Attention/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000002", "kind": "paper", "title": "Attention Is All You Need", "authors": ["Ashish Vaswani"], "language": "en", "links": [{ "work": "01J9Z3K0000000000000000001" }], "files": [{ "name": "at.pdf" }] }
      """
    And I have run shelf with arguments "reindex --library library"
    And the web server is running on the library "library"

  Scenario: The links have a tab that sits right next to the Files tab
    When I open "/works/01J9Z3K0000000000000000001" in the browser
    Then the "Links" tab is shown
    And the "Links" tab comes right after the "Files" tab
    And the tab "Links" shows the number 1

  Scenario: The tab lists the linked works with their notes
    When I open "/works/01J9Z3K0000000000000000001#links" in the browser
    Then the list "Links" has 1 items
    And the page shows the text "Attention Is All You Need"
    And the "Note" field has the value "background reading"
    And the browser reported no errors

  Scenario: A click on a linked work opens that work
    Given I open "/works/01J9Z3K0000000000000000001#links" in the browser
    When I click the "Attention Is All You Need" link
    Then the browser address contains "/works/01J9Z3K0000000000000000002"
    And the heading "Attention Is All You Need" is shown

  Scenario: A work without links still has the tab, to make the first link
    When I open "/works/01J9Z3K0000000000000000003#links" in the browser
    Then the "Links" tab is shown
    And the page shows the text "No links yet"

  Scenario: A link is removed
    Given I open "/works/01J9Z3K0000000000000000001#links" in the browser
    When I click the "Remove link" button
    Then the page shows the text "No links yet"
    And the file "library/A/Kubernetes/book.json" soon does not contain "links"
    And the file "library/B/Attention/book.json" soon does not contain "links"

  Scenario: A work is found by its title and linked with a note
    Given I open "/works/01J9Z3K0000000000000000003#links" in the browser
    When I fill the "Find a work to link" field with "Kubernetes"
    And I click the "Kubernetes Patterns" button
    And I fill the "Note" field with "runtime security"
    And I click the "Link" button
    Then the list "Links" has 1 items
    And the "Note" field has the value "runtime security"
    And the file "library/C/Containers/book.json" soon contains "runtime security"
    And the file "library/A/Kubernetes/book.json" soon contains "01J9Z3K0000000000000000003"

  Scenario: A note is changed
    Given I open "/works/01J9Z3K0000000000000000001#links" in the browser
    When I fill the "Note" field with "required reading"
    And I click the "Save note" button
    Then the file "library/A/Kubernetes/book.json" soon contains "required reading"
