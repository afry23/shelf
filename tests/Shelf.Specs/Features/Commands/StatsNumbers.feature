@PRD-7.1 @M1
Feature: Numbers in the statistics of the library
  As the owner of a library
  I want the statistics to count works, files, formats, languages and the review queue correctly
  So that I can trust the overview

  Background:
    Given an empty working directory
    And I have run shelf with arguments "init library"
    And a sample EPUB "library/Ibryam, Bilgin/Kubernetes Patterns (2019)/kp.epub" with the title "Kubernetes Patterns", the authors "Bilgin Ibryam" and the text seed 1
    And a sample PDF "library/Ibryam, Bilgin/Kubernetes Patterns (2019)/kp.pdf" with the title "Kubernetes Patterns", the author "Bilgin Ibryam" and the text seed 1
    And the sidecar "library/Ibryam, Bilgin/Kubernetes Patterns (2019)/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K1000000000000000001", "title": "Kubernetes Patterns", "authors": ["Bilgin Ibryam"], "language": "en", "year": 2019, "tags": ["cloud/kubernetes", "devops"], "readStatus": "reading", "files": [{ "name": "kp.epub" }, { "name": "kp.pdf" }] }
      """
    And a sample PDF "library/Ålander, Jördis/Event Sourcing in Practice/es.pdf" with the title "Event Sourcing in Practice", the author "Jördis Ålander" and the text seed 2
    And the sidecar "library/Ålander, Jördis/Event Sourcing in Practice/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K2000000000000000002", "title": "Event Sourcing in Practice", "authors": ["Jördis Ålander"], "language": "en", "tags": ["devops"], "files": [{ "name": "es.pdf" }] }
      """
    And a sample EPUB "library/Weiß, Lukas/Verteilte Systeme/vs.epub" with the title "Verteilte Systeme verstehen", the authors "Lukas Weiß" and the text seed 3
    And the sidecar "library/Weiß, Lukas/Verteilte Systeme/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K3000000000000000003", "title": "Verteilte Systeme verstehen", "authors": ["Lukas Weiß"], "language": "de", "tags": ["architektur"], "readStatus": "read", "files": [{ "name": "vs.epub" }] }
      """
    And the review queue of "library" gets a grouping entry for the work "01J9Z3K2000000000000000002" about "Ålander, Jördis/Event Sourcing in Practice/es.pdf" with the candidate "01J9Z3K1000000000000000001" at 0.6 because "same title"
    And the review queue of "library" gets an entry of the kind "extraction-error" about "incoming/broken.epub" with the message "bad-epub: broken"
    And I have run shelf with arguments "reindex --library library"

  Scenario Outline: The numbers of a library with three works
    When I run shelf with arguments "stats --library library"
    Then the exit code is 0
    And standard output matches the pattern "<pattern>"

    Examples:
      | pattern                                    |
      | (?m)^Works +3$                             |
      | (?m)^Files +4$                             |
      | (?m)^Authors +3$                           |
      | (?m)^Tags +3$                              |
      | (?m)^Size +[0-9.]+ (KB\|MB)$               |
      | (?m)^  epub +2 +[0-9.]+ (B\|KB\|MB)$       |
      | (?m)^  pdf +2 +[0-9.]+ (B\|KB\|MB)$        |
      | (?m)^  unread +1$                          |
      | (?m)^  reading +1$                         |
      | (?m)^  read +1$                            |
      | (?m)^  en +2$                              |
      | (?m)^  de +1$                              |
      | (?m)^Review queue +2$                      |
      | (?m)^  grouping +1$                        |
      | (?m)^  extraction-error +1$                |

  Scenario: The same numbers in JSON
    When I run shelf with arguments "stats --library library --json"
    Then the exit code is 0
    And the JSON of standard output has the number "works" equal to 3
    And the JSON of standard output has the number "files" equal to 4
    And the JSON of standard output has the number "authors" equal to 3
    And the JSON of standard output has the number "tags" equal to 3
    And the JSON of standard output has the string "formats[0].format" equal to "epub"
    And the JSON of standard output has the number "formats[0].files" equal to 2
    And the JSON of standard output has the string "formats[1].format" equal to "pdf"
    And the JSON of standard output has the number "readStatus.unread" equal to 1
    And the JSON of standard output has the number "readStatus.reading" equal to 1
    And the JSON of standard output has the number "readStatus.read" equal to 1
    And the JSON of standard output has the string "languages[0].language" equal to "en"
    And the JSON of standard output has the number "languages[0].works" equal to 2
    And the JSON of standard output has the number "reviewQueue.total" equal to 2
    And the JSON of standard output has the number "reviewQueue.grouping" equal to 1
    And the JSON of standard output has the number "reviewQueue.extractionError" equal to 1
    And the JSON of standard output has the number "reviewQueue.weakMetadata" equal to 0
