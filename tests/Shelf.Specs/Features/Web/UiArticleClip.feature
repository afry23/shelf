@ui @F-ART-2 @M13
Feature: Adding an article by its address in the browser
  As a reader
  I want to type the address of a page into the library view
  So that the article is in my library without saving, converting and importing it by hand

  Background:
    Given an empty working directory
    And I have run shelf with arguments "init library"
    And the online source "web" serves the article at "https://blog.example.com/why-tests-matter" titled "Why Tests Matter" by "Jane Doe" with one image
    And the web server is running on the library "library"

  Scenario: Nothing is asked online until the button is clicked
    When I open "/library" in the browser
    And I click the "Add article" button
    Then the page shows the text "keeps its article as Markdown"
    And no online source was queried

  Scenario: An article is added from its address
    Given I open "/library" in the browser
    When I click the "Add article" button
    And I fill the "Address of the page" field with "https://blog.example.com/why-tests-matter"
    And I click the "Clip article" button
    Then the page shows the text "Why Tests Matter"
    And the file "library/Example Blog/Why Tests Matter/article.md" exists
    And the file "library/Example Blog/Why Tests Matter/book.json" exists

  Scenario: A page that cannot be loaded is explained
    Given the online source "web" fails with "503"
    And I open "/library" in the browser
    When I click the "Add article" button
    And I fill the "Address of the page" field with "https://blog.example.com/other"
    And I click the "Clip article" button
    Then an alert says "503"
