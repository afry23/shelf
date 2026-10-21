@F-GRP-6 @F-GRP-1 @F-GRP-2 @F-GRP-3 @F-IMP-1 @M1
Feature: Grouping quality on a generated library (acceptance criterion 1)
  As the owner of a folder with hundreds of mixed e-book files
  I want the formats of a book to end up in one work, and different books never to be merged
  So that the automatic grouping can be trusted with the default thresholds

  The generated library mixes single-format works, works as EPUB + PDF, a third file (exact copy or second EPUB),
  editions of the same book, books with the same title by different authors, and series.
  The ground truth names the work of every file; the files are matched by their SHA-256.

  Scenario Outline: The default thresholds merge the format pairs and never merge different books
    Given an empty working directory
    And I have run shelf with arguments "init library"
    And a generated library of <works> works and <files> files with the seed <seed>
    When I run shelf with arguments "import generated/input --library library"
    Then the exit code is 0
    And the generated library has 100 works with an EPUB and a PDF
    And at least 95 percent of them are in one work of the library
    And no work of the library holds the files of different generated works other than editions of one book
    And the library has at most <maxWorks> works

    Examples:
      | works | files | seed | maxWorks |
      | 389   | 500   | 1    | 393      |
      | 389   | 500   | 2    | 393      |
      | 389   | 500   | 3    | 393      |
      | 389   | 500   | 42   | 393      |

  Scenario Outline: PDFs in print layout (running header, page numbers, hyphenation) are grouped as well
    Given an empty working directory
    And I have run shelf with arguments "init library"
    And a generated library of <works> works and <files> files with the seed <seed> and the PDFs in print layout
    When I run shelf with arguments "import generated/input --library library"
    Then the exit code is 0
    And at least 95 percent of them are in one work of the library
    And no work of the library holds the files of different generated works other than editions of one book
    And the library has at most <maxWorks> works

    Examples:
      | works | files | seed | maxWorks |
      | 389   | 500   | 1    | 393      |
      | 389   | 500   | 2    | 393      |
      | 389   | 500   | 3    | 393      |
      | 389   | 500   | 42   | 393      |

  Scenario: The same grouping results from a rebuild of the index
    Given an empty working directory
    And I have run shelf with arguments "init library"
    And a generated library of 389 works and 500 files with the seed 7
    And I have run shelf with arguments "import generated/input --library library"
    When I run shelf with arguments "reindex --library library"
    Then the exit code is 0
    And no work of the library holds the files of different generated works
    And at least 95 percent of them are in one work of the library

  Scenario: Stricter thresholds still never merge different books
    Given an empty working directory
    And I have run shelf with arguments "init library"
    And a config file "library/.shelf/config.toml" with content:
      """
      [grouping]
      auto_merge = 1.0
      """
    And a generated library of 389 works and 500 files with the seed 1
    When I run shelf with arguments "import generated/input --library library"
    Then the exit code is 0
    And no work of the library holds the files of different generated works
