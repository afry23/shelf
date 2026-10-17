@F-IMP-1 @F-GRP-1 @F-GRP-3 @F-IMP-7 @M1
Feature: Importing files into the library
  As a user with e-books in some folder
  I want shelf import to copy them into my library and group the formats of a book into one work
  So that my library gets one tidy folder per book, and unclear cases are shown to me

  Background:
    Given an empty working directory
    And I have run shelf with arguments "init library"

  Scenario: A file becomes a work in a folder named after author and title
    Given the folder "incoming" contains the fixtures "derived/epub3-isbn-series.epub"
    When I run shelf with arguments "import incoming --library library"
    Then the exit code is 0
    And standard output contains "new       epub3-isbn-series.epub"
    And standard output contains "Imported 1 file: 1 new work"
    And standard error is empty
    And the file "library/Ålander, Jördis/Event Streams in Practice/epub3-isbn-series.epub" exists
    And the sidecar "library/Ålander, Jördis/Event Streams in Practice/book.json" has the title "Event Streams in Practice" and the authors "Jördis Ålander"
    And the sidecar "library/Ålander, Jördis/Event Streams in Practice/book.json" lists the files "epub3-isbn-series.epub"
    And the table "works" of the index has 1 rows
    And the table "files" of the index has 1 rows

  Scenario: Importing copies by default and leaves the source alone
    Given the folder "incoming" contains the fixtures "derived/epub3-isbn-series.epub"
    When I run shelf with arguments "import incoming --library library"
    Then the file "incoming/epub3-isbn-series.epub" is identical to the fixture "derived/epub3-isbn-series.epub"
    And the file "library/Ålander, Jördis/Event Streams in Practice/epub3-isbn-series.epub" is identical to the fixture "derived/epub3-isbn-series.epub"

  Scenario: With --move the source is removed after the copy was verified
    Given the folder "incoming" contains the fixtures "derived/epub3-isbn-series.epub"
    When I run shelf with arguments "import incoming --move --library library"
    Then the exit code is 0
    And the file "incoming/epub3-isbn-series.epub" does not exist
    And the file "library/Ålander, Jördis/Event Streams in Practice/epub3-isbn-series.epub" is identical to the fixture "derived/epub3-isbn-series.epub"

  Scenario: An EPUB and a PDF with the same ISBN become one work
    Given the folder "incoming" contains the fixtures "derived/epub3-isbn-series.epub, derived/Event Streams in Practice.pdf"
    When I run shelf with arguments "import incoming --library library"
    Then the exit code is 0
    And standard output contains "added"
    And standard output contains "same ISBN-13 9780000000019"
    And standard output contains "Imported 2 files: 1 new work, 1 added to an existing work"
    And the sidecar "library/Ålander, Jördis/Event Streams in Practice/book.json" lists the files "Event Streams in Practice.pdf, epub3-isbn-series.epub"
    And the table "works" of the index has 1 rows
    And the table "files" of the index has 2 rows
    And the review queue of "library" has 0 entries

  Scenario: Files without metadata are grouped by the title and author of their names and reported as weak
    Given the folder "incoming" contains the fixtures "derived/Jane Doe - Quiet Systems (2004).epub, derived/Jane Doe - Quiet Systems (2004).pdf"
    When I run shelf with arguments "import incoming --library library"
    Then the exit code is 0
    And standard output contains "Imported 2 files: 1 new work, 1 added to an existing work, 1 review entry"
    And the table "works" of the index has 1 rows
    And the review queue of "library" has 1 entry
    And review entry 1 has the kind "weak-metadata"
    And review entry 1 has a message containing "title and authors come from the file name"

  Scenario: A file that could belong to a work but not surely gets its own work and a review entry
    Given a sample PDF "incoming/Jane Doe - Quiet Systems (2004).pdf" with the title "Quiet Systems", the author "Jane Doe" and the text seed 21
    And a sample EPUB "incoming/Jane Doe - Quiet Systems (2010).epub" with the title "Quiet Systems", the authors "Jane Doe" and the text seed 22
    When I run shelf with arguments "import incoming --library library"
    Then the exit code is 0
    And the table "works" of the index has 2 rows
    And standard output contains "may belong to"
    And standard output contains "title and author match, year differs"
    And standard output contains "Imported 2 files: 2 new works, 1 review entry"
    And the review queue of "library" has 1 entry
    And review entry 1 has the kind "grouping"
    And review entry 1 has a message containing "may belong to"

  Scenario: Importing the same content again is skipped as a duplicate
    Given the folder "incoming" contains the fixtures "derived/epub3-isbn-series.epub"
    And I have run shelf with arguments "import incoming --library library"
    And the fixture "derived/epub3-isbn-series.epub" copied as "other/renamed.epub"
    When I run shelf with arguments "import other --library library"
    Then the exit code is 0
    And standard output contains "duplicate renamed.epub"
    And standard output contains "same content as Ålander, Jördis/Event Streams in Practice/epub3-isbn-series.epub"
    And standard output contains "1 duplicate skipped"
    And the table "files" of the index has 1 rows
    And the file "other/renamed.epub" exists

  Scenario: A file name that is taken is never overwritten
    Given the folder "incoming" contains the fixtures "derived/Event Streams in Practice.pdf"
    And I have run shelf with arguments "import incoming --library library"
    And the fixture "derived/Event Streams in Practice.pdf" copied as "more/Event Streams in Practice.pdf" with a trailing line break
    When I run shelf with arguments "import more --library library"
    Then the exit code is 0
    And the sidecar "library/Ålander, Jördis/Event Streams in Practice/book.json" lists the files "Event Streams in Practice (1).pdf, Event Streams in Practice.pdf"
    And the file "library/Ålander, Jördis/Event Streams in Practice/Event Streams in Practice.pdf" is identical to the fixture "derived/Event Streams in Practice.pdf"
    And the table "files" of the index has 2 rows

  Scenario: A directory is searched recursively and other file types are ignored
    Given the fixture "derived/epub3-isbn-series.epub" copied as "incoming/a/one.epub"
    And the fixture "derived/epub2-isbn-cover.epub" copied as "incoming/b/c/two.epub"
    And a file "incoming/notes.txt" in the working directory
    And a file "incoming/b/book.mobi" in the working directory
    When I run shelf with arguments "import incoming --library library"
    Then the exit code is 0
    And standard output contains "Imported 2 files: 2 new works"
    And standard output contains "2 other files ignored"
    And the table "works" of the index has 2 rows

  Scenario: A broken file is reported, its problem is queued and the rest is imported
    Given the folder "incoming" contains the fixtures "derived/not-a-zip.epub, derived/epub3-isbn-series.epub"
    When I run shelf with arguments "import incoming --library library"
    Then the exit code is 1
    And standard output contains "error     not-a-zip.epub"
    And standard output contains "not-a-container"
    And standard output contains "Imported 1 file: 1 new work, 1 error"
    And standard error contains "warning: "
    And the table "works" of the index has 1 rows
    And the review queue of "library" has 1 entry
    And review entry 1 has the kind "extraction-error"
    And the file "incoming/not-a-zip.epub" exists

  Scenario: A dry run changes nothing and says what would happen
    Given the folder "incoming" contains the fixtures "derived/epub3-isbn-series.epub, derived/Event Streams in Practice.pdf"
    When I run shelf with arguments "import incoming --dry-run --library library"
    Then the exit code is 0
    And standard output contains "would add"
    And standard output contains "Would import 2 files: 1 new work, 1 added to an existing work"
    And the directory "library/Ålander, Jördis" does not exist
    And the table "works" of the index has 0 rows
    And the review queue of "library" has 0 entries

  Scenario: The JSON output lists every file
    Given the folder "incoming" contains the fixtures "derived/epub3-isbn-series.epub, derived/Event Streams in Practice.pdf"
    When I run shelf with arguments "import incoming --json --library library"
    Then the exit code is 0
    And standard output is a JSON object with the integer properties "imported=2, newWorks=1, added=1, duplicates=0, reviewEntries=0, errors=0, ignored=0"
    And the JSON property "files" of standard output has 2 items

  Scenario: Imported text can be found in the index
    Given the folder "incoming" contains the fixtures "derived/Event Streams in Practice.pdf"
    When I run shelf with arguments "import incoming --library library"
    Then the table "chunks" of the index has at least 1 rows
    And the chunk index finds the word "streams"
    And the file "Event Streams in Practice.pdf" in the index has the text status "done"
    And the file "Event Streams in Practice.pdf" in the index has a hash and a fingerprint

  Scenario: A scanned PDF without a text layer is imported without text
    Given the folder "incoming" contains the fixtures "derived/scanned-page.pdf"
    When I run shelf with arguments "import incoming --library library"
    Then the exit code is 0
    And the file "scanned-page.pdf" in the index has the text status "none"

  Scenario Outline: Import needs a library and a source
    When I run shelf with arguments "<arguments>"
    Then the exit code is <code>
    And standard error contains "<message>"

    Examples:
      | arguments                        | code | message                    |
      | import nowhere --library library | 1    | does not exist             |
      | import                           | 2    | Required argument missing  |
      | import x --library nowhere       | 1    | is not a shelf library     |
