@F-PAP-5 @PRD-7.2 @M9
Feature: Citations through MCP
  As an AI assistant that helps a person write
  I want to get the citations of a book, a paper or a search result
  So that I can cite what the library holds without inventing a reference

  Background:
    Given an empty working directory
    And I have run shelf with arguments "init library"
    And a paper "Deep Residual Learning for Image Recognition" by "Kaiming He, Xiangyu Zhang" from 2016 with the DOI "10.1109/CVPR.2016.90" and the venue "CVPR"
    And a paper "Attention Is All You Need" by "Ashish Vaswani" from 2017 with the DOI "10.48550/arXiv.1706.03762" and the venue "NeurIPS"
    And a book "Kubernetes Patterns" by "Bilgin Ibryam" from 2019
    And the sidecar "library/Ibryam, Bilgin/No Key Yet/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000009", "title": "No Key Yet", "authors": ["Unknown Author"], "files": [{ "name": "nk.pdf" }] }
      """
    And a sample PDF "library/Ibryam, Bilgin/No Key Yet/nk.pdf" with the title "No Key Yet", the author "Unknown Author" and the text seed 9
    And I have run shelf with arguments "reindex --library library"
    And I have run shelf with arguments "citekeys --library library"
    And the MCP server is running on the library "library"

  Scenario: export_citations gives BibTeX for the whole library by default
    When I call the tool "export_citations" with the arguments:
      """
      {}
      """
    Then the tool call succeeds
    And the result has the string "format" equal to "bibtex"
    And the result has the string "content" containing "@inproceedings{he2016deep,"
    And the result has the string "content" containing "doi = {10.1109/CVPR.2016.90}"
    And the result has the string "content" containing "@book{ibryam2019kubernetes,"
    And the result has the number "works" equal to 3
    And the result has the array "skipped" with 1 items

  Scenario: A query limits the works
    When I call the tool "export_citations" with the arguments:
      """
      { "query": "venue:CVPR" }
      """
    Then the result has the number "works" equal to 1
    And the result has the string "content" containing "he2016deep"

  Scenario Outline: The format can be chosen
    When I call the tool "export_citations" with the arguments:
      """
      { "query": "type:paper", "format": "<format>" }
      """
    Then the result has the string "format" equal to "<format>"
    And the result has the string "content" containing "<marker>"

    Examples:
      | format   | marker          |
      | csl-json | paper-conference |
      | ris      | TY  - CPAPER     |

  Scenario: A work without a cite key cannot be exported by its ID
    When I call the tool "export_citations" with the arguments:
      """
      { "id": "01J9Z3K0000000000000000009" }
      """
    Then the tool call fails with the message containing "cite key"

  Scenario Outline: Unusable arguments are errors of the tool
    When I call the tool "export_citations" with the arguments:
      """
      <arguments>
      """
    Then the tool call fails with the message containing "<message>"

    Examples:
      | arguments                  | message          |
      | { "format": "docx" }       | Unknown format   |
      | { "query": "year:later" }  | year             |
      | { "id": "nope" }           | nope             |
      | { "id": "01J9Z3K0000000000000000009", "query": "x" } | either |

  Scenario: Exporting changes nothing, not even the cite keys
    Given I take a snapshot of the folder "library"
    When I call the tool "export_citations" with the arguments:
      """
      {}
      """
    Then the folder "library" equals the snapshot
