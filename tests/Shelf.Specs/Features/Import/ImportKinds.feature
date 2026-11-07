@F-IMP-1 @F-PAP-2 @F-ART-2 @M1
Feature: Telling import what kind of files it gets
  As a user with books, articles and papers
  I want --book, --article and --paper on shelf import
  So that each kind is taken in the right way without guessing

  Background:
    Given an empty working directory
    And I have run shelf with arguments "init library"

  Scenario: Books are the default
    Given a sample EPUB "incoming/book.epub" with the title "Plain Book", the authors "Ann Author" and the text seed 1
    When I run shelf with arguments "import incoming/book.epub --library library"
    Then the exit code is 0
    And the work "Plain Book" has the kind "book"

  Scenario: --book says the same as the default
    Given a sample EPUB "incoming/book.epub" with the title "Plain Book", the authors "Ann Author" and the text seed 1
    When I run shelf with arguments "import --book incoming/book.epub --library library"
    Then the exit code is 0
    And the work "Plain Book" has the kind "book"

  Scenario Outline: The three kinds exclude each other
    Given a file "incoming/x.pdf" in the working directory
    When I run shelf with arguments "import <flags> incoming/x.pdf --library library"
    Then the exit code is 2
    And standard error contains "give only one of --book, --article and --paper"

    Examples:
      | flags               |
      | --book --article    |
      | --book --paper      |
      | --article --paper   |

  Scenario: Without --article a Markdown file is no book
    Given a Markdown file "incoming/post.md" with content:
      """
      # A Post
      """
    When I run shelf with arguments "import incoming/post.md --library library"
    Then the exit code is 1
    And standard error contains "a Markdown file is no book (use --article)"
    Then the table "works" of the index has 0 rows

  Scenario: --paper makes a PDF without DOI a paper
    Given a sample PDF "incoming/notes.pdf" with the title "Lecture Notes on Types", the author "Ada Lovelace" and the text seed 1
    When I run shelf with arguments "import --paper incoming/notes.pdf --library library"
    Then the exit code is 0
    And the work "Lecture Notes on Types" has the kind "paper"

  Scenario: Without --paper the same PDF is a book
    Given a sample PDF "incoming/notes.pdf" with the title "Lecture Notes on Types", the author "Ada Lovelace" and the text seed 1
    When I run shelf with arguments "import incoming/notes.pdf --library library"
    Then the exit code is 0
    And the work "Lecture Notes on Types" has the kind "book"

  Scenario: --paper keeps the DOI that is found
    Given a sample PDF "incoming/resnet.pdf" with the title "Deep Residual Learning", the author "Kaiming He" and the first page text "doi:10.1109/CVPR.2016.90"
    When I run shelf with arguments "import --paper incoming/resnet.pdf --library library"
    Then the exit code is 0
    And the work "Deep Residual Learning" has the kind "paper"
    And the work "Deep Residual Learning" has the DOI "10.1109/cvpr.2016.90"

  Scenario: --paper takes only PDF files
    Given a sample EPUB "incoming/book.epub" with the title "Not A Paper", the authors "Ann Author" and the text seed 1
    When I run shelf with arguments "import --paper incoming/book.epub --library library"
    Then the exit code is 1
    And standard error contains "not a PDF file"
    And the table "works" of the index has 0 rows

  Scenario: --article takes only Markdown files
    Given a sample PDF "incoming/x.pdf" with the title "A PDF", the author "Ann Author" and the text seed 1
    When I run shelf with arguments "import --article incoming/x.pdf --library library"
    Then the exit code is 1
    And standard error contains "not a Markdown file"
    And the table "works" of the index has 0 rows
