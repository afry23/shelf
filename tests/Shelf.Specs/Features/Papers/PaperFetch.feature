@F-PAP-8 @M9
Feature: Adding a paper by its DOI or arXiv ID
  As a researcher with a reference in my hand
  I want shelf fetch to load the metadata and the open-access PDF of a paper
  So that I do not have to download, rename and import it by hand

  Background:
    Given an empty working directory
    And I have run shelf with arguments "init library"
    And the online source "arxiv" holds the paper:
      | field    | value                                         |
      | arxiv    | 1706.03762                                    |
      | title    | Attention Is All You Need                     |
      | authors  | Ashish Vaswani, Noam Shazeer, Niki Parmar     |
      | year     | 2017                                          |
      | abstract | The dominant sequence transduction models.    |
    And the online source "arxiv" has a PDF for the arXiv ID "1706.03762"

  Scenario Outline: A paper is created from an arXiv ID in any of its spellings
    When I run shelf with arguments "fetch <reference> --library library"
    Then the exit code is 0
    And standard output contains "Fetched Attention Is All You Need"
    And the table "works" of the index has 1 rows
    And the table "files" of the index has 1 rows
    And the work "Attention Is All You Need" has the kind "paper"
    And the work "Attention Is All You Need" has the arXiv ID "1706.03762"
    And the work "Attention Is All You Need" has the authors "Ashish Vaswani, Noam Shazeer, Niki Parmar"
    And the work "Attention Is All You Need" has the year 2017
    And the work "Attention Is All You Need" has the cite key "vaswani2017attention"

    Examples:
      | reference                                  |
      | 1706.03762                                 |
      | arXiv:1706.03762v5                         |
      | https://arxiv.org/abs/1706.03762v5         |
      | https://arxiv.org/pdf/1706.03762           |
      | 10.48550/arXiv.1706.03762                  |
      | https://doi.org/10.48550/arXiv.1706.03762  |

  Scenario: The abstract and the identifier are stored in the sidecar
    When I run shelf with arguments "fetch 1706.03762 --library library"
    Then the file "library/Vaswani, Ashish/Attention Is All You Need (2017)/book.json" contains "The dominant sequence transduction models."
    And the file "library/Vaswani, Ashish/Attention Is All You Need (2017)/book.json" contains '"kind": "paper"'

  Scenario: A paper that is already in the library is not fetched again
    Given I have run shelf with arguments "fetch 1706.03762 --library library"
    When I run shelf with arguments "fetch arXiv:1706.03762v2 --library library"
    Then the exit code is 1
    And standard error contains "already in the library"
    And the table "works" of the index has 1 rows

  Scenario: A DOI whose paper has no known open-access PDF creates nothing
    Given the online source "crossref" holds the paper:
      | field   | value                                        |
      | doi     | 10.1109/CVPR.2016.90                         |
      | title   | Deep Residual Learning for Image Recognition |
      | authors | Kaiming He, Xiangyu Zhang                    |
      | venue   | CVPR                                         |
      | year    | 2016                                         |
    When I run shelf with arguments "fetch https://doi.org/10.1109/CVPR.2016.90 --library library"
    Then the exit code is 1
    And standard error contains "no open-access PDF"
    And the table "works" of the index has 0 rows

  Scenario: A paper that no source knows creates nothing
    Given the online source "arxiv" holds no books
    When I run shelf with arguments "fetch 1706.03762 --library library"
    Then the exit code is 1
    And standard error contains "no online source knows"
    And the table "works" of the index has 0 rows

  Scenario: A source that cannot be reached is reported
    Given the online source "arxiv" fails with "connection refused"
    When I run shelf with arguments "fetch 1706.03762 --library library"
    Then the exit code is 1
    And standard error contains "arxiv: connection refused"

  Scenario Outline: Something that is neither a DOI nor an arXiv ID is a usage error
    When I run shelf with arguments "fetch <reference> --library library"
    Then the exit code is 2
    And standard error contains "neither a DOI nor an arXiv ID"
    And no online source was queried

    Examples:
      | reference     |
      | not-an-id     |
      | 1234.5678     |
      | 10.14         |

  Scenario: Nothing goes online unless the command is run
    When I run shelf with arguments "reindex --library library"
    Then no online source was queried
