@PRD-7.3 @F-PAP-5 @M9
Feature: Citations over HTTP
  As the library and book views of the web interface
  I want to download the citations of a work or of a search result
  So that a reference manager or a LaTeX project can read them

  Background:
    Given an empty working directory
    And I have run shelf with arguments "init library"
    And a sample PDF "library/He/Deep Residual Learning/resnet.pdf" with the title "Deep Residual Learning for Image Recognition", the author "Kaiming He" and the text seed 1
    And the sidecar "library/He/Deep Residual Learning/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000001", "kind": "paper", "title": "Deep Residual Learning for Image Recognition", "authors": ["Kaiming He", "Xiangyu Zhang"], "year": 2016, "citeKey": "he2016deep", "doi": "10.1109/cvpr.2016.90", "venue": "CVPR", "files": [{ "name": "resnet.pdf" }] }
      """
    And a sample PDF "library/Ibryam/Kubernetes Patterns/kp.pdf" with the title "Kubernetes Patterns", the author "Bilgin Ibryam" and the text seed 2
    And the sidecar "library/Ibryam/Kubernetes Patterns/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000002", "title": "Kubernetes Patterns", "authors": ["Bilgin Ibryam"], "year": 2019, "citeKey": "ibryam2019kubernetes", "files": [{ "name": "kp.pdf" }] }
      """
    And a sample PDF "library/Roe/No Key/nk.pdf" with the title "No Key", the author "Jane Roe" and the text seed 3
    And the sidecar "library/Roe/No Key/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000003", "title": "No Key", "authors": ["Jane Roe"], "files": [{ "name": "nk.pdf" }] }
      """
    And I have run shelf with arguments "reindex --library library"
    And the web server is running on the library "library"

  Scenario Outline: All citations with a cite key are a download in the chosen format
    When I request "GET /api/v1/citations?format=<format>"
    Then the response status is 200
    And the response header "Content-Type" starts with "<type>"
    And the response header "Content-Disposition" contains "attachment"
    And the response header "Content-Disposition" contains "<file>"
    And the response header "X-Shelf-Works" is "2"
    And the response header "X-Shelf-Skipped" is "1"
    And the response body contains "<marker>"
    And the response body contains "10.1109/cvpr.2016.90"

    Examples:
      | format   | type                                  | file       | marker                        |
      | bibtex   | text/x-bibtex                         | shelf.bib  | @inproceedings{he2016deep,    |
      | csl-json | application/vnd.citationstyles.csl+json | shelf.json | paper-conference            |
      | ris      | application/x-research-info-systems   | shelf.ris  | TY  - CPAPER                  |

  Scenario: BibTeX is the default format
    When I request "GET /api/v1/citations"
    Then the response status is 200
    And the response header "Content-Disposition" contains "shelf.bib"

  Scenario: A query limits the works, in the language of the search
    When I request "GET /api/v1/citations?query=venue%3ACVPR"
    Then the response status is 200
    And the response header "X-Shelf-Works" is "1"
    And the response header "X-Shelf-Skipped" is "0"
    And the response body contains "he2016deep"
    And the response body does not contain "ibryam2019kubernetes"

  Scenario: Exporting writes nothing, so a work without a cite key stays without one
    When I request "GET /api/v1/citations"
    Then the file "library/Roe/No Key/book.json" does not contain "citeKey"

  Scenario: One work is a download named after its cite key
    When I request "GET /api/v1/works/01J9Z3K0000000000000000001/citation?format=ris"
    Then the response status is 200
    And the response header "Content-Disposition" contains "he2016deep.ris"
    And the response body contains "ID  - he2016deep"
    And the response body does not contain "ibryam2019kubernetes"

  Scenario: A work without a cite key cannot be exported
    When I request "GET /api/v1/works/01J9Z3K0000000000000000003/citation"
    Then the response status is 400
    And the response body contains "cite key"

  Scenario: An unknown work is a 404
    When I request "GET /api/v1/works/01J9Z3K0000000000000000009/citation"
    Then the response status is 404

  Scenario Outline: Unusable requests are refused
    When I request "GET <request>"
    Then the response status is 400
    And the response body contains "<message>"

    Examples:
      | request                              | message         |
      | /api/v1/citations?format=docx        | Unknown format  |
      | /api/v1/citations?query=year%3Alater | year            |
      | /api/v1/works/01J9Z3K0000000000000000001/citation?format=docx | Unknown format |
