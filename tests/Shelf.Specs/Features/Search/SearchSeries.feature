@F-WEB-7 @PRD-6.6 @M12
Feature: Searching by series
  As a person who reads a book series
  I want to filter by series
  So that I see all the books of a series, whatever the case it was written in

  Background:
    Given an empty working directory
    And I have run shelf with arguments "init library"
    And a sample EPUB "library/Ibryam/Kubernetes Patterns/kp.epub" with the title "Kubernetes Patterns", the authors "Bilgin Ibryam" and the chapters:
      | title | text                                  |
      | One   | The sidecar container extends it.     |
    And the sidecar "library/Ibryam/Kubernetes Patterns/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000001", "title": "Kubernetes Patterns", "authors": ["Bilgin Ibryam"], "language": "en", "year": 2019, "series": "Reusable Elements", "seriesIndex": 1, "files": [{ "name": "kp.epub" }] }
      """
    And a sample PDF "library/Mueller/Cloud Patterns/cp.pdf" with the title "Cloud Patterns", the author "Anna Müller" and the pages:
      | text                                     |
      | Serverless functions scale down to zero. |
    And the sidecar "library/Mueller/Cloud Patterns/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000002", "title": "Cloud Patterns", "authors": ["Anna Müller"], "language": "en", "year": 2020, "series": "reusable elements", "seriesIndex": 2, "files": [{ "name": "cp.pdf" }] }
      """
    And a sample PDF "library/Vaswani/Attention/attention.pdf" with the title "Attention Is All You Need", the author "Ashish Vaswani" and the pages:
      | text                                 |
      | The Transformer relies on attention. |
    And the sidecar "library/Vaswani/Attention/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000003", "kind": "paper", "title": "Attention Is All You Need", "authors": ["Ashish Vaswani"], "language": "en", "year": 2017, "series": "Neural Papers", "files": [{ "name": "attention.pdf" }] }
      """
    And a sample PDF "library/Body/Loose/loose.pdf" with the title "Loose Notes", the author "N. Body" and the pages:
      | text        |
      | Some notes. |
    And the sidecar "library/Body/Loose/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000004", "title": "Loose Notes", "authors": ["N. Body"], "language": "en", "files": [{ "name": "loose.pdf" }] }
      """
    And I have run shelf with arguments "reindex --library library"

  Scenario Outline: The series filter takes the whole name of a series, whatever its case
    When I run shelf with arguments '<arguments>'
    Then the exit code is 0
    And standard output contains "<found>"
    And standard output does not contain "<absent>"

    Examples:
      | arguments | found | absent |
      | search "series:\"Reusable Elements\"" --library library | Kubernetes Patterns | Loose Notes |
      | search "series:\"REUSABLE ELEMENTS\"" --library library | Cloud Patterns | Attention Is All You Need |
      | search "series:\"Neural Papers\"" --library library | Attention Is All You Need | Kubernetes Patterns |
      | search "series:\"Neural Papers\" type:paper" --library library | Attention Is All You Need | Cloud Patterns |
      | search "-series:\"Reusable Elements\"" --library library | Loose Notes | Kubernetes Patterns |
      | search "-series:\"Reusable Elements\"" --library library | Attention Is All You Need | Cloud Patterns |

  Scenario: A part of a series name is not the series
    When I run shelf with arguments "search series:Reusable --library library"
    Then the exit code is 0
    And standard output does not contain "Kubernetes Patterns"

  Scenario: A series filter without a value is refused
    When I run shelf with arguments "search 'series:' --library library"
    Then the exit code is 2
    And standard error contains "series: needs a value"
