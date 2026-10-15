@PRD-9.1 @M0
Feature: Synthetic library generator
  As a developer of shelf
  I want a deterministic generator for a synthetic e-book library
  So that performance and grouping tests use the same reproducible data on every machine

  Background:
    Given an empty generator workspace

  Scenario Outline: The generator creates the requested numbers of works and files
    When I run the generator with arguments "generate --seed 42 --works <works> --files <files>" into "out"
    Then the generator exit code is 0
    And the output "out" contains <files> e-book files
    And the ground truth of "out" lists <works> works and <files> files
    And the ground truth of "out" lists <single> works with 1 file, <double> works with 2 files and <triple> works with 3 files
    And the output "out" contains <epub> EPUB files and <pdf> PDF files

    Examples:
      | works | files | single | double | triple | epub | pdf |
      | 10    | 10    | 10     | 0      | 0      | 6    | 4   |
      | 10    | 15    | 5      | 5      | 0      | 8    | 7   |
      | 20    | 30    | 11     | 8      | 1      | 17   | 13  |
      | 20    | 40    | 2      | 16     | 2      | 22   | 18  |
      | 100   | 150   | 55     | 40     | 5      | 83   | 67  |

  Scenario Outline: Presets plan the documented library shapes without writing files
    When I run the generator with arguments "generate --preset <preset> --plan"
    Then the generator exit code is 0
    And the generator plan has the value <value> for "<property>"

    Examples:
      | preset    | property             | value |
      | reference | works                | 10000 |
      | reference | files                | 15000 |
      | reference | singleFileWorks      | 5500  |
      | reference | twoFileWorks         | 4000  |
      | reference | threeFileWorks       | 500   |
      | reference | epubFiles            | 8300  |
      | reference | pdfFiles             | 6700  |
      | reference | exactDuplicates      | 250   |
      | reference | sameFormatDuplicates | 250   |
      | reference | editionPairs         | 200   |
      | reference | hardNegativePairs    | 100   |
      | reference | seriesWorks          | 1000  |
      | tiny      | works                | 20    |
      | tiny      | files                | 30    |

  Scenario Outline: The same seed produces byte-identical output
    When I run the generator with arguments "generate --seed <seed> --works 20 --files 30" into "a"
    And I run the generator with arguments "generate --seed <seed> --works 20 --files 30" into "b"
    Then the outputs "a" and "b" are byte-identical

    Examples:
      | seed       |
      | 0          |
      | 42         |
      | 2147483647 |

  Scenario: Different seeds produce different libraries
    When I run the generator with arguments "generate --seed 1 --works 20 --files 30" into "a"
    And I run the generator with arguments "generate --seed 2 --works 20 --files 30" into "b"
    Then the outputs "a" and "b" differ

  Scenario: The ground truth describes every generated file exactly
    When I run the generator with arguments "generate --seed 42 --works 100 --files 150" into "out"
    Then every e-book file in "out" is listed in the ground truth with its size and SHA-256
    And every file listed in the ground truth of "out" exists
    And every generated EPUB in "out" starts with the ZIP signature and every generated PDF with "%PDF-"

  Scenario Outline: Duplicates in the ground truth match the file contents
    When I run the generator with arguments "generate --seed 42 --works 100 --files 150" into "out"
    Then the ground truth of "out" lists <count> files with duplicate kind "<kind>"
    And every file with duplicate kind "<kind>" in "out" belongs to the same work as its original
    And every file with duplicate kind "<kind>" in "out" <relation> its original

    Examples:
      | kind        | count | relation                     |
      | exact       | 3     | has the same SHA-256 as      |
      | same-format | 2     | has a different SHA-256 than |

  Scenario Outline: A medium library contains every variation needed for grouping tests
    When I run the generator with arguments "generate --seed 42 --works 200 --files 300" into "out"
    Then the ground truth of "out" has at least one file with the trait "<trait>"

    Examples:
      | trait                   |
      | epub2                   |
      | epub3                   |
      | cover                   |
      | isbn                    |
      | series                  |
      | author-surname-first    |
      | author-initials         |
      | author-no-diacritics    |
      | title-edition-suffix    |
      | title-leading-article   |
      | title-non-ascii         |
      | pdf-info                |
      | pdf-xmp                 |
      | pdf-no-metadata         |
      | pdf-isbn-in-text        |
      | file-name-author-title  |
      | language-de             |
      | language-en             |

  Scenario Outline: A medium library contains related and confusable works
    When I run the generator with arguments "generate --seed 42 --works 200 --files 300" into "out"
    Then the ground truth of "out" lists <count> work pairs with relation "<relation>"

    Examples:
      | relation                    | count |
      | edition                     | 4     |
      | same-title-different-author | 2     |

  Scenario Outline: Invalid arguments are usage errors
    When I run the generator with arguments "<arguments>" into "out"
    Then the generator exit code is 2
    And the generator error output contains "<message>"
    And the output "out" contains 0 e-book files

    Examples:
      | arguments                                    | message                                              |
      | generate --works 0 --files 0                 | --works must be at least 1                           |
      | generate --works 10 --files 9                | --files must be between 10 and 21 for 10 works       |
      | generate --works 10 --files 22               | --files must be between 10 and 21 for 10 works       |
      | generate --works 10                          | --works and --files must be given together           |
      | generate --preset tiny --works 10 --files 10 | --preset cannot be combined with --works or --files  |
      | generate --preset huge                       | Unknown preset 'huge'                                |
      | generate --works 10 --files 10 --words-min 9000 --words-max 8000 | --words-min must not exceed --words-max |

  Scenario: A missing output directory is a usage error
    When I run the generator with arguments "generate --works 10 --files 10"
    Then the generator exit code is 2
    And the generator error output contains "--out is required unless --plan is given"

  Scenario: The generator never writes into a non-empty directory
    Given the generator workspace directory "out" contains a file "keep.txt"
    When I run the generator with arguments "generate --seed 42 --works 10 --files 10" into "out"
    Then the generator exit code is 1
    And the generator error output contains "is not empty"
    And the output "out" contains 0 e-book files
    And the generator workspace file "out/keep.txt" still exists
