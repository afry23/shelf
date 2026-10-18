@F-IMP-4 @M1
Feature: ISBN detection
  As a user with several formats of the same book
  I want shelf to find the ISBN in the metadata and on the first and last pages
  So that the files of one book can be grouped with certainty

  Scenario Outline: ISBNs are found in text and normalized to ISBN-13
    When shelf searches the text "<text>" for an ISBN
    Then the ISBN is "<isbn>"

    Examples:
      | text                                | isbn          |
      | ISBN 978-0-00-000001-9              | 9780000000019 |
      | ISBN-13: 9780000000026              | 9780000000026 |
      | ISBN 0-306-40615-2                  | 9780306406157 |
      | ISBN 978‐0‐306‐40615‐7              | 9780306406157 |
      | isbn 9780306406157 and more         | 9780306406157 |
      | ISBN: 978 0 306 40615 7             | 9780306406157 |
      | Printed in 2019. ISBN 9780306406157 | 9780306406157 |

  Scenario Outline: Numbers that are not valid ISBNs are rejected
    When shelf searches the text "<text>" for an ISBN
    Then there is no ISBN

    Examples:
      | text                         | reason                                  |
      | ISBN 978-0-306-40615-8       | wrong check digit                       |
      | 12345678901234               | 14 digits                               |
      | call 0306406152 now          | ISBN-10 without the word ISBN           |
      | 97803064061570               | 14 digits                               |

  Scenario Outline: Identifiers of the package or the Info dictionary are ISBNs only if nothing else
    When shelf reads the identifier "<identifier>"
    Then the ISBN is "<isbn>"

    Examples:
      | identifier                                  | isbn          |
      | urn:isbn:9780000000019                      | 9780000000019 |
      | 9780000000026                               | 9780000000026 |
      | 978-0-00-000002-6                           | 9780000000026 |
      | isbn:0306406152                             | 9780306406157 |

  Scenario Outline: UUIDs and other identifiers are never ISBNs
    When shelf reads the identifier "<identifier>"
    Then there is no ISBN

    Examples:
      | identifier                                           |
      | urn:uuid:9b6ffe1d-f194-4cc9-bf3f-fdd005afba9d        |
      | urn:uuid:9780000000019                               |
      | http://www.example.org/book/9780000000019            |
      | calibre:12                                           |
      |                                                      |

  Scenario Outline: The ISBN of a file comes from the metadata first and from the first and last pages second
    When shelf extracts the fixture "<file>"
    Then the ISBN is "<isbn>"

    Examples:
      | file                                         | isbn          |
      | derived/epub3-isbn-series.epub               | 9780000000019 |
      | derived/epub2-isbn-cover.epub                | 9780000000026 |
      | derived/Event Streams in Practice.pdf        | 9780000000019 |
      | derived/Jane Doe - Quiet Systems (2004).epub |               |
      | derived/Jane Doe - Quiet Systems (2004).pdf  |               |
      | upstream/gutenberg/pg201-flatland.epub3.epub |               |
      | upstream/pdf/arxiv-2403.02174v1.pdf          |               |
