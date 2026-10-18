@F-GRP-5 @M2
Feature: Editions of a book are different works that are marked as related
  As a user with the 2nd and the 3rd edition of a book
  I want shelf to keep them as two works and to tell me that they belong together
  So that I can find the other edition without the two being merged

  Background:
    Given an empty working directory
    And I have run shelf with arguments "init library"

  Scenario: A file with another ISBN of a known book becomes a work of its own that is related to it
    Given a sample EPUB "incoming/first/pea.epub" with the title "Patterns of Enterprise Application Architecture", the authors "Martin Fowler", the ISBN "9780321127426" and the text seed 1
    And I have run shelf with arguments "import incoming/first --library library"
    And a sample EPUB "incoming/second/pea.epub" with the title "Patterns of Enterprise Application Architecture", the authors "Martin Fowler", the ISBN "9780321200686" and the text seed 1
    When I run shelf with arguments "import incoming/second --library library"
    Then the exit code is 0
    And the table "works" of the index has 2 rows

  Scenario: Both works name the other one, and show lists it
    Given a sample EPUB "incoming/first/pea.epub" with the title "Patterns of Enterprise Application Architecture", the authors "Martin Fowler", the ISBN "9780321127426" and the text seed 1
    And I have run shelf with arguments "import incoming/first --library library"
    And a sample EPUB "incoming/second/pea.epub" with the title "Patterns of Enterprise Application Architecture", the authors "Martin Fowler", the ISBN "9780321200686" and the text seed 1
    And I have run shelf with arguments "import incoming/second --library library"
    When I run shelf with arguments "show 'library/Fowler, Martin/Patterns of Enterprise Application Architecture' --library library"
    Then standard output matches the pattern "(?m)^Related\s+Patterns of Enterprise Application Architecture\s+01[0-9A-HJKMNP-TV-Z]{24}$"
    And the file "library/Fowler, Martin/Patterns of Enterprise Application Architecture/book.json" contains "related"
    And the file "library/Fowler, Martin/Patterns of Enterprise Application Architecture (2)/book.json" contains "related"

  Scenario: The JSON of show has the related works
    Given a sample EPUB "incoming/first/pea.epub" with the title "Patterns of Enterprise Application Architecture", the authors "Martin Fowler", the ISBN "9780321127426" and the text seed 1
    And I have run shelf with arguments "import incoming/first --library library"
    And a sample EPUB "incoming/second/pea.epub" with the title "Patterns of Enterprise Application Architecture", the authors "Martin Fowler", the ISBN "9780321200686" and the text seed 1
    And I have run shelf with arguments "import incoming/second --library library"
    When I run shelf with arguments "show 'library/Fowler, Martin/Patterns of Enterprise Application Architecture (2)' --library library --json"
    Then the JSON property "related" of standard output has 1 items

  Scenario: Two files of the same book are not related, they are one work
    Given a sample EPUB "incoming/first/pea.epub" with the title "Patterns of Enterprise Application Architecture", the authors "Martin Fowler" and the text seed 1
    And I have run shelf with arguments "import incoming/first --library library"
    And a sample PDF "incoming/second/pea.pdf" with the title "Patterns of Enterprise Application Architecture", the author "Martin Fowler" and the text seed 1
    When I run shelf with arguments "import incoming/second --library library"
    Then the table "works" of the index has 1 rows
    And the file "library/Fowler, Martin/Patterns of Enterprise Application Architecture/book.json" does not contain "related"

  Scenario: A work that is not related to anything shows no related line
    Given a sample EPUB "incoming/first/pea.epub" with the title "Patterns of Enterprise Application Architecture", the authors "Martin Fowler" and the text seed 1
    And I have run shelf with arguments "import incoming/first --library library"
    When I run shelf with arguments "show 'library/Fowler, Martin/Patterns of Enterprise Application Architecture' --library library"
    Then standard output does not contain "Related"

  Scenario: Merging two works that are related removes the relation
    Given a sample EPUB "incoming/first/pea.epub" with the title "Patterns of Enterprise Application Architecture", the authors "Martin Fowler", the ISBN "9780321127426" and the text seed 1
    And I have run shelf with arguments "import incoming/first --library library"
    And a sample EPUB "incoming/second/pea.epub" with the title "Patterns of Enterprise Application Architecture", the authors "Martin Fowler", the ISBN "9780321200686" and the text seed 1
    And I have run shelf with arguments "import incoming/second --library library"
    When I run shelf with arguments "merge 'library/Fowler, Martin/Patterns of Enterprise Application Architecture' 'library/Fowler, Martin/Patterns of Enterprise Application Architecture (2)' --library library"
    Then the exit code is 0
    And the file "library/Fowler, Martin/Patterns of Enterprise Application Architecture/book.json" does not contain "related"
