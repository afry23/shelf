@F-SRC-5 @M1
Feature: Text with a location
  As a user searching inside books
  I want every piece of extracted text to know where it came from
  So that a search hit can say "page 12" or "chapter 3" and open the book there

  Scenario Outline: PDF text is split by page and located as page=N
    When shelf extracts the fixture "<file>"
    Then the text has <segments> segments
    And the locator of segment 1 is "page=1"
    And the locator of the last segment is "page=<last page>"
    And the text of segment 1 contains "<word>"

    Examples:
      | file                                  | segments | last page | word    |
      | derived/Event Streams in Practice.pdf | 1        | 1         | Streams |

  Scenario: Every page of a longer PDF is located by its own number
    When shelf extracts the fixture "upstream/pdf/arxiv-2403.02174v1.pdf"
    Then the page count is 7
    And the text has 7 segments
    And the locator of segment 1 is "page=1"
    And the locator of segment 4 is "page=4"
    And the locator of the last segment is "page=7"

  Scenario Outline: EPUB text is split by spine item and located by a chapter CFI
    When shelf extracts the fixture "<file>"
    Then the locator of segment <number> is "<locator>"

    Examples:
      | file                           | number | locator                     |
      | derived/epub3-isbn-series.epub | 1      | epubcfi(/6/2[chapter1]!)    |
      | derived/epub3-isbn-series.epub | 2      | epubcfi(/6/4[chapter2]!)    |
      | derived/epub2-isbn-cover.epub  | 1      | epubcfi(/6/2[chapter1]!)    |

  Scenario Outline: Locators are built from the position and the id of the spine item
    When shelf builds the chapter locator for spine item <index> with the id "<idref>"
    Then the locator is "<locator>"

    Examples:
      | index | idref   | locator                      |
      | 0     | ch1     | epubcfi(/6/2[ch1]!)          |
      | 4     | x       | epubcfi(/6/10[x]!)           |
      | 11    | a_b-c.d | epubcfi(/6/24[a_b-c.d]!)     |
      | 1     | a[1]    | epubcfi(/6/4[a^[1^]]!)       |
      | 2     | a^b,c   | epubcfi(/6/6[a^^b^,c]!)      |

  Scenario Outline: Page locators are parsed back
    When shelf parses the page locator "<locator>"
    Then the page is "<page>"

    Examples:
      | locator           | page |
      | page=1            | 1    |
      | page=212          | 212  |
      | page=0            |      |
      | page=-3           |      |
      | page=x            |      |
      | epubcfi(/6/2!)    |      |
      |                   |      |

  Scenario: Text of a scanned PDF is empty, not an error
    When shelf extracts the fixture "derived/scanned-page.pdf"
    Then the text has 0 segments
    And the page count is 1
