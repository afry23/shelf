@F-BLK-1 @F-BLK-2 @M20
Feature: Changing the metadata of many works at once
  As a person with a grown library
  I want to select works by filters and set a value for all of them
  So that I do not edit the language or the publisher book by book

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

  Scenario: Without --apply nothing is written, and the changes are shown
    Given I take a snapshot of the folder "library"
    When I run shelf with arguments "edit --query 'publisher:Reilly' --language en --library library"
    Then the exit code is 0
    And standard output contains "Kubernetes Patterns"
    And standard output contains "Cloud Native"
    And standard output contains "language: (none) -> en"
    And standard output does not contain "Manning"
    And standard output contains "--apply"
    And the folder "library" equals the snapshot

  Scenario: With --apply the value is set for every selected work and for no other
    When I run shelf with arguments "edit --query 'publisher:Reilly' --language en --apply --library library"
    Then the exit code is 0
    And standard output contains "2 works"
    And the file "library/Ibryam/Kubernetes Patterns/book.json" contains '"language": "en"'
    And the file "library/Mueller/Cloud Native/book.json" contains '"language": "en"'
    And the file "library/Weiss/Verteilte Systeme/book.json" contains '"language": "de"'
    When I run shelf with arguments "search publisher:Reilly --library library"
    Then the search results are, in any order: "Kubernetes Patterns; Cloud Native"

  Scenario: Several queries are joined by OR and a work found twice is changed once
    When I run shelf with arguments "edit --query 'publisher:Manning' --query 'publisher:dpunkt' --query 'tag:architektur/ddd' --publisher 'Fachbuch' --apply --library library"
    Then the exit code is 0
    And standard output contains "2 works"
    And the file "library/Alander/Event Sourcing in Practice/book.json" contains '"publisher": "Fachbuch"'
    And the file "library/Weiss/Verteilte Systeme/book.json" contains '"publisher": "Fachbuch"'
    And the file "library/Ibryam/Kubernetes Patterns/book.json" contains "Reilly"

  Scenario: Words and filters inside one query are joined by AND
    When I run shelf with arguments "edit --query 'tag:architektur/ddd year:>=2020' --rating 5 --apply --library library"
    Then the exit code is 0
    And standard output contains "1 work"
    And the file "library/Alander/Event Sourcing in Practice/book.json" contains '"rating": 5'
    And the file "library/Weiss/Verteilte Systeme/book.json" does not contain "rating"

  Scenario: A saved search can be the query
    Given I have run shelf with arguments "searches add reilly publisher:Reilly --library library"
    When I run shelf with arguments "edit --query @reilly --series 'Cloud' --apply --library library"
    Then the exit code is 0
    And the file "library/Mueller/Cloud Native/book.json" contains '"series": "Cloud"'

  Scenario: A work that has the value already is not listed and not counted
    When I run shelf with arguments "edit --query 'tag:architektur/ddd' --language de --library library"
    Then the exit code is 0
    And standard output contains "Event Sourcing in Practice"
    And standard output does not contain "Verteilte Systeme verstehen"
    And standard output contains "1 of 2"

  Scenario: Nothing to do is said so
    When I run shelf with arguments "edit --query 'publisher:dpunkt' --language de --apply --library library"
    Then the exit code is 0
    And standard output contains "Nothing to change"

  Scenario: A value is removed with an empty string
    When I run shelf with arguments "edit --query 'publisher:dpunkt' --language '' --apply --library library"
    Then the exit code is 0
    And the file "library/Weiss/Verteilte Systeme/book.json" does not contain "language"

  Scenario: Tags are added and removed for all selected works
    When I run shelf with arguments "edit --query 'publisher:Reilly' --add-tag 'topic/cloud' --remove-tag 'cloud/aws' --apply --library library"
    Then the exit code is 0
    And the file "library/Ibryam/Kubernetes Patterns/book.json" contains "topic/cloud"
    And the file "library/Mueller/Cloud Native/book.json" contains "topic/cloud"
    And the file "library/Mueller/Cloud Native/book.json" does not contain "cloud/aws"
    And the file "library/Ibryam/Kubernetes Patterns/book.json" contains "cloud/kubernetes"

  Scenario: The whole change is undone in one step
    Given I have run shelf with arguments "edit --query 'publisher:Reilly' --language en --apply --library library"
    When I run shelf with arguments "undo --library library"
    Then the exit code is 0
    And the file "library/Ibryam/Kubernetes Patterns/book.json" does not contain "language"
    And the file "library/Mueller/Cloud Native/book.json" does not contain "language"
    When I run shelf with arguments "search publisher:Reilly --library library"
    Then the exit code is 0

  Scenario: The change is available as JSON
    When I run shelf with arguments "edit --query 'publisher:Reilly' --language en --json --library library"
    Then the exit code is 0
    And standard output contains '"applied":false'
    And the JSON integer property "matched" of standard output is 2
    And the JSON property "works" of standard output has 2 items
    And standard output contains '"field":"language"'
    And standard output contains '"to":"en"'

  Scenario Outline: Every field that fits many works can be set
    When I run shelf with arguments "edit --query 'publisher:Reilly' <option> --apply --library library"
    Then the exit code is 0
    And the file "library/Mueller/Cloud Native/book.json" contains '<json>'

    Examples:
      | option                 | json                       |
      | --language en          | "language": "en"           |
      | --publisher Packt      | "publisher": "Packt"       |
      | --series Cloud         | "series": "Cloud"          |
      | --edition '2nd'        | "edition": "2nd"           |
      | --year 2020            | "year": 2020               |
      | --rating 4             | "rating": 4                |
      | --status read          | "readStatus": "read"       |
      | --author 'Team Cloud'  | "Team Cloud"               |

  Scenario Outline: What would give many works the same unique value is refused
    When I run shelf with arguments "edit --query 'publisher:Reilly' <option> --apply --library library"
    Then the exit code is 2
    And standard error contains "<option_name>"
    And standard error contains "--query"

    Examples:
      | option                 | option_name |
      | --title 'Same'         | --title     |
      | --cite-key same2020    | --cite-key  |
      | --isbn 9780596520687   | --isbn      |

  Scenario Outline: A request that cannot be met is refused and nothing is written
    Given I take a snapshot of the folder "library"
    When I run shelf with arguments "edit <arguments> --library library"
    Then the exit code is <code>
    And standard error contains "<message>"
    And the folder "library" equals the snapshot

    Examples:
      | arguments                                                            | code | message                  |
      | --query 'publisher:Reilly' --apply                                  | 2    | nothing to change        |
      | --query 'publisher:Reilly' --year abc --apply                       | 2    | not a year               |
      | --query '' --language en --apply                                    | 2    | nothing to search for    |
      | 01J9Z3K0000000000000000001 --query 'publisher:Reilly' --language en | 2    | either a work or --query |
      | --language en                                                       | 2    | a work or --query        |
      | --query '@nothing' --language en --apply                            | 2    | no saved search          |

  Scenario: A query that finds no work changes nothing
    When I run shelf with arguments "edit --query 'publisher:Nobody' --language en --apply --library library"
    Then the exit code is 0
    And standard output contains "No works match"
