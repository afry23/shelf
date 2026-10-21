@F-GRP-1 @F-GRP-3 @F-GRP-6 @M1
Feature: Deciding which work a file belongs to
  As a user importing a file
  I want shelf to merge it into an existing work only when that is certain
  So that unclear cases are shown to me instead of being merged wrongly

  Background:
    Given the library has these works:
      | work | title        | authors          | year | isbn13        | fingerprint |
      | W1   | The Hobbit   | J. R. R. Tolkien | 1937 | 9780261102217 | 0123456789abcdef |
      | W2   | Dune         | Frank Herbert    | 1965 |               | fedcba9876543210 |
      | W3   | Dune         | Frank Herbert    | 1984 |               |                  |
      | W4   | Clean Code   | Robert C. Martin | 2008 | 9780132350884 |                  |

  Scenario Outline: The cascade follows the confidence table of the PRD
    When a file arrives with the title "<title>", the authors "<authors>", the year "<year>", the ISBN "<isbn13>" and the fingerprint "<fingerprint>"
    Then the decision is "<decision>" with the confidence <confidence>

    Examples:
      | title                       | authors            | year | isbn13        | fingerprint | decision                  | confidence |
      | The Hobbit                  | J. R. R. Tolkien   | 1937 | 9780261102217 |             | merge into W1             | 1.0        |
      | Something Else Entirely     | Someone Else       |      | 9780261102217 |             | merge into W1             | 1.0        |
      | The Hobbit                  | Tolkien, J. R. R.  | 1937 |               |             | merge into W1             | 0.95       |
      | Hobbit                      | John Tolkien       |      |               |             | merge into W1             | 0.95       |
      | The Hobbit (2nd Edition)    | Tolkien            | 1937 |               |             | merge into W1             | 0.95       |
      | The Hobbit                  | J. R. R. Tolkien   | 1951 |               |             | new work, review W1       | 0.6        |
      | Something Else Entirely     | Someone Else       |      |               | W1~0        | merge into W1             | 0.9        |
      | Something Else Entirely     | Someone Else       |      |               | W1~3        | merge into W1             | 0.9        |
      | Something Else Entirely     | Someone Else       |      |               | W1~4        | new work, review W1       | 0.6        |
      | Something Else Entirely     | Someone Else       |      |               | W1~5        | new work, review W1       | 0.6        |
      | Something Else Entirely     | Someone Else       |      |               | W1~6        | new work                  | 0          |
      | Dune                        | Frank Herbert      | 1965 |               |             | merge into W2             | 0.95       |
      | Dune                        | Frank Herbert      | 1984 |               |             | merge into W3             | 0.95       |
      | Dune                        | Frank Smith        | 1965 |               |             | new work                  | 0          |
      | The Hobbit                  |                    | 1937 |               |             | new work                  | 0          |
      | Clean Code                  | Robert Martin      |      |               |             | merge into W4             | 0.95       |
      | Clean Code                  | Martin Fowler      |      |               |             | new work                  | 0          |
      | Unrelated                   | Nobody             |      |               |             | new work                  | 0          |
      | The Hobbit                  | J. R. R. Tolkien   | 1937 | 9780306406157 |             | new work, review W1       | 0.6        |
      | The Hobbit                  | J. R. R. Tolkien   | 1937 |               | W1~6        | new work, review W1       | 0.6        |
      | The Hobbit                  | J. R. R. Tolkien   | 1937 |               | W1~5        | merge into W1             | 0.95       |
      | Something Else Entirely     | Someone Else       |      | 9780306406157 | W1~2        | new work, review W1       | 0.6        |

  Scenario: Two works that fit equally well are never merged automatically
    When a file arrives with the title "Dune", the authors "Frank Herbert", the year "", the ISBN "" and the fingerprint ""
    Then the decision is "new work, review W2 and W3" with the confidence 0.95

  Scenario: The first sure stage decides, even if a later stage points elsewhere
    When a file arrives with the title "Dune", the authors "Frank Herbert", the year "1965", the ISBN "9780261102217" and the fingerprint "W2~0"
    Then the decision is "merge into W1" with the confidence 1.0

  Scenario: A file that was marked as not a duplicate is not merged into that work
    Given the work "W2" has the file "Dune (copy).epub" marked as not a duplicate
    When a file named "Dune (copy).epub" arrives with the title "Dune", the authors "Frank Herbert", the year "1965", the ISBN "" and the fingerprint ""
    Then the decision is "new work, review W3" with the confidence 0.6

  Scenario: A new work is a candidate for the files that follow
    When a file named "one.epub" arrives with the title "Neuromancer", the authors "William Gibson", the year "1984", the ISBN "" and the fingerprint ""
    And the file is stored as a new work "W5"
    And a file named "two.pdf" arrives with the title "Neuromancer", the authors "W. Gibson", the year "1984", the ISBN "" and the fingerprint ""
    Then the decision is "merge into W5" with the confidence 0.95

  Scenario Outline: The thresholds of the configuration are honored
    Given the configuration contains the line "<setting>"
    When a file arrives with the title "<title>", the authors "<authors>", the year "<year>", the ISBN "" and the fingerprint "<fingerprint>"
    Then the decision is "<decision>" with the confidence <confidence>

    Examples:
      | setting                     | title                   | authors          | year | fingerprint | decision            | confidence |
      | grouping.auto_merge = 0.95  | Something Else Entirely | Someone Else     |      | W1~2        | new work, review W1 | 0.9        |
      | grouping.auto_merge = 0.95  | The Hobbit              | J. R. R. Tolkien | 1937 |             | merge into W1       | 0.95       |
      | grouping.auto_merge = 0.6   | The Hobbit              | J. R. R. Tolkien | 1951 |             | merge into W1       | 0.6        |
      | grouping.review = 0.7       | The Hobbit              | J. R. R. Tolkien | 1951 |             | new work            | 0          |
      | grouping.max_hamming = 6    | Something Else Entirely | Someone Else     |      | W1~7        | new work            | 0          |
      | grouping.max_hamming = 6    | Something Else Entirely | Someone Else     |      | W1~6        | new work, review W1 | 0.6        |
      | grouping.auto_hamming = 1   | Something Else Entirely | Someone Else     |      | W1~2        | new work, review W1 | 0.6        |
      | grouping.auto_hamming = 5   | Something Else Entirely | Someone Else     |      | W1~5        | merge into W1       | 0.9        |

  Scenario Outline: A file of a format the work does not have yet ignores a differing ISBN if the text is alike
    Given the work "W1" has files of the formats "<work formats>"
    When a file named "<name>" arrives with the title "The Hobbit", the authors "J. R. R. Tolkien", the year "<year>", the ISBN "<isbn13>" and the fingerprint "<fingerprint>"
    Then the decision is "<decision>" with the confidence <confidence>

    Examples:
      | work formats | name            | year | isbn13        | fingerprint | decision            | confidence |
      | epub         | The Hobbit.pdf  | 1937 | 9780306406157 | W1~3        | merge into W1       | 0.95       |
      | epub         | The Hobbit.pdf  | 1937 | 9780306406157 | W1~4        | new work, review W1 | 0.6        |
      | epub         | The Hobbit.pdf  | 1937 | 9780306406157 |             | new work, review W1 | 0.6        |
      | epub         | The Hobbit.pdf  |      | 9780306406157 | W1~2        | merge into W1       | 0.95       |
      | epub         | The Hobbit.pdf  | 1937 | 9780306406157 | W1~6        | new work, review W1 | 0.6        |
      | epub         | The Hobbit.epub | 1937 | 9780306406157 | W1~3        | new work, review W1 | 0.6        |
      | epub, pdf    | The Hobbit.pdf  | 1937 | 9780306406157 | W1~3        | new work, review W1 | 0.6        |
      | epub         | The Hobbit.pdf  | 1951 | 9780306406157 | W1~3        | new work, review W1 | 0.6        |

  Scenario: The text alone does not merge a file of another format when the ISBNs differ
    Given the work "W1" has files of the formats "epub"
    When a file named "Other Title.pdf" arrives with the title "Something Else Entirely", the authors "Someone Else", the year "", the ISBN "9780306406157" and the fingerprint "W1~2"
    Then the decision is "new work, review W1" with the confidence 0.6
