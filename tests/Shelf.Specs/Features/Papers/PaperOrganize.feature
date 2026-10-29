@F-PAP-10 @F-ORG-1 @M9
Feature: Folders and names of papers
  As a researcher with thousands of short papers
  I want papers sorted by year and cite key instead of by author and title
  So that the library folder stays readable and a file name is the key I cite

  Background:
    Given an empty working directory
    And I have run shelf with arguments "init library"

  Scenario: By default a paper goes to papers/<year>/<cite key> and a book stays under its author
    Given a paper "Attention Is All You Need" by "Ashish Vaswani" from 2017 with the cite key "vaswani2017attention"
    And a book "Kubernetes Patterns" by "Bilgin Ibryam" from 2019
    When I run shelf with arguments "organize --apply --library library"
    Then the exit code is 0
    And the file "library/papers/2017/vaswani2017attention/vaswani2017attention.pdf" exists
    And the file "library/papers/2017/vaswani2017attention/book.json" exists
    And the file "library/Ibryam, Bilgin/Kubernetes Patterns (2019)/Kubernetes Patterns.pdf" exists

  Scenario: The dry run shows the paper pattern and writes nothing
    Given a paper "Attention Is All You Need" by "Ashish Vaswani" from 2017 with the cite key "vaswani2017attention"
    When I run shelf with arguments "organize --library library"
    Then standard output contains "papers/2017/vaswani2017attention/vaswani2017attention.pdf"
    And the file "library/Ashish Vaswani/Attention Is All You Need/paper.pdf" exists

  Scenario: A paper without a year goes straight to papers
    Given a paper "Notes on Nothing" by "Jane Roe" without a year with the cite key "roe-notes"
    When I run shelf with arguments "organize --apply --library library"
    Then the file "library/papers/roe-notes/roe-notes.pdf" exists

  Scenario: A paper without a cite key is named after its title
    Given a paper "Mystery Result" by "Unknown Author" from 2017 without a cite key
    When I run shelf with arguments "organize --apply --library library"
    Then the file "library/papers/2017/Mystery Result/Mystery Result.pdf" exists

  Scenario: The pattern for papers can be chosen on the command line
    Given a paper "Attention Is All You Need" by "Ashish Vaswani" from 2017 with the cite key "vaswani2017attention"
    When I run shelf with arguments "organize --apply --paper-pattern '{year}/{author_sort}/{title}.{ext}' --library library"
    Then the file "library/2017/Vaswani, Ashish/Attention Is All You Need.pdf" exists

  Scenario: The pattern for papers can be chosen in the library config
    Given a paper "Attention Is All You Need" by "Ashish Vaswani" from 2017 with the cite key "vaswani2017attention"
    And a config file "library/.shelf/config.toml" with content:
      """
      [organize]
      paper_pattern = "preprints/{cite_key}.{ext}"
      """
    When I run shelf with arguments "organize --apply --library library"
    Then the file "library/preprints/vaswani2017attention.pdf" exists

  Scenario: --pattern is the pattern of books and leaves papers alone
    Given a paper "Attention Is All You Need" by "Ashish Vaswani" from 2017 with the cite key "vaswani2017attention"
    And a book "Kubernetes Patterns" by "Bilgin Ibryam" from 2019
    When I run shelf with arguments "organize --apply --pattern '{title}.{ext}' --library library"
    Then the file "library/papers/2017/vaswani2017attention/vaswani2017attention.pdf" exists
    And the file "library/Kubernetes Patterns.pdf" exists

  Scenario: An organized paper library is stable and can be undone
    Given a paper "Attention Is All You Need" by "Ashish Vaswani" from 2017 with the cite key "vaswani2017attention"
    And I have run shelf with arguments "organize --apply --library library"
    When I run shelf with arguments "organize --library library"
    Then standard output contains "Nothing to organize (1 file already in place)"
    When I run shelf with arguments "undo --library library"
    Then the file "library/Ashish Vaswani/Attention Is All You Need/paper.pdf" exists

  Scenario: An unusable pattern for papers is refused
    When I run shelf with arguments "organize --paper-pattern '{cite_key}' --library library"
    Then the exit code is 2
    And standard error contains "--paper-pattern: the last part must end with '.{ext}'"
