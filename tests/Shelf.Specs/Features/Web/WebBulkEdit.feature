@F-BLK-3 @M20
Feature: Changing the metadata of many works over HTTP
  As a script or a later bulk dialog of the web interface
  I want to preview and apply one change to the works that filters select
  So that the rules are the same as on the command line

  Background:
    Given an empty working directory
    And I have run shelf with arguments "init library"
    And a sample PDF "library/Ibryam/Kubernetes Patterns/kp.pdf" with the title "Kubernetes Patterns", the author "Bilgin Ibryam" and the text seed 1
    And the sidecar "library/Ibryam/Kubernetes Patterns/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000001", "title": "Kubernetes Patterns", "authors": ["Bilgin Ibryam"], "publisher": "O'Reilly Media", "year": 2019, "tags": ["cloud/kubernetes"], "files": [{ "name": "kp.pdf" }] }
      """
    And a sample PDF "library/Mueller/Cloud Native/cn.pdf" with the title "Cloud Native", the author "Anna Müller" and the text seed 2
    And the sidecar "library/Mueller/Cloud Native/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000004", "title": "Cloud Native", "authors": ["Anna Müller"], "publisher": "O'Reilly Media", "year": 2017, "tags": ["cloud/aws"], "files": [{ "name": "cn.pdf" }] }
      """
    And a sample PDF "library/Alander/Event Sourcing in Practice/es.pdf" with the title "Event Sourcing in Practice", the author "Jördis Ålander" and the text seed 3
    And the sidecar "library/Alander/Event Sourcing in Practice/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000002", "title": "Event Sourcing in Practice", "authors": ["Jördis Ålander"], "publisher": "Manning", "year": 2021, "language": "en", "tags": ["architektur/ddd"], "files": [{ "name": "es.pdf" }] }
      """
    And a sample PDF "library/Weiss/Verteilte Systeme/vs.pdf" with the title "Verteilte Systeme verstehen", the author "Lukas Weiß" and the text seed 4
    And the sidecar "library/Weiss/Verteilte Systeme/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000003", "title": "Verteilte Systeme verstehen", "authors": ["Lukas Weiß"], "publisher": "dpunkt", "year": 2015, "language": "de", "tags": ["architektur/ddd"], "files": [{ "name": "vs.pdf" }] }
      """
    And I have run shelf with arguments "reindex --library library"
    And the web server is running on the library "library"

  Scenario: Without apply the change is only previewed
    Given I take a snapshot of the folder "library"
    When I send "POST /api/v1/works/bulk-edit" with the JSON:
      """
      { "queries": ["publisher:Reilly"], "set": { "language": "en" } }
      """
    Then the response status is 200
    And the JSON field "applied" of the response is false
    And the JSON field "matched" of the response is 2
    And the JSON array "works" of the response has 2 entries
    And the response body contains "Kubernetes Patterns"
    And the folder "library" equals the snapshot

  Scenario: With apply the change is written for the works of all queries
    When I send "POST /api/v1/works/bulk-edit" with the JSON:
      """
      { "queries": ["publisher:Manning", "publisher:dpunkt"], "set": { "publisher": "Fachbuch", "rating": 5 }, "apply": true }
      """
    Then the response status is 200
    And the JSON field "changed" of the response is 2
    And the file "library/Alander/Event Sourcing in Practice/book.json" contains '"publisher": "Fachbuch"'
    And the file "library/Weiss/Verteilte Systeme/book.json" contains '"rating": 5'
    And the file "library/Ibryam/Kubernetes Patterns/book.json" contains "Reilly"

  Scenario: Tags are added and removed
    When I send "POST /api/v1/works/bulk-edit" with the JSON:
      """
      { "queries": ["publisher:Reilly"], "addTags": ["topic/cloud"], "removeTags": ["cloud/aws"], "apply": true }
      """
    Then the response status is 200
    And the file "library/Mueller/Cloud Native/book.json" contains "topic/cloud"
    And the file "library/Mueller/Cloud Native/book.json" does not contain "cloud/aws"

  Scenario: A field is removed with null
    When I send "POST /api/v1/works/bulk-edit" with the JSON:
      """
      { "queries": ["publisher:dpunkt"], "set": { "language": null }, "apply": true }
      """
    Then the response status is 200
    And the file "library/Weiss/Verteilte Systeme/book.json" does not contain "language"

  Scenario Outline: What cannot be done is a 400 and nothing is written
    Given I take a snapshot of the folder "library"
    When I send "POST /api/v1/works/bulk-edit" with the JSON:
      """
      <body>
      """
    Then the response status is 400
    And the response body contains "<message>"
    And the folder "library" equals the snapshot

    Examples:
      | body                                                                                   | message              |
      | { "set": { "language": "en" }, "apply": true }                                        | queries              |
      | { "queries": [], "set": { "language": "en" }, "apply": true }                         | queries              |
      | { "queries": ["publisher:Reilly"], "apply": true }                                    | nothing to change    |
      | { "queries": ["publisher:Reilly"], "set": { "title": "Same" }, "apply": true }        | title                |
      | { "queries": ["publisher:Reilly"], "set": { "year": "abc" }, "apply": true }          | year                 |
      | { "queries": ["publisher:Reilly"], "set": { "nothing": 1 }, "apply": true }           | nothing              |
      | { "queries": ["status:nothing"], "set": { "language": "en" }, "apply": true }          | status               |
      | { "queries": ["@unknown"], "set": { "language": "en" }, "apply": true }               | no saved search      |
