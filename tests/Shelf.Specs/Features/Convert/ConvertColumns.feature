# Runs by hand with the real converter of convert.command (marker + pandoc, ADR 0022); it needs the tools and
# several GB of models, so the automatic suite skips it.
@F-CNV-1 @F-CNV-2 @M24 @ignore
Feature: Reading order of converted PDFs with several columns
  As a person who reads scientific texts on a Kindle
  I want the EPUB of a two-column PDF to follow the reading order
  So that the text is not interleaved line by line

  Scenario Outline: The reading order of the converted text follows the layout
    Given an empty working directory
    And I have run shelf with arguments "init library"
    And the fixture "<fixture>" is the only file of a work with the ID "01J9Z3K0000000000000000004"
    And the real converter of the configuration is installed
    And I have run shelf with arguments "convert 01J9Z3K0000000000000000004 --library library"
    When I read the text of the new EPUB
    Then the phrase "<first>" comes before the phrase "<second>"

    Examples:
      | fixture                 | first                  | second                 |
      | derived/one-column.pdf  | Chapter 1              | Chapter 2              |
      | derived/two-columns.pdf | end of the left column | start of the right one |
      | derived/footnotes.pdf   | Main text              | Footnote 1             |
