@F-IMP-2 @M1
Feature: Broken and unusual files
  As a user whose folder holds files of every quality
  I want a damaged file to be reported as a problem of that one file
  So that the rest of an import goes through and I learn which file to look at

  Background:
    Given an empty working directory

  Scenario Outline: Broken fixtures fail with a named kind
    When shelf extracts the fixture "<file>"
    Then extraction fails with the kind "<kind>"
    And the error message starts with the path of the file

    Examples:
      | file                         | kind           |
      | derived/not-a-zip.epub       | not-a-container |
      | derived/malformed-opf.epub   | bad-epub       |

  Scenario Outline: Generated broken files fail with a named kind
    Given a file "<name>" with the content <content>
    When shelf extracts the file "<name>"
    Then extraction fails with the kind "<kind>"

    Examples:
      | name       | content               | kind            |
      | empty.pdf  | nothing               | bad-pdf         |
      | empty.epub | nothing               | not-a-container |
      | junk.pdf   | the text "not a pdf"  | bad-pdf         |
      | junk.epub  | the text "not a zip"  | not-a-container |
      | book.mobi  | the text "x"          | unsupported     |
      | noext      | the text "x"          | unsupported     |

  Scenario: A PDF cut off in the middle is an error of that file or a file without text, never a crash
    Given the fixture "derived/Event Streams in Practice.pdf" cut off after half of its bytes as "cut.pdf"
    When shelf extracts the file "cut.pdf"
    Then extraction ends with a result or with an error of the kind "bad-pdf"

  Scenario: An EPUB with an entry that is too large is refused before it is read
    Given an EPUB "huge.epub" whose chapter claims to have 65 MiB when unpacked
    When shelf extracts the file "huge.epub"
    Then extraction fails with the kind "too-large"

  Scenario: Extraction does not change the file
    Given the fixture "derived/epub3-isbn-series.epub" copied as "copy.epub"
    When shelf extracts the file "copy.epub"
    Then the file "copy.epub" is identical to the fixture "derived/epub3-isbn-series.epub"
