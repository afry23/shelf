@F-PAP-3 @M9
Feature: Loading paper metadata from Crossref and arXiv
  As a user whose papers carry poor metadata
  I want shelf enrich to look a paper up by DOI or arXiv ID
  So that title, authors, venue, year and abstract are filled in, but only when I say so

  Background:
    Given an empty working directory
    And I have run shelf with arguments "init library"
    And a paper "attention" with the arXiv ID "1706.03762" and the title "Attention Is All You Need" and no authors
    And the online source "arxiv" holds the paper:
      | field    | value                                         |
      | arxiv    | 1706.03762                                    |
      | title    | Attention Is All You Need                     |
      | authors  | Ashish Vaswani, Noam Shazeer, Niki Parmar     |
      | year     | 2017                                          |
      | abstract | The dominant sequence transduction models.    |
    And the online source "crossref" holds the paper:
      | field    | value                                         |
      | doi      | 10.1109/CVPR.2016.90                          |
      | title    | Deep Residual Learning for Image Recognition  |
      | authors  | Kaiming He, Xiangyu Zhang                     |
      | venue    | CVPR                                          |
      | year     | 2016                                          |

  Scenario: The arXiv ID is looked up at arXiv and the preview writes nothing
    When I run shelf with arguments "enrich library/attention --library library"
    Then the exit code is 0
    And standard output contains "from arxiv"
    And standard output contains "+ authors: Ashish Vaswani, Noam Shazeer, Niki Parmar"
    And standard output contains "+ abstract: The dominant sequence transduction models."
    And standard output contains "Nothing written; run again with --apply"
    And the work "Attention Is All You Need" has no authors in the index

  Scenario: Applying fills the empty fields and the cite key
    When I run shelf with arguments "enrich library/attention --apply --library library"
    Then the exit code is 0
    And the work "Attention Is All You Need" has the authors "Ashish Vaswani, Noam Shazeer, Niki Parmar"
    And the work "Attention Is All You Need" has the year 2017
    And the work "Attention Is All You Need" has the cite key "vaswani2017attention"

  Scenario: A DOI is looked up at Crossref
    Given a paper "resnet" with the DOI "10.1109/CVPR.2016.90" and the title "Deep Residual Learning for Image Recognition" and no authors
    When I run shelf with arguments "enrich library/resnet --apply --library library"
    Then the exit code is 0
    And standard output contains "from crossref"
    And the work "Deep Residual Learning for Image Recognition" has the venue "CVPR"
    And the work "Deep Residual Learning for Image Recognition" has the authors "Kaiming He, Xiangyu Zhang"

  Scenario: Without an identifier, only an exact title with a shared author surname counts
    Given a paper "unknown" with the title "Deep Residual Learning for Image Recognition" and the authors "Kaiming He" and no identifier
    When I run shelf with arguments "enrich library/unknown --library library"
    Then standard output contains "from crossref"
    And standard output contains "+ doi: 10.1109/CVPR.2016.90"

  Scenario: A title match with a different author is not taken
    Given a paper "other" with the title "Deep Residual Learning for Image Recognition" and the authors "Jane Doe" and no identifier
    When I run shelf with arguments "enrich library/other --library library"
    Then the exit code is 1
    And standard error contains "no clear match"

  Scenario: Nothing goes online without the command
    When I run shelf with arguments "reindex --library library"
    Then no online source was queried

  Scenario: Several papers with the same title are listed with what tells them apart
    Given a paper "reposts" with the title "Deep Residual Learning for Image Recognition" and the authors "Kaiming He" and no identifier
    And the online source "crossref" holds the paper:
      | field     | value                                        |
      | doi       | 10.65215/ysbyhc05                            |
      | title     | Deep Residual Learning for Image Recognition |
      | authors   | Kaiming He                                   |
      | publisher | A Repository of Reposts                      |
      | year      | 2025                                         |
    When I run shelf with arguments "enrich library/reposts --library library"
    Then the exit code is 1
    And standard error contains "2 candidates; pick one with --pick"
    And standard output contains "1. Deep Residual Learning for Image Recognition (Kaiming He & Xiangyu Zhang, 2016, 10.1109/CVPR.2016.90, CVPR)"
    And standard output contains "2. Deep Residual Learning for Image Recognition (Kaiming He, A Repository of Reposts, 2025, 10.65215/ysbyhc05)"

  Scenario: A second source with a single match decides before the user has to choose
    Given a paper "reposts" with the title "Attention Is All You Need" and the authors "Ashish Vaswani" and no identifier
    And the online source "crossref" holds the paper:
      | field   | value                     |
      | doi     | 10.65215/aaaa             |
      | title   | Attention Is All You Need |
      | authors | Ashish Vaswani            |
      | year    | 2025                      |
    And the online source "crossref" holds the paper:
      | field   | value                     |
      | doi     | 10.65215/bbbb             |
      | title   | Attention Is All You Need |
      | authors | Ashish Vaswani            |
      | year    | 2025                      |
    When I run shelf with arguments "enrich library/reposts --library library"
    Then the exit code is 0
    And standard output contains "from arxiv"
    And standard output contains "+ arxiv: 1706.03762"

  Scenario: Without a second source the candidates are still offered
    Given a paper "reposts" with the title "Attention Is All You Need" and the authors "Ashish Vaswani" and no identifier
    And the online source "arxiv" holds no books
    And the online source "crossref" holds the paper:
      | field   | value                     |
      | doi     | 10.65215/aaaa             |
      | title   | Attention Is All You Need |
      | authors | Ashish Vaswani            |
    And the online source "crossref" holds the paper:
      | field   | value                     |
      | doi     | 10.65215/bbbb             |
      | title   | Attention Is All You Need |
      | authors | Ashish Vaswani            |
    When I run shelf with arguments "enrich library/reposts --library library"
    Then the exit code is 1
    And standard error contains "2 candidates; pick one with --pick"
