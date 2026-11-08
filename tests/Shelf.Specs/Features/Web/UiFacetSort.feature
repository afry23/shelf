@ui @F-WEB-9 @PRD-8.2 @M23
Feature: Sort the facet values in the filter panel
  As a person who browses the library
  I want to sort the authors, publishers, tags, languages and years of the filter panel
  So that I find a value by its name or by how many works have it

  Background:
    Given an empty working directory
    And I have run shelf with arguments "init library"
    And a sample EPUB "library/Ibryam/Kubernetes Patterns/kp.epub" with the title "Kubernetes Patterns", the authors "Bilgin Ibryam" and the chapters:
      | title | text                              |
      | One   | The sidecar container extends it. |
    And the sidecar "library/Ibryam/Kubernetes Patterns/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000001", "title": "Kubernetes Patterns", "authors": ["Bilgin Ibryam"], "language": "en", "publisher": "O'Reilly Media", "year": 2019, "tags": ["devops"], "files": [{ "name": "kp.epub" }] }
      """
    And a sample PDF "library/Mueller/Cloud Patterns/cp.pdf" with the title "Cloud Patterns", the author "Anna Müller" and the pages:
      | text                                     |
      | Serverless functions scale down to zero. |
    And the sidecar "library/Mueller/Cloud Patterns/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000002", "title": "Cloud Patterns", "authors": ["Anna Müller", "Bilgin Ibryam"], "language": "de", "publisher": "Manning", "year": 2020, "tags": ["devops", "cloud"], "files": [{ "name": "cp.pdf" }] }
      """
    And a sample PDF "library/Vaswani/Attention/attention.pdf" with the title "Attention Is All You Need", the author "Ashish Vaswani" and the pages:
      | text                                 |
      | The Transformer relies on attention. |
    And the sidecar "library/Vaswani/Attention/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000003", "title": "Attention Is All You Need", "authors": ["Ashish Vaswani"], "language": "en", "publisher": "Manning", "year": 2017, "tags": ["devops"], "files": [{ "name": "attention.pdf" }] }
      """
    And I have run shelf with arguments "reindex --library library"
    And the web server is running on the library "library"

  Scenario Outline: A facet group is sorted from its own list
    Given I open "/library" in the browser
    When I choose "<order>" in the "Sort <group>" list
    Then the first value of the facet group "<group>" is "<first>"
    And the browser address contains "facetSort"

    Examples:
      | group     | order             | first          |
      | Authors   | Name A-Z          | Anna Müller    |
      | Authors   | Name Z-A          | Bilgin Ibryam  |
      | Authors   | Count low to high | Anna Müller    |
      | Publisher | Count high to low | Manning        |
      | Publisher | Name Z-A          | O'Reilly Media |
      | Tags      | Name A-Z          | cloud          |
      | Language  | Name A-Z          | de             |
      | Year      | Name A-Z          | 2017           |

  Scenario: The order of a group is kept in the address
    Given I open "/library" in the browser
    When I choose "Name A-Z" in the "Sort Authors" list
    And I reload the page
    Then the first value of the facet group "Authors" is "Anna Müller"
    And the "Sort Authors" field has the value "name.asc"

  Scenario: Another order does not change the works that pass
    Given I open "/library" in the browser
    When I choose "Name A-Z" in the "Sort Authors" list
    Then the list "Books" has 3 items
