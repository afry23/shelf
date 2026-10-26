@PRD-7.1 @F-LIB-5 @M1
Feature: Showing a work
  As a user of the library
  I want to see everything shelf knows about one work
  So that I can check its metadata, its files and where I stopped reading

  Background:
    Given an empty working directory
    And I have run shelf with arguments "init library"
    And a sample EPUB "library/Ibryam, Bilgin/Kubernetes Patterns (2019)/kp.epub" with the title "Kubernetes Patterns", the authors "Bilgin Ibryam" and the text seed 1
    And a sample PDF "library/Ibryam, Bilgin/Kubernetes Patterns (2019)/kp.pdf" with the title "Kubernetes Patterns", the author "Bilgin Ibryam" and the text seed 1
    And the sidecar "library/Ibryam, Bilgin/Kubernetes Patterns (2019)/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K1000000000000000001", "title": "Kubernetes Patterns", "subtitle": "Reusable Elements for Cloud Native Applications", "authors": ["Bilgin Ibryam", "Roland Huss"], "language": "en", "year": 2019, "isbn13": "9781492050285", "publisher": "O'Reilly", "series": "Patterns", "seriesIndex": 2, "edition": "1st", "citeKey": "ibryam2019", "tags": ["cloud/kubernetes", "devops"], "rating": 4, "readStatus": "reading", "added": "2026-10-01T09:30:00Z", "description": "A catalog of reusable patterns.", "files": [{ "name": "kp.epub", "progress": { "locator": "epubcfi(/6/14!/4/2/10)", "updatedAt": "2026-10-12T19:04:00Z" } }, { "name": "kp.pdf" }] }
      """
    And a sample PDF "library/Ålander, Jördis/Event Sourcing in Practice/es.pdf" with the title "Event Sourcing in Practice", the author "Jördis Ålander" and the text seed 2
    And the sidecar "library/Ålander, Jördis/Event Sourcing in Practice/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K2000000000000000002", "title": "Event Sourcing in Practice", "authors": ["Jördis Ålander"], "files": [{ "name": "es.pdf" }] }
      """
    And I have run shelf with arguments "reindex --library library"

  Scenario: The details of a work are shown one per line
    When I run shelf with arguments "show 01J9Z3K1000000000000000001 --library library"
    Then the exit code is 0
    And standard output matches the pattern "(?m)^Kubernetes Patterns$"
    And standard output matches the pattern "(?m)^ID +01J9Z3K1000000000000000001$"
    And standard output matches the pattern "(?m)^Subtitle +Reusable Elements for Cloud Native Applications$"
    And standard output matches the pattern "(?m)^Authors +Bilgin Ibryam, Roland Huss$"
    And standard output matches the pattern "(?m)^Year +2019$"
    And standard output matches the pattern "(?m)^Language +en$"
    And standard output matches the pattern "(?m)^ISBN-13 +9781492050285$"
    And standard output matches the pattern "(?m)^Publisher +O'Reilly$"
    And standard output matches the pattern "(?m)^Series +Patterns #2$"
    And standard output matches the pattern "(?m)^Edition +1st$"
    And standard output matches the pattern "(?m)^Cite key +ibryam2019$"
    And standard output matches the pattern "(?m)^Tags +cloud/kubernetes, devops$"
    And standard output matches the pattern "(?m)^Rating +4/5$"
    And standard output matches the pattern "(?m)^Status +reading$"
    And standard output matches the pattern "(?m)^Added +2026-10-01$"
    And standard output matches the pattern "(?m)^Folder +Ibryam, Bilgin/Kubernetes Patterns \(2019\)$"
    And standard output matches the pattern "(?m)^A catalog of reusable patterns\.$"
    And standard error is empty

  Scenario: Files are listed with their size and the reading position
    When I run shelf with arguments "show 01J9Z3K1000000000000000001 --library library"
    Then standard output matches the pattern "(?m)^Files$"
    And standard output matches the pattern "(?m)^  kp\.epub +[0-9.]+ (B|KB|MB)  reading position epubcfi\(/6/14!/4/2/10\)$"
    And standard output matches the pattern "(?m)^  kp\.pdf +[0-9.]+ (B|KB|MB)$"

  Scenario: Fields a work does not have are left out
    When I run shelf with arguments "show 01J9Z3K2000000000000000002 --library library"
    Then the exit code is 0
    And standard output matches the pattern "(?m)^Event Sourcing in Practice$"
    And standard output matches the pattern "(?m)^Authors +Jördis Ålander$"
    And standard output matches the pattern "(?m)^Status +unread$"
    And standard output does not contain "Subtitle"
    And standard output does not contain "Publisher"
    And standard output does not contain "Cite key"
    And standard output does not contain "ISBN"
    And standard output does not contain "Rating"
    And standard output does not contain "Tags"
    And standard output does not contain "Series"
    And standard output does not contain "Year"

  Scenario Outline: A work is found by its ID, a unique start of its ID, or the path of its folder or one of its files
    When I run shelf with arguments "show '<reference>' --library library"
    Then the exit code is 0
    And standard output matches the pattern "(?m)^ID +01J9Z3K1000000000000000001$"

    Examples:
      | reference                                                       | how                                       |
      | 01J9Z3K1000000000000000001                                      | the whole ID                              |
      | 01j9z3k1000000000000000001                                      | the ID in lower case                      |
      | 01J9Z3K1                                                        | a start of the ID that no other work has  |
      | library/Ibryam, Bilgin/Kubernetes Patterns (2019)               | the folder, relative to the working folder |
      | library/Ibryam, Bilgin/Kubernetes Patterns (2019)/kp.pdf        | a file of the work                        |
      | Ibryam, Bilgin/Kubernetes Patterns (2019)                       | the folder, relative to the library       |

  Scenario: Inside a work folder the dot stands for the work
    Given I am in the folder "library/Ibryam, Bilgin/Kubernetes Patterns (2019)"
    When I run shelf with arguments "show ."
    Then the exit code is 0
    And standard output matches the pattern "(?m)^ID +01J9Z3K1000000000000000001$"

  Scenario: The JSON form carries every field and the files
    When I run shelf with arguments "show 01J9Z3K1000000000000000001 --library library --json"
    Then the exit code is 0
    And the JSON of standard output has the string "id" equal to "01J9Z3K1000000000000000001"
    And the JSON of standard output has the string "title" equal to "Kubernetes Patterns"
    And the JSON of standard output has the string "subtitle" equal to "Reusable Elements for Cloud Native Applications"
    And the JSON of standard output has the string "authors[1]" equal to "Roland Huss"
    And the JSON of standard output has the number "year" equal to 2019
    And the JSON of standard output has the string "language" equal to "en"
    And the JSON of standard output has the string "isbn13" equal to "9781492050285"
    And the JSON of standard output has the string "series" equal to "Patterns"
    And the JSON of standard output has the number "rating" equal to 4
    And the JSON of standard output has the string "readStatus" equal to "reading"
    And the JSON of standard output has the string "tags[0]" equal to "cloud/kubernetes"
    And the JSON of standard output has the string "folder" equal to "Ibryam, Bilgin/Kubernetes Patterns (2019)"
    And the JSON of standard output has the string "files[0].name" equal to "kp.epub"
    And the JSON of standard output has the string "files[0].format" equal to "epub"
    And the JSON of standard output has the string "files[0].progress.locator" equal to "epubcfi(/6/14!/4/2/10)"
    And the JSON of standard output has the string "files[1].name" equal to "kp.pdf"
    And the JSON property "files" of standard output has 2 items

  Scenario: The JSON form of a sparse work has only what it has
    When I run shelf with arguments "show 01J9Z3K2000000000000000002 --library library --json"
    Then the exit code is 0
    And the JSON of standard output has the string "title" equal to "Event Sourcing in Practice"
    And the JSON of standard output has the string "readStatus" equal to "unread"
    And standard output does not contain "subtitle"
    And standard output does not contain "rating"

  Scenario Outline: A reference that names no work is an error
    When I run shelf with arguments "show '<reference>' --library library"
    Then the exit code is 1
    And standard error starts with "error: <message>"
    And standard output is empty

    Examples:
      | reference                             | message                                      |
      | 01J9Z3K9000000000000000009            | no work matches '01J9Z3K9000000000000000009' |
      | library/Nobody/Nothing                | no work matches 'library/Nobody/Nothing'     |
      | library                               | no work matches 'library'                    |
      | 01J9Z3K                               | '01J9Z3K' matches 2 works                    |

  Scenario: Without a reference the command explains its usage
    When I run shelf with arguments "show --library library"
    Then the exit code is 2
    And standard error contains "Required argument missing"

  Scenario: Without an index the command asks for a reindex
    Given I delete the file "library/.shelf/index.db"
    When I run shelf with arguments "show 01J9Z3K1000000000000000001 --library library"
    Then the exit code is 1
    And standard error contains "run 'shelf reindex'"
