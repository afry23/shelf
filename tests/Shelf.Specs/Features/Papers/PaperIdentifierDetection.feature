@F-PAP-2 @M9
Feature: DOI and arXiv ID detection
  As a user who imports scientific papers
  I want shelf to find the DOI or arXiv ID of a PDF
  So that a paper is recognized as a paper and can be looked up and cited

  Scenario Outline: DOIs are found in text and normalized
    When shelf searches the text "<text>" for a DOI
    Then the DOI is "<doi>"

    Examples:
      | text                                                   | doi                       |
      | doi:10.1145/3295500.3356159                            | 10.1145/3295500.3356159   |
      | DOI 10.1038/nature14539.                               | 10.1038/nature14539       |
      | https://doi.org/10.1109/CVPR.2016.90                   | 10.1109/CVPR.2016.90      |
      | https://dx.doi.org/10.1007/s00799-020-00288-x)         | 10.1007/s00799-020-00288-x |
      | DOI: 10.1145/3295500.3356159, pages 1-12               | 10.1145/3295500.3356159   |
      | DOI: 10.1002/(SICI)1097-4571(199806)49:8<693::AID-ASI4>3.0.CO;2-O | 10.1002/(SICI)1097-4571(199806)49:8<693::AID-ASI4>3.0.CO;2-O |
      | Doi: 10.1145/3295500.3356159                           | 10.1145/3295500.3356159   |

  Scenario Outline: Text that is not a DOI is rejected
    When shelf searches the text "<text>" for a DOI
    Then there is no DOI

    Examples:
      | text                         | reason                         |
      | version 10.14 of the tool    | no registrant code with a slash |
      | 10.1145                      | no suffix                      |
      | pages 10.5/12                | registrant code too short      |
      |                              | empty                          |

  Scenario Outline: arXiv IDs are found in file names and text
    When shelf searches the text "<text>" for an arXiv ID
    Then the arXiv ID is "<id>"

    Examples:
      | text                                | id                |
      | 2301.12345v2.pdf                    | 2301.12345        |
      | arXiv:1706.03762v5 [cs.CL]          | 1706.03762        |
      | arXiv:0704.0001                     | 0704.0001         |
      | hep-th/9901001.pdf                  | hep-th/9901001    |
      | arXiv:math.GT/0309136v1             | math.GT/0309136   |

  Scenario Outline: Numbers that are not arXiv IDs are rejected
    When shelf searches the text "<text>" for an arXiv ID
    Then there is no arXiv ID

    Examples:
      | text                     | reason                       |
      | 1234.5678.pdf            | month 34 does not exist      |
      | 2301.123.pdf             | fewer than 4 digits after the dot |
      | version 3.14159          | not a date prefix            |

  Scenario: A PDF with a DOI on its first page becomes a paper
    Given an empty working directory
    And I have run shelf with arguments "init library"
    And a sample PDF "library/inbox/attention.pdf" with the title "Attention Is All You Need", the author "Ashish Vaswani" and the first page text "arXiv:1706.03762v5 [cs.CL] 2 Aug 2023"
    When I run shelf with arguments "import library/inbox --library library"
    Then the exit code is 0
    And the work "Attention Is All You Need" has the kind "paper"
    And the work "Attention Is All You Need" has the arXiv ID "1706.03762"

  Scenario: A PDF without any identifier stays a book
    Given an empty working directory
    And I have run shelf with arguments "init library"
    And a sample PDF "library/inbox/pea.pdf" with the title "Patterns of Enterprise Application Architecture", the author "Martin Fowler" and the text seed 1
    When I run shelf with arguments "import library/inbox --library library"
    Then the exit code is 0
    And the work "Patterns of Enterprise Application Architecture" has the kind "book"
