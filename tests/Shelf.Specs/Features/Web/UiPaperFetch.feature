@ui @F-PAP-8 @M9
Feature: Adding a paper by DOI or arXiv ID in the browser
  As a researcher with a reference in my hand
  I want to type a DOI or an arXiv ID into the library view
  So that the paper is in my library without downloading and importing it by hand

  Background:
    Given an empty working directory
    And I have run shelf with arguments "init library"
    And the online source "arxiv" holds the paper:
      | field   | value                                     |
      | arxiv   | 1706.03762                                |
      | title   | Attention Is All You Need                 |
      | authors | Ashish Vaswani, Noam Shazeer, Niki Parmar |
      | year    | 2017                                      |
    And the online source "arxiv" has a PDF for the arXiv ID "1706.03762"
    And the web server is running on the library "library"

  Scenario: Nothing is asked online until the button is clicked
    When I open "/library" in the browser
    And I click the "Add paper" button
    Then the page shows the text "Loads the details and the open-access PDF"
    And no online source was queried

  Scenario: A paper is added from an arXiv ID
    Given I open "/library" in the browser
    When I click the "Add paper" button
    And I fill the "DOI or arXiv ID" field with "arXiv:1706.03762"
    And I click the "Fetch paper" button
    Then the page shows the text "Attention Is All You Need"
    And the file "library/Vaswani, Ashish/Attention Is All You Need (2017)/1706.03762.pdf" exists

  Scenario: Something that is not a DOI or an arXiv ID is explained
    Given I open "/library" in the browser
    When I click the "Add paper" button
    And I fill the "DOI or arXiv ID" field with "not-an-id"
    And I click the "Fetch paper" button
    Then the page shows the text "neither a DOI nor an arXiv ID"
