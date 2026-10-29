@F-PAP-8 @M9
Feature: Finding a free PDF of a paper with Unpaywall
  As a researcher who has the DOI of a paper that is not on arXiv
  I want shelf fetch to ask Unpaywall for a legal free copy
  So that the paper comes into the library without downloading it by hand

  Background:
    Given an empty working directory
    And I have run shelf with arguments "init library"
    And the online source "crossref" holds the paper:
      | field   | value                                        |
      | doi     | 10.1109/CVPR.2016.90                         |
      | title   | Deep Residual Learning for Image Recognition |
      | authors | Kaiming He, Xiangyu Zhang                    |
      | venue   | CVPR                                         |
      | year    | 2016                                         |

  Scenario: Without an e-mail address Unpaywall is not asked, and the error says how to enable it
    Given the online source "unpaywall" has a PDF for the DOI "10.1109/cvpr.2016.90"
    When I run shelf with arguments "fetch 10.1109/CVPR.2016.90 --library library"
    Then the exit code is 1
    And standard error contains "no open-access PDF"
    And standard error contains "fetch.unpaywall_email"
    And the online source "unpaywall" was not queried
    And the table "works" of the index has 0 rows

  Scenario: With an e-mail address a DOI becomes a work
    Given the config file of the library "library" contains the line "fetch.unpaywall_email = 'me@example.org'"
    And the online source "unpaywall" has a PDF for the DOI "10.1109/cvpr.2016.90"
    When I run shelf with arguments "fetch https://doi.org/10.1109/CVPR.2016.90 --library library"
    Then the exit code is 0
    And standard output contains "Fetched Deep Residual Learning for Image Recognition"
    And the table "works" of the index has 1 rows
    And the work "Deep Residual Learning for Image Recognition" has the kind "paper"
    And the work "Deep Residual Learning for Image Recognition" has the DOI "10.1109/cvpr.2016.90"
    And the work "Deep Residual Learning for Image Recognition" has the venue "CVPR"
    And the work "Deep Residual Learning for Image Recognition" has the cite key "he2016deep"
    And the file "library/He, Kaiming/Deep Residual Learning for Image Recognition (2016)/10.1109_cvpr.2016.90.pdf" exists

  Scenario: The e-mail address can be in the global config
    Given a global config file with content:
      """
      [fetch]
      unpaywall_email = "me@example.org"
      """
    And the online source "unpaywall" has a PDF for the DOI "10.1109/cvpr.2016.90"
    When I run shelf with arguments "fetch 10.1109/CVPR.2016.90 --library library"
    Then the exit code is 0

  Scenario: arXiv is asked first, and Unpaywall only if arXiv has no PDF
    Given the config file of the library "library" contains the line "fetch.unpaywall_email = 'me@example.org'"
    And the online source "arxiv" holds the paper:
      | field   | value                     |
      | arxiv   | 1706.03762                |
      | title   | Attention Is All You Need |
      | authors | Ashish Vaswani            |
      | year    | 2017                      |
      | doi     | 10.48550/arxiv.1706.03762 |
    And the online source "arxiv" has a PDF for the arXiv ID "1706.03762"
    And the online source "unpaywall" has a PDF for the DOI "10.48550/arxiv.1706.03762"
    When I run shelf with arguments "fetch 1706.03762 --library library"
    Then the exit code is 0
    And the online source "unpaywall" was not queried

  Scenario: A DOI that Unpaywall has no PDF for creates nothing
    Given the config file of the library "library" contains the line "fetch.unpaywall_email = 'me@example.org'"
    When I run shelf with arguments "fetch 10.1109/CVPR.2016.90 --library library"
    Then the exit code is 1
    And standard error contains "no open-access PDF"
    And standard error does not contain "fetch.unpaywall_email"
    And the online source "unpaywall" was queried
    And the table "works" of the index has 0 rows

  Scenario: An Unpaywall that cannot be reached is reported
    Given the config file of the library "library" contains the line "fetch.unpaywall_email = 'me@example.org'"
    And the online source "unpaywall" fails with "HTTP 422 Unprocessable Entity"
    When I run shelf with arguments "fetch 10.1109/CVPR.2016.90 --library library"
    Then the exit code is 1
    And standard error contains "unpaywall: HTTP 422 Unprocessable Entity"

  Scenario: Unpaywall is never asked for anything but the PDF of a fetched paper
    Given the config file of the library "library" contains the line "fetch.unpaywall_email = 'me@example.org'"
    When I run shelf with arguments "reindex --library library"
    Then no online source was queried
