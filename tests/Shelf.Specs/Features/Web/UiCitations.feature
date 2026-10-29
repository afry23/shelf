@ui @F-PAP-5 @M9
Feature: Citations from the browser
  As a researcher who writes with the library at hand
  I want to download the citation of a book or of a search result
  So that I can paste it into my reference manager or my LaTeX project

  Background:
    Given an empty working directory
    And I have run shelf with arguments "init library"
    And a sample PDF "library/He/Deep Residual Learning/resnet.pdf" with the title "Deep Residual Learning for Image Recognition", the author "Kaiming He" and the text seed 1
    And the sidecar "library/He/Deep Residual Learning/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000001", "kind": "paper", "title": "Deep Residual Learning for Image Recognition", "authors": ["Kaiming He"], "year": 2016, "citeKey": "he2016deep", "venue": "CVPR", "files": [{ "name": "resnet.pdf" }] }
      """
    And a sample PDF "library/Weiss/Verteilte Systeme/vs.pdf" with the title "Verteilte Systeme verstehen", the author "Lukas Weiß" and the text seed 2
    And the sidecar "library/Weiss/Verteilte Systeme/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000003", "title": "Verteilte Systeme verstehen", "authors": ["Lukas Weiß"], "files": [{ "name": "vs.pdf" }] }
      """
    And I have run shelf with arguments "reindex --library library"
    And the web server is running on the library "library"

  Scenario: A work with a cite key offers its citation in three formats
    Given I open "/works/01J9Z3K0000000000000000001" in the browser
    When I open the "Manage" tab
    Then the link "BibTeX" points to an address containing "/api/v1/works/01J9Z3K0000000000000000001/citation?format=bibtex"
    And the link "RIS" points to an address containing "format=ris"
    And the link "CSL-JSON" points to an address containing "format=csl-json"

  Scenario: A work without a cite key asks for one
    Given I open "/works/01J9Z3K0000000000000000003" in the browser
    When I open the "Manage" tab
    Then the page shows the text "or to cite the work"

  Scenario: The library view exports the citations of everything it shows
    When I open "/library" in the browser
    Then the link "Export citations" points to an address containing "/api/v1/citations?format=bibtex"

  Scenario: The export follows the filter of the library view
    When I open "/library?q=venue%3ACVPR" in the browser
    Then the link "Export citations" points to an address containing "query=venue%3ACVPR"
