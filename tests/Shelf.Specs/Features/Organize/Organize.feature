@F-ORG-1 @F-ORG-5 @F-ORG-6 @F-ORG-7 @F-ORG-8 @M2
Feature: Organizing the library by pattern
  As a user whose books lie in folders of their own making
  I want shelf organize to show what it would move and to do it only on request
  So that the library gets the folder structure of the pattern without ever losing or overwriting a file

  Background:
    Given an empty working directory
    And I have run shelf with arguments "init library"
    And a sample EPUB "library/inbox/pea.epub" with the title "Patterns of Enterprise Application Architecture", the authors "Martin Fowler" and the text seed 1
    And a sample PDF "library/inbox/pea.pdf" with the title "Patterns of Enterprise Application Architecture", the author "Martin Fowler" and the text seed 1
    And the sidecar "library/inbox/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K1000000000000000001", "title": "Patterns of Enterprise Application Architecture", "authors": ["Martin Fowler"], "year": 2002, "tags": ["architecture"], "files": [{ "name": "pea.epub" }, { "name": "pea.pdf" }] }
      """
    And a sample EPUB "library/misc/ddia.epub" with the title "Designing Data-Intensive Applications", the authors "Martin Kleppmann" and the text seed 2
    And the sidecar "library/misc/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K2000000000000000002", "title": "Designing Data-Intensive Applications", "authors": ["Martin Kleppmann"], "year": 2017, "files": [{ "name": "ddia.epub" }] }
      """
    And I have run shelf with arguments "reindex --library library"

  Scenario: Without --apply organize only shows the changes
    When I run shelf with arguments "organize --library library"
    Then the exit code is 0
    And standard output contains "inbox/pea.epub -> Fowler, Martin/Patterns of Enterprise Application Architecture (2002)/Patterns of Enterprise Application Architecture.epub"
    And standard output contains "misc/ddia.epub -> Kleppmann, Martin/Designing Data-Intensive Applications (2017)/Designing Data-Intensive Applications.epub"
    And standard output contains "Would move 3 files in 2 works"
    And standard output contains "--apply"
    And the file "library/inbox/pea.epub" exists
    And the file "library/misc/ddia.epub" exists
    And the directory "library/Fowler, Martin" does not exist
    And the directory "library/.shelf/journal" does not exist

  Scenario: With --apply the files move and the sidecar follows
    When I run shelf with arguments "organize --apply --library library"
    Then the exit code is 0
    And standard output contains "Moved 3 files in 2 works"
    And the file "library/Fowler, Martin/Patterns of Enterprise Application Architecture (2002)/Patterns of Enterprise Application Architecture.epub" exists
    And the file "library/Fowler, Martin/Patterns of Enterprise Application Architecture (2002)/Patterns of Enterprise Application Architecture.pdf" exists
    And the file "library/Kleppmann, Martin/Designing Data-Intensive Applications (2017)/Designing Data-Intensive Applications.epub" exists
    And the file "library/inbox/pea.epub" does not exist
    And the file "library/misc/ddia.epub" does not exist
    And the sidecar "library/Fowler, Martin/Patterns of Enterprise Application Architecture (2002)/book.json" lists the files "Patterns of Enterprise Application Architecture.epub, Patterns of Enterprise Application Architecture.pdf"
    And the file "library/Fowler, Martin/Patterns of Enterprise Application Architecture (2002)/book.json" contains "architecture"
    And the file "library/inbox/book.json" does not exist

  Scenario: The index follows at once
    When I run shelf with arguments "organize --apply --library library"
    Then the table "works" of the index has 2 rows
    And the table "files" of the index has 3 rows
    And the file "Patterns of Enterprise Application Architecture.epub" in the index has the path "Fowler, Martin/Patterns of Enterprise Application Architecture (2002)/Patterns of Enterprise Application Architecture.epub"
    And the full-text index finds the work "01J9Z3K1000000000000000001" for the query "patterns"

  Scenario: Every run writes a journal
    When I run shelf with arguments "organize --apply --library library"
    Then the directory "library/.shelf/journal" exists

  Scenario: Empty folders are removed after moving, folders with other files stay
    Given the file "library/inbox/notes.txt" has the content "my notes"
    When I run shelf with arguments "organize --apply --library library"
    Then the directory "library/misc" does not exist
    And the directory "library/inbox" exists
    And the file "library/inbox/notes.txt" exists

  Scenario: A second run finds nothing to do
    Given I have run shelf with arguments "organize --apply --library library"
    When I run shelf with arguments "organize --apply --library library"
    Then the exit code is 0
    And standard output contains "Nothing to organize"

  Scenario Outline: A pattern from the command line replaces the configured one
    When I run shelf with arguments "organize --apply --pattern '<pattern>' --library library"
    Then the exit code is 0
    And the file "library/<first>" exists
    And the file "library/<second>" exists

    Examples:
      | pattern                          | first                                                                      | second                                                           |
      | {author_sort}/{first_tag}/{title}.{ext} | Fowler, Martin/architecture/Patterns of Enterprise Application Architecture.epub | Kleppmann, Martin/Designing Data-Intensive Applications.epub |
      | {author}/{title} ({year}).{ext}  | Martin Fowler/Patterns of Enterprise Application Architecture (2002).epub  | Martin Kleppmann/Designing Data-Intensive Applications (2017).epub |

  Scenario Outline: An invalid pattern is a usage error and nothing moves
    When I run shelf with arguments "organize --apply --pattern '<pattern>' --library library"
    Then the exit code is 2
    And standard error contains "error: --pattern: <message>"
    And the file "library/inbox/pea.epub" exists

    Examples:
      | pattern        | message                                  |
      | {nope}.{ext}   | unknown placeholder '{nope}'             |
      | {title}        | the last part must end with '.{ext}'     |
      | ../{title}.{ext} | must not contain '..'                  |

  Scenario: A taken target is never overwritten; the work is skipped and reported
    Given the file "library/Fowler, Martin/Patterns of Enterprise Application Architecture (2002)/Patterns of Enterprise Application Architecture.epub" has the content "precious"
    When I run shelf with arguments "organize --apply --library library"
    Then the exit code is 0
    And standard error contains "skipped inbox/pea.epub"
    And standard error contains "target exists"
    And standard output contains "Moved 1 file in 1 work (1 collision)"
    And the file "library/Fowler, Martin/Patterns of Enterprise Application Architecture (2002)/Patterns of Enterprise Application Architecture.epub" contains "precious"
    And the file "library/inbox/pea.epub" exists
    And the file "library/inbox/pea.pdf" exists
    And the file "library/inbox/book.json" exists
    And the file "library/Kleppmann, Martin/Designing Data-Intensive Applications (2017)/Designing Data-Intensive Applications.epub" exists

  Scenario: Two works that map to the same place: the first moves, the second is reported
    Given a sample EPUB "library/other/pea2.epub" with the title "Patterns of Enterprise Application Architecture", the authors "Martin Fowler" and the text seed 3
    And the sidecar "library/other/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K3000000000000000003", "title": "Patterns of Enterprise Application Architecture", "authors": ["Martin Fowler"], "year": 2002, "files": [{ "name": "pea2.epub" }] }
      """
    And I have run shelf with arguments "reindex --library library"
    When I run shelf with arguments "organize --apply --library library --json"
    Then the exit code is 0
    And standard output is a JSON object with the integer properties "files=3, works=2, unchanged=0, collisions=2"
    And the file "library/other/pea2.epub" exists
    And the file "library/other/book.json" exists

  Scenario: The dry run as JSON
    When I run shelf with arguments "organize --library library --json"
    Then the exit code is 0
    And standard output is a JSON object with the integer properties "files=3, works=2, unchanged=0, collisions=0"
    And the JSON property "changes" of standard output has 3 items
    And the JSON property "collided" of standard output has 0 items
    And the file "library/inbox/pea.epub" exists

  Scenario: The second run as JSON counts the files already in place
    Given I have run shelf with arguments "organize --apply --library library"
    When I run shelf with arguments "organize --library library --json"
    Then standard output is a JSON object with the integer properties "files=0, works=0, unchanged=3, collisions=0"
