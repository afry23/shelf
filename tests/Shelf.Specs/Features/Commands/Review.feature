@F-IMP-7 @F-GRP-4 @M1
Feature: Working through the review queue
  As a user whose import was unsure about some files
  I want to list the queue and decide each entry
  So that nothing is filed wrongly and nothing stays undecided

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
    When I run shelf with arguments "review --library library"
    Then the exit code is 0
    And standard output is "Nothing to review."

  Scenario: The entries are listed with what shelf knows
    Given the review queue of "library" gets a grouping entry for the work "01J9Z3K3000000000000000003" about "Ibryam, Bilgin/Kubernetes Patterns/kp.pdf" with the candidate "01J9Z3K1000000000000000001" at 0.6 because "same title and author, year differs"
    And the review queue of "library" gets an entry of the kind "extraction-error" about "Ålander, Jördis/Event Sourcing in Practice/es.pdf" with the message "bad-pdf: not a PDF"
    When I run shelf with arguments "review --library library"
    Then the exit code is 0
    And standard output matches the pattern "(?m)^1\. grouping +Ibryam, Bilgin/Kubernetes Patterns/kp\.pdf$"
    And standard output matches the pattern "(?m)^   id +01[0-9A-HJKMNP-TV-Z]{24}$"
    And standard output matches the pattern "(?m)^   work +01J9Z3K3000000000000000003 Kubernetes Patterns$"
    And standard output matches the pattern "(?m)^   candidate +01J9Z3K1000000000000000001 Kubernetes Patterns \(2019\) 0\.6 same title and author, year differs$"
    And standard output matches the pattern "(?m)^2\. extraction-error +Ålander, Jördis/Event Sourcing in Practice/es\.pdf$"
    And standard output matches the pattern "(?m)^   bad-pdf: not a PDF$"
    And standard output contains "2 entries"

  Scenario: The JSON form lists every entry in full
    Given the review queue of "library" gets a grouping entry for the work "01J9Z3K3000000000000000003" about "Ibryam, Bilgin/Kubernetes Patterns/kp.pdf" with the candidate "01J9Z3K1000000000000000001" at 0.6 because "same title and author, year differs"
    When I run shelf with arguments "review --library library --json"
    Then the exit code is 0
    And the JSON of standard output has the number "total" equal to 1
    And the JSON of standard output has the string "entries[0].kind" equal to "grouping"
    And the JSON of standard output has the string "entries[0].file" equal to "Ibryam, Bilgin/Kubernetes Patterns/kp.pdf"
    And the JSON of standard output has the string "entries[0].work" equal to "01J9Z3K3000000000000000003"
    And the JSON of standard output has the string "entries[0].candidates[0].work" equal to "01J9Z3K1000000000000000001"
    And the JSON of standard output has the decimal "entries[0].candidates[0].confidence" equal to 0.6
    And the JSON of standard output has the string "entries[0].candidates[0].signals[0]" equal to "same title and author, year differs"

  Scenario: Accepting a grouping entry merges the work into the best candidate
    Given the review queue of "library" gets a grouping entry for the work "01J9Z3K3000000000000000003" about "Ibryam, Bilgin/Kubernetes Patterns/kp.pdf" with the candidate "01J9Z3K1000000000000000001" at 0.6 because "same title and author, year differs"
    When I run shelf with arguments "review accept 1 --library library"
    Then the exit code is 0
    And standard output matches the pattern "^Merged 01J9Z3K3000000000000000003 into 01J9Z3K1000000000000000001: 1 file moved$"
    And the sidecar "library/Ibryam, Bilgin/Kubernetes Patterns (2019)/book.json" lists the files "kp.epub, kp.pdf"
    And the review queue of "library" has 0 entries
    And the table "works" of the index has 2 rows

  Scenario: Accepting can name another work
    Given the review queue of "library" gets a grouping entry for the work "01J9Z3K3000000000000000003" about "Ibryam, Bilgin/Kubernetes Patterns/kp.pdf" with the candidate "01J9Z3K1000000000000000001" at 0.6 because "same title and author, year differs"
    When I run shelf with arguments "review accept 1 --into 01J9Z3K2 --library library"
    Then the exit code is 0
    And the sidecar "library/Ålander, Jördis/Event Sourcing in Practice/book.json" lists the files "es.pdf, kp.pdf"
    And the review queue of "library" has 0 entries

  Scenario: Dismissing a grouping entry keeps the works apart and remembers it
    Given the review queue of "library" gets a grouping entry for the work "01J9Z3K3000000000000000003" about "Ibryam, Bilgin/Kubernetes Patterns/kp.pdf" with the candidate "01J9Z3K1000000000000000001" at 0.6 because "same title and author, year differs"
    When I run shelf with arguments "review dismiss 1 --library library"
    Then the exit code is 0
    And standard output is "Dismissed the entry about Ibryam, Bilgin/Kubernetes Patterns/kp.pdf"
    And the review queue of "library" has 0 entries
    And the table "works" of the index has 3 rows
    And the sidecar "library/Ibryam, Bilgin/Kubernetes Patterns (2019)/book.json" read back marks 1 work as not duplicates
    And the sidecar "library/Ibryam, Bilgin/Kubernetes Patterns/book.json" read back marks 1 work as not duplicates

  Scenario Outline: Other entries are settled by dismissing them
    Given the review queue of "library" gets an entry of the kind "<kind>" about "Ålander, Jördis/Event Sourcing in Practice/es.pdf" with the message "<message>"
    When I run shelf with arguments "review dismiss 1 --library library"
    Then the exit code is 0
    And the review queue of "library" has 0 entries
    And the sidecar "library/Ålander, Jördis/Event Sourcing in Practice/book.json" read back marks 0 works as not duplicates

    Examples:
      | kind             | message                              |
      | extraction-error | bad-pdf: not a PDF                   |
      | weak-metadata    | title and authors come from the file name |

  Scenario Outline: An entry is named by its position or by the start of its ID
    Given the review queue of "library" gets an entry of the kind "weak-metadata" about "a.epub" with the message "first"
    And the review queue of "library" gets an entry of the kind "weak-metadata" about "b.epub" with the message "second"
    When I run shelf with arguments "review dismiss <entry> --library library"
    Then the exit code is 0
    And the review queue of "library" has 1 entry
    And entry 1 of "library" has the message "<left>"

    Examples:
      | entry | left   |
      | 1     | second |
      | 2     | first  |

  Scenario: Accepting only works for grouping entries
    Given the review queue of "library" gets an entry of the kind "extraction-error" about "Ålander, Jördis/Event Sourcing in Practice/es.pdf" with the message "bad-pdf: not a PDF"
    When I run shelf with arguments "review accept 1 --library library"
    Then the exit code is 1
    And standard error starts with "error: only a grouping entry can be accepted (this one is an extraction-error)"
    And the review queue of "library" still has 1 entries

  Scenario Outline: An entry that does not exist is an error
    When I run shelf with arguments "review <action> <entry> --library library"
    Then the exit code is 1
    And standard error starts with "error: no review entry matches '<entry>'"

    Examples:
      | action  | entry |
      | accept  | 1     |
      | dismiss | 3     |

  Scenario: Accepting an entry whose work is gone is an error
    Given the review queue of "library" gets a grouping entry for the work "01J9Z3K5000000000000000005" about "x.pdf" with the candidate "01J9Z3K1000000000000000001" at 0.6 because "same title"
    When I run shelf with arguments "review accept 1 --library library"
    Then the exit code is 1
    And standard error starts with "error: no work matches '01J9Z3K5000000000000000005'"
    And the review queue of "library" still has 1 entries

  Scenario: A new import puts its uncertain files into the queue that the command lists
    Given the folder "incoming" contains the fixtures "derived/Jane Doe - Quiet Systems (2004).epub"
    And I have run shelf with arguments "import incoming --library library"
    When I run shelf with arguments "review --library library"
    Then the exit code is 0
    And standard output matches the pattern "(?m)^1\. weak-metadata +"
