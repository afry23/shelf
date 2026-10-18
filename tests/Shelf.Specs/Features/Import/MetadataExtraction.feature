@F-IMP-2 @M1
Feature: Metadata from EPUB and PDF files
  As a user importing e-books
  I want shelf to read title, authors, language and more from inside the files
  So that works are described correctly without typing anything

  Scenario Outline: EPUB metadata comes from the OPF package
    When shelf extracts the fixture "<file>"
    Then the title is "<title>"
    And the authors are "<authors>"
    And the language is "<language>"
    And the title and authors come from the file content

    Examples:
      | file                                                  | title                                 | authors                      | language |
      | derived/epub3-isbn-series.epub                        | Event Streams in Practice             | Jördis Ålander               | en       |
      | derived/epub2-isbn-cover.epub                         | Größenordnungen verteilter Systeme    | Lukas Weiß; Ana Núñez        | de       |
      | upstream/gutenberg/pg201-flatland.epub2.epub          | Flatland: A Romance of Many Dimensions | Edwin Abbott Abbott          | en       |
      | upstream/gutenberg/pg201-flatland.epub3.epub          | Flatland: A Romance of Many Dimensions | Edwin Abbott Abbott          | en       |
      | upstream/gutenberg/pg22367-die-verwandlung.epub3.epub | Die Verwandlung                       | Franz Kafka                  | de       |
      | upstream/standardebooks/edwin-a-abbott_flatland.epub  | Flatland                              | Edwin A. Abbott              | en-GB    |

  Scenario Outline: EPUB series information is read from EPUB 3 collections and calibre metadata
    When shelf extracts the fixture "<file>"
    Then the series is "<series>" with the volume <volume>

    Examples:
      | file                           | series                | volume |
      | derived/epub3-isbn-series.epub | Streaming Foundations | 2      |
      | derived/epub2-isbn-cover.epub  | Verteilte Systeme     | 1      |

  Scenario Outline: PDF metadata comes from the Info dictionary and the XMP packet
    When shelf extracts the fixture "<file>"
    Then the title is "<title>"
    And the authors are "<authors>"
    And the page count is <pages>

    Examples:
      | file                          | title                     | authors        | pages |
      | derived/Event Streams in Practice.pdf | Event Streams in Practice | Jördis Ålander | 1     |
      | derived/scanned-page.pdf              | Scanned Page              |                | 1     |

  Scenario: A PDF without any metadata gets title and authors from nowhere inside the file
    When shelf extracts the fixture "upstream/pdf/arxiv-2403.02174v1.pdf" without using its file name
    Then there is no title
    And there are no authors

  Scenario Outline: Language is only taken from the file, never guessed
    When shelf extracts the fixture "<file>"
    Then the language is "<language>"

    Examples:
      | file                                  | language |
      | derived/Event Streams in Practice.pdf |          |
      | derived/scanned-page.pdf              |          |
