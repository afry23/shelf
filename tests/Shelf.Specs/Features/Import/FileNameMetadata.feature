@F-IMP-3 @M1
Feature: Metadata from the file name
  As a user whose files carry no metadata
  I want shelf to guess author, title and year from names like "Author - Title (Year).pdf"
  So that such files still become sensible works, and I am asked when the guess is weak

  Scenario Outline: Author, title and year are taken from the file name
    When shelf reads the file name "<file name>"
    Then the file name title is "<title>"
    And the file name authors are "<authors>"
    And the file name year is "<year>"

    Examples:
      | file name                                          | title                       | authors                     | year |
      | Jane Doe - Quiet Systems (2004).pdf                | Quiet Systems               | Jane Doe                    | 2004 |
      | Jane Doe - Quiet Systems [2004].epub               | Quiet Systems               | Jane Doe                    | 2004 |
      | Jane Doe - Quiet Systems.epub                      | Quiet Systems               | Jane Doe                    |      |
      | Quiet Systems (2004).pdf                           | Quiet Systems               |                             | 2004 |
      | Quiet Systems.pdf                                  | Quiet Systems               |                             |      |
      | Kleppmann, Martin - Designing Data (2017).epub     | Designing Data              | Kleppmann, Martin           | 2017 |
      | Jane Doe & John Roe - Shared Work (1999).pdf       | Shared Work                 | Jane Doe; John Roe          | 1999 |
      | Jane Doe; John Roe - Shared Work.pdf              | Shared Work                 | Jane Doe; John Roe          |      |
      | Jane_Doe_-_Quiet_Systems_(2004).pdf                | Quiet Systems               | Jane Doe                    | 2004 |
      | Jane Doe – Quiet Systems.pdf                       | Quiet Systems               | Jane Doe                    |      |
      | Title - With - Dashes.pdf                          | With - Dashes               | Title                       |      |
      | arxiv-2403.02174v1.pdf                             | arxiv-2403.02174v1          |                             |      |
      | Catch 22 (1961).epub                               | Catch 22                    |                             | 1961 |
      | 1984 (1949).epub                                   | 1984                        |                             | 1949 |

  Scenario Outline: Years that cannot be publication years are not taken
    When shelf reads the file name "<file name>"
    Then the file name year is ""
    And the file name title is "<title>"

    Examples:
      | file name                | title               |
      | Report (0042).pdf        | Report (0042)       |
      | Report (3999).pdf        | Report (3999)       |
      | Report (12345).pdf       | Report (12345)      |

  Scenario Outline: Embedded metadata wins, the file name only fills what is missing
    When shelf extracts the fixture "<file>"
    Then the title is "<title>"
    And the authors are "<authors>"
    And the title comes from the <title origin>
    And the authors come from the <authors origin>

    Examples:
      | file                                    | title                     | authors        | title origin | authors origin |
      | derived/Jane Doe - Quiet Systems (2004).epub | Quiet Systems        | Jane Doe       | file name    | file name      |
      | derived/Jane Doe - Quiet Systems (2004).pdf  | Quiet Systems        | Jane Doe       | file name    | file name      |
      | derived/epub3-isbn-series.epub          | Event Streams in Practice | Jördis Ålander | file content | file content   |
      | derived/scanned-page.pdf                | Scanned Page              |                | file content | nothing        |
      | upstream/pdf/arxiv-2403.02174v1.pdf     | arxiv-2403.02174v1        |                | file name    | nothing        |

  Scenario: The year of the file name completes metadata without a year
    When shelf extracts the fixture "derived/Jane Doe - Quiet Systems (2004).pdf"
    Then the year is "2004"
