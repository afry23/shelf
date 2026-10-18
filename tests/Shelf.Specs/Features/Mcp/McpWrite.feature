@PRD-7.2 @M3
Feature: Tagging through MCP
  As an AI assistant
  I want to add tags to works and remove them again
  So that I can help to keep the library in order

  Background:
    Given an empty working directory
    And I have run shelf with arguments "init library"
    And a sample EPUB "library/Ibryam/Kubernetes Patterns/kp.epub" with the title "Kubernetes Patterns", the authors "Bilgin Ibryam" and the chapters:
      | title                 | text                                                                                       |
      | Foundational Patterns | The sidecar container extends the main container. Health probes tell the platform when a pod is alive. |
      | Behavioral Patterns   | A batch job runs to completion. A daemon service runs on every node of the cluster.        |
    And the sidecar "library/Ibryam/Kubernetes Patterns/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000001", "title": "Kubernetes Patterns", "subtitle": "Reusable Elements", "authors": ["Bilgin Ibryam"], "language": "en", "year": 2019, "description": "Design patterns for containers", "tags": ["cloud/kubernetes", "devops"], "readStatus": "read", "files": [{ "name": "kp.epub" }] }
      """
    And a sample PDF "library/Alander/Event Sourcing in Practice/es.pdf" with the title "Event Sourcing in Practice", the author "Jördis Ålander" and the pages:
      | text                                                                                          |
      | Event sourcing stores every change as an event. Aggregates replay events to rebuild their state. |
      | A projection builds a read model from the event stream. Kubernetes is only mentioned once here. |
    And the sidecar "library/Alander/Event Sourcing in Practice/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000002", "title": "Event Sourcing in Practice", "authors": ["Jördis Ålander"], "language": "en", "year": 2021, "tags": ["architektur/ddd", "cloud/azure"], "files": [{ "name": "es.pdf" }] }
      """
    And a sample EPUB "library/Weiss/Verteilte Systeme/vs.epub" with the title "Verteilte Systeme verstehen", the authors "Lukas Weiß" and the chapters:
      | title   | text                                                                      |
      | Konsens | Ein Konsensprotokoll sorgt dafür, dass alle Knoten denselben Zustand sehen. |
    And the sidecar "library/Weiss/Verteilte Systeme/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000003", "title": "Verteilte Systeme verstehen", "authors": ["Lukas Weiß"], "language": "de", "year": 2015, "tags": ["architektur/verteilte-systeme", "architektur/ddd"], "files": [{ "name": "vs.epub" }] }
      """
    And I have run shelf with arguments "reindex --library library"
    And the MCP server is running on the library "library"


  Scenario: add_tags adds tags to a work and says what the work has now
    When I call the tool "add_tags" with the arguments:
      """
      { "id": "01J9Z3K0000000000000000002", "tags": ["lesen/bald", "event-sourcing"] }
      """
    Then the tool call succeeds
    And the result has the string "id" equal to "01J9Z3K0000000000000000002"
    And the result has the string "title" equal to "Event Sourcing in Practice"
    And the result has the number "changed" equal to 2
    And the result has the array "tags" with 4 items
    And the file "library/Alander/Event Sourcing in Practice/book.json" contains "lesen/bald"

  Scenario: The index knows the new tag at once
    Given I call the tool "add_tags" with the arguments:
      """
      { "id": "01J9Z3K0000000000000000002", "tags": ["lesen/bald"] }
      """
    When I call the tool "search_books" with the arguments:
      """
      { "query": "tag:lesen" }
      """
    Then the result has the number "total" equal to 1
    And the result has the string "results[0].title" equal to "Event Sourcing in Practice"

  Scenario: Adding a tag that the work has changes nothing
    When I call the tool "add_tags" with the arguments:
      """
      { "id": "01J9Z3K0000000000000000002", "tags": ["architektur/ddd"] }
      """
    Then the result has the number "changed" equal to 0
    And the result has the array "tags" with 2 items

  Scenario: Tags are cleaned up like on the command line
    When I call the tool "add_tags" with the arguments:
      """
      { "id": "01J9Z3K0000000000000000002", "tags": [" lesen / bald "] }
      """
    Then the result has the string "tags[2]" equal to "lesen/bald"

  Scenario: remove_tags removes exactly the tags named, and the tags below stay
    When I call the tool "remove_tags" with the arguments:
      """
      { "id": "01J9Z3K0000000000000000001", "tags": ["cloud"] }
      """
    Then the result has the number "changed" equal to 0
    When I call the tool "remove_tags" with the arguments:
      """
      { "id": "01J9Z3K0000000000000000001", "tags": ["cloud/kubernetes"] }
      """
    Then the result has the number "changed" equal to 1
    And the result has the array "tags" with 1 items
    And the result has the string "tags[0]" equal to "devops"

  Scenario: A change through MCP can be undone with shelf undo
    Given I call the tool "add_tags" with the arguments:
      """
      { "id": "01J9Z3K0000000000000000002", "tags": ["lesen/bald"] }
      """
    When I run shelf with arguments "undo --library library"
    Then the exit code is 0
    And standard output contains "Reverted the last tag add run"
    And the file "library/Alander/Event Sourcing in Practice/book.json" does not contain "lesen/bald"

  Scenario Outline: The tag tools refuse what they cannot do
    When I call the tool "<tool>" with the arguments:
      """
      <arguments>
      """
    Then the tool call fails with the message containing "<message>"

    Examples:
      | tool        | arguments                                                    | message                       |
      | add_tags    | { "id": "01J9Z3K0000000000000000002", "tags": [] }           | at least one tag              |
      | add_tags    | { "id": "01J9Z3K0000000000000000002", "tags": ["a//b"] }     | tag 'a//b': has an empty part |
      | add_tags    | { "id": "01J9Z3K0000000000000000002", "tags": [" "] }        | must not be empty             |
      | add_tags    | { "id": "01ZZZZZZZZZZZZZZZZZZZZZZZZ", "tags": ["x"] }       | work                          |
      | remove_tags | { "id": "01J9Z3K0000000000000000002", "tags": [] }           | at least one tag              |
      | remove_tags | { "id": "01ZZZZZZZZZZZZZZZZZZZZZZZZ", "tags": ["x"] }       | work                          |

  Scenario: A tag that is refused leaves the work as it was
    Given I take a snapshot of the folder "library"
    When I call the tool "add_tags" with the arguments:
      """
      { "id": "01J9Z3K0000000000000000002", "tags": ["fine", "a//b"] }
      """
    Then the tool call fails with the message containing "has an empty part"
    And the folder "library" equals the snapshot

  Scenario: The tag tools change nothing but tags
    Given I call the tool "add_tags" with the arguments:
      """
      { "id": "01J9Z3K0000000000000000002", "tags": ["lesen/bald"] }
      """
    When I call the tool "get_book" with the arguments:
      """
      { "id": "01J9Z3K0000000000000000002" }
      """
    Then the result has the string "title" equal to "Event Sourcing in Practice"
    And the result has the string "files[0].name" equal to "es.pdf"
    And the file "library/Alander/Event Sourcing in Practice/es.pdf" exists

  Scenario: The tools that change something say so in their annotations
    Then the tool "add_tags" is not read-only and not destructive
    And the tool "remove_tags" is not read-only and not destructive
