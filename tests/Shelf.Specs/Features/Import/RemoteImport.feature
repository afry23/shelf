@F-IMP-8 @F-IMP-1 @M5
Feature: Importing files from a cloud folder
  As a user whose e-books lie in Google Drive or another cloud
  I want shelf import --from to fetch them with rclone and import them like local files
  So that I do not have to download a folder by hand first

  Background:
    Given an empty working directory
    And I have run shelf with arguments "init library"

  Scenario: Files of a remote folder are imported like local files
    Given the rclone remote "gdrive:Books" holds the fixtures "derived/epub3-isbn-series.epub"
    When I run shelf with arguments "import --from rclone:gdrive:Books --library library"
    Then the exit code is 0
    And standard output contains "Imported 1 file: 1 new work"
    And the file "library/Ålander, Jördis/Event Streams in Practice/epub3-isbn-series.epub" exists
    And the table "works" of the index has 1 rows
    And no rclone download folder is left behind

  Scenario: Only the book formats are requested from the remote
    Given the rclone remote "gdrive:Books" holds the fixtures "derived/epub3-isbn-series.epub"
    When I run shelf with arguments "import --from rclone:gdrive:Books --library library"
    Then rclone was called with the arguments "copy gdrive:Books"
    And rclone was called with the arguments "--include *.epub --include *.pdf"

  Scenario: A dry run changes nothing in the library
    Given the rclone remote "gdrive:Books" holds the fixtures "derived/epub3-isbn-series.epub"
    When I run shelf with arguments "import --from rclone:gdrive:Books --dry-run --library library"
    Then the exit code is 0
    And the table "works" of the index has 0 rows
    And no rclone download folder is left behind

  Scenario Outline: Unusable arguments are refused before rclone is called
    When I run shelf with arguments "<arguments>"
    Then the exit code is 2
    And standard error contains "<message>"
    And rclone was not called

    Examples:
      | arguments                                                  | message                                |
      | import --from rclone:gdrive:Books --move --library library | --move cannot be used with --from      |
      | import --from gdrive:Books --library library               | --from must start with rclone:         |
      | import --from rclone: --library library                    | --from needs a remote and a path       |
      | import incoming --from rclone:gdrive:Books --library library | give either a path or --from, not both |

  Scenario: rclone is not installed
    Given rclone is not installed
    When I run shelf with arguments "import --from rclone:gdrive:Books --library library"
    Then the exit code is 2
    And standard error contains "rclone was not found"
    And the table "works" of the index has 0 rows

  Scenario: rclone fails
    Given the rclone call fails with "Failed to create file system for gdrive:Books: didn't find section in config file"
    When I run shelf with arguments "import --from rclone:gdrive:Books --library library"
    Then the exit code is 1
    And standard error contains "didn't find section in config file"
    And the table "works" of the index has 0 rows
    And no rclone download folder is left behind
