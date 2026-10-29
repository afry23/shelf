@F-PAP-7 @M9
Feature: Searching papers
  As a researcher
  I want to filter by type, DOI, arXiv ID and venue, and to search the abstract
  So that I find a paper by what it is, where it appeared and what it says

  Background:
    Given an empty working directory
    And I have run shelf with arguments "init library"
    And a paper "Attention Is All You Need" by "Ashish Vaswani" from 2017 with the DOI "10.48550/arXiv.1706.03762" and the venue "Advances in NeurIPS 30"
    And a paper "Deep Residual Learning for Image Recognition" by "Kaiming He" from 2016 with the DOI "10.1109/CVPR.2016.90" and the venue "CVPR"
    And a book "Kubernetes Patterns" by "Bilgin Ibryam" from 2019

  Scenario Outline: Filters select papers by their identifiers and venue
    When I run shelf with arguments "search <query> --library library"
    Then the exit code is 0
    And standard output contains "<found>"
    And standard output does not contain "<absent>"

    Examples:
      | query                                 | found                                        | absent                    |
      | type:paper                            | Attention Is All You Need                    | Kubernetes Patterns       |
      | type:book                             | Kubernetes Patterns                          | Attention Is All You Need |
      | -type:paper                           | Kubernetes Patterns                          | Deep Residual Learning    |
      | doi:10.1109/CVPR.2016.90              | Deep Residual Learning for Image Recognition | Attention Is All You Need |
      | doi:https://doi.org/10.1109/CVPR.2016.90 | Deep Residual Learning for Image Recognition | Attention Is All You Need |
      | doi:10.1109/cvpr.2016.90              | Deep Residual Learning for Image Recognition | Attention Is All You Need |
      | venue:neurips                         | Attention Is All You Need                    | Deep Residual Learning    |
      | venue:CVPR year:>2015                 | Deep Residual Learning for Image Recognition | Attention Is All You Need |

  Scenario: The abstract of a paper is searched like its description
    Given the paper "Attention Is All You Need" has the abstract "The dominant sequence transduction models are based on recurrent networks."
    When I run shelf with arguments "search transduction --library library"
    Then standard output contains "Attention Is All You Need"
    And standard output does not contain "Deep Residual Learning"

  Scenario: A type that does not exist is refused
    When I run shelf with arguments "search type:thesis --library library"
    Then the exit code is 2
    And standard error contains "type: 'thesis' is not one of book, paper"
