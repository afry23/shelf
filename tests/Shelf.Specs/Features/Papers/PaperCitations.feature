@F-PAP-4 @F-PAP-5 @M9
Feature: Cite keys and citation export
  As a researcher who writes papers
  I want a stable cite key per work and a BibTeX export
  So that I can cite what I read without a second reference manager

  Background:
    Given an empty working directory
    And I have run shelf with arguments "init library"

  Scenario Outline: Cite keys are built from author, year and first significant title word
    When shelf builds a cite key for the title "<title>", the authors "<authors>" and the year <year>
    Then the cite key is "<key>"

    Examples:
      | title                                   | authors                      | year | key                |
      | Attention Is All You Need               | Ashish Vaswani, Noam Shazeer | 2017 | vaswani2017attention |
      | The Anatomy of a Large-Scale Search     | Sergey Brin                  | 1998 | brin1998anatomy    |
      | On the Origin of Species                | Charles Darwin               | 1859 | darwin1859origin   |
      | Zufall und Notwendigkeit                | Jörg Müller                  | 2001 | muller2001zufall   |
      | A Note on Two Problems                  | Edsger W. Dijkstra           | 1959 | dijkstra1959note   |
      | Deep Residual Learning                  | Kaiming He                   | 2016 | he2016deep         |

  Scenario: A cite key that is already taken gets a letter suffix
    Given a paper "Attention Is All You Need" by "Ashish Vaswani" from 2017 with the cite key "vaswani2017attention"
    And a paper "Attention Mechanisms Revisited" by "Anna Vaswani" from 2017 without a cite key
    When I run shelf with arguments "export --format bibtex --library library"
    Then the exit code is 0
    And standard output contains "@article{vaswani2017attention,"
    And standard output contains "@article{vaswani2017attentiona,"

  Scenario: An existing cite key is never changed
    Given a paper "Attention Is All You Need" by "Ashish Vaswani" from 2017 with the cite key "transformer"
    When I run shelf with arguments "export --format bibtex --library library"
    Then standard output contains "@article{transformer,"
    And standard output does not contain "vaswani2017attention"

  Scenario: BibTeX export of a paper carries DOI, venue and abstract
    Given a paper "Deep Residual Learning for Image Recognition" by "Kaiming He, Xiangyu Zhang" from 2016 with the DOI "10.1109/CVPR.2016.90" and the venue "CVPR"
    When I run shelf with arguments "export --format bibtex --library library"
    Then standard output contains "@inproceedings{he2016deep,"
    And standard output contains "title = {Deep Residual Learning for Image Recognition}"
    And standard output contains "author = {He, Kaiming and Zhang, Xiangyu}"
    And standard output contains "doi = {10.1109/CVPR.2016.90}"
    And standard output contains "booktitle = {CVPR}"

  Scenario Outline: The export formats differ in syntax, not in content
    Given a paper "Deep Residual Learning for Image Recognition" by "Kaiming He" from 2016 with the DOI "10.1109/CVPR.2016.90" and the venue "CVPR"
    When I run shelf with arguments "export --format <format> --library library"
    Then standard output contains "<marker>"
    And standard output contains "10.1109/CVPR.2016.90"

    Examples:
      | format   | marker                  |
      | bibtex   | @inproceedings{he2016   |
      | csl-json | paper-conference        |
      | ris      | TY  - CPAPER            |

  Scenario: Export is limited by a query
    Given a paper "Attention Is All You Need" by "Ashish Vaswani" from 2017 with the DOI "10.48550/arXiv.1706.03762" and the venue "NeurIPS"
    And a paper "Deep Residual Learning for Image Recognition" by "Kaiming He" from 2016 with the DOI "10.1109/CVPR.2016.90" and the venue "CVPR"
    When I run shelf with arguments "export --format bibtex --library library venue:CVPR"
    Then standard output contains "he2016deep"
    And standard output does not contain "vaswani2017attention"

  Scenario: An unknown format is refused
    When I run shelf with arguments "export --format docx --library library"
    Then the exit code is 2
    And standard error contains "Unknown format"
