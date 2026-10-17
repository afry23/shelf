@F-GRP-1 @M1
Feature: Text fingerprint
  As a user with an EPUB and a PDF of the same book
  I want a fingerprint of the running text that survives the differences of the two formats
  So that the files are grouped even when title, author and ISBN are missing

  Scenario: The same text has the same fingerprint
    Given a text "A" of 6000 words from the seed 1
    And the text "B" is the text "A"
    When shelf computes the fingerprints
    Then the distance between "A" and "B" is 0

  Scenario Outline: Changed words move the fingerprint a little, never as far as another text
    Given a text "A" of 6000 words from the seed 1
    And the text "B" is the text "A" with <percent> percent of the words replaced
    When shelf computes the fingerprints
    Then the distance between "A" and "B" is at most <distance>
    And the distance between "A" and "B" is at least <least>

    Examples:
      | percent | distance | least |
      | 1       | 8        | 1     |
      | 3       | 12       | 3     |

  Scenario Outline: Unrelated texts are far apart
    Given a text "A" of 6000 words from the seed 1
    And a text "B" of 6000 words from the seed <seed>
    When shelf computes the fingerprints
    Then the distance between "A" and "B" is at least 20

    Examples:
      | seed |
      | 2    |
      | 3    |
      | 99   |

  Scenario: Case, punctuation, white space and accents do not matter
    Given a text "A" of 6000 words from the seed 1
    And the text "B" is the text "A" in upper case with extra punctuation and line breaks
    When shelf computes the fingerprints
    Then the distance between "A" and "B" is 0

  Scenario: Only the first 5000 words count
    Given a text "A" of 6000 words from the seed 1
    And the text "B" is the first 5000 words of the text "A" followed by 3000 words from the seed 7
    When shelf computes the fingerprints
    Then the distance between "A" and "B" is 0

  Scenario: Text that spreads over many segments counts like one text
    Given a text "A" of 6000 words from the seed 1
    And the text "B" is the text "A" cut into segments of 400 words
    When shelf computes the fingerprints
    Then the distance between "A" and "B" is 0

  Scenario Outline: A text that is too short has no fingerprint
    Given a text "A" of <words> words from the seed 1
    When shelf computes the fingerprints
    Then the text "A" has <result>

    Examples:
      | words | result             |
      | 0     | no fingerprint     |
      | 199   | no fingerprint     |
      | 200   | a fingerprint      |

  Scenario: Fixtures that contain the same book are close
    When shelf computes the fingerprints of the fixtures "upstream/gutenberg/pg201-flatland.epub2.epub" and "upstream/gutenberg/pg201-flatland.epub3.epub"
    Then the distance between the fixtures is at most 3
