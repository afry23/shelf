@F-PAP-9 @M9
Feature: Grouping papers by DOI and arXiv ID
  As a researcher who imports papers from several sources
  I want the DOI and the arXiv ID to decide which files are one paper
  So that versions of a paper are one work and a preprint and its published version stay two related works

  Background:
    Given an empty working directory
    And I have run shelf with arguments "init library"

  Scenario: Versions of an arXiv paper are one work, even if title and text differ
    Given a sample PDF "incoming/first/v1.pdf" with the title "Attention Is All You Need", the author "Ashish Vaswani", the first page text "arXiv:1706.03762v1 [cs.CL] 12 Jun 2017" and the text seed 1
    And I have run shelf with arguments "import incoming/first --library library"
    And a sample PDF "incoming/second/v5.pdf" with the title "Attention Is All You Need (revised)", the author "A. Vaswani", the first page text "arXiv:1706.03762v5 [cs.CL] 2 Aug 2023" and the text seed 2
    When I run shelf with arguments "import incoming/second --library library"
    Then the exit code is 0
    And the table "works" of the index has 1 rows
    And the table "files" of the index has 2 rows

  Scenario Outline: The same DOI is one work, whatever its spelling
    Given a sample PDF "incoming/first/a.pdf" with the title "Deep Residual Learning for Image Recognition", the author "Kaiming He", the first page text "<first>" and the text seed 1
    And I have run shelf with arguments "import incoming/first --library library"
    And a sample PDF "incoming/second/b.pdf" with the title "ResNet", the author "K. He", the first page text "<second>" and the text seed 2
    When I run shelf with arguments "import incoming/second --library library"
    Then the table "works" of the index has 1 rows

    Examples:
      | first                           | second                                   |
      | doi:10.1109/CVPR.2016.90        | DOI: 10.1109/CVPR.2016.90.               |
      | doi:10.1109/CVPR.2016.90        | https://doi.org/10.1109/cvpr.2016.90     |

  Scenario Outline: A preprint and the published version are two works that are related
    Given a sample PDF "incoming/first/pre.pdf" with the title "Attention Is All You Need", the author "Ashish Vaswani", the first page text "<first>" and the text seed 1
    And I have run shelf with arguments "import incoming/first --library library"
    And a sample PDF "incoming/second/pub.pdf" with the title "Attention Is All You Need", the author "Ashish Vaswani", the first page text "<second>" and the text seed 2
    When I run shelf with arguments "import incoming/second --library library"
    Then the exit code is 0
    And the table "works" of the index has 2 rows
    And the file "library/Vaswani, Ashish/Attention Is All You Need/book.json" contains "related"
    And the file "library/Vaswani, Ashish/Attention Is All You Need (2)/book.json" contains "related"

    Examples:
      | first                      | second                         |
      | arXiv:1706.03762v5 [cs.CL] | doi:10.5555/3295222.3295349    |
      | doi:10.5555/3295222.3295349 | doi:10.48550/arXiv.1706.03762 |

  Scenario: A file without an identifier still joins the paper with the same title and text
    Given a sample PDF "incoming/first/a.pdf" with the title "Attention Is All You Need", the author "Ashish Vaswani", the first page text "arXiv:1706.03762v5 [cs.CL]" and the text seed 1
    And I have run shelf with arguments "import incoming/first --library library"
    And a sample PDF "incoming/second/b.pdf" with the title "Attention Is All You Need", the author "Ashish Vaswani", the first page text "Downloaded from the author's homepage." and the text seed 1
    When I run shelf with arguments "import incoming/second --library library"
    Then the table "works" of the index has 1 rows
