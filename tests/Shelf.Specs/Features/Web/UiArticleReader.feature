@ui @F-ART-8 @PRD-8.3 @M13
Feature: Reading an article in the web interface
  As a reader
  I want to open an article with its pictures in the browser
  So that I read it like a book, without the site it came from

  Background:
    Given an empty working directory
    And I have run shelf with arguments "init library"
    And an article "Why Tests Matter" from the site "Example Blog" with the text "Tests document intent." and 1 images
    And I have run shelf with arguments "reindex --library library"
    And the web server is running on the library "library"

  Scenario: The article opens with its text and its image
    Given I open "/library" in the browser
    When I click the "Why Tests Matter" link
    And I click the "Read" link
    Then the reader shows the text "Tests document intent."
    And the reader shows 1 images
    And every image of the reader comes from the library

  Scenario: A script or a remote image in the Markdown is not run or loaded
    Given the article "Why Tests Matter" also contains "<script>window.pwned = 1</script> ![](https://tracker.example.com/p.gif)"
    And I have run shelf with arguments "reindex --library library"
    When I open the article "Why Tests Matter" in the reader
    Then the reader shows the text "Tests document intent."
    And the reader ran no script from the article
    And the reader loaded no image from outside the library
