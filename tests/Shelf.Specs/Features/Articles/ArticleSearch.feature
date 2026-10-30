@F-ART-7 @M13
Feature: Searching articles
  As a reader
  I want to find an article by its text, its type and its site
  So that I find what I once read without remembering where

  Background:
    Given an empty working directory
    And I have run shelf with arguments "init library"
    And a book "Kubernetes Patterns" by "Bilgin Ibryam" from 2019
    And an article "Why Tests Matter" from the site "Example Blog" with the text "Executable documentation fails loudly."
    And an article "Rolling Out Safely" from the site "Ops Weekly" with the text "Canary releases limit the blast radius."

  Scenario Outline: Filters and full text find articles
    When I run shelf with arguments "search <query> --library library"
    Then the exit code is 0
    And standard output contains "<found>"
    And standard output does not contain "<absent>"

    Examples:
      | query              | found               | absent              |
      | type:article       | Why Tests Matter    | Kubernetes Patterns |
      | -type:article      | Kubernetes Patterns | Why Tests Matter    |
      | site:\"Ops Weekly\" | Rolling Out Safely | Why Tests Matter    |
      | site:ops           | Rolling Out Safely  | Why Tests Matter    |
      | blast radius       | Rolling Out Safely  | Why Tests Matter    |
      | loudly type:article | Why Tests Matter   | Rolling Out Safely  |

  Scenario: The image links of an article are not searched as text
    Given the article "Why Tests Matter" has an image saved as "images/01-3fa9c2d1.jpg"
    When I run shelf with arguments "search 3fa9c2d1 --library library"
    Then standard output does not contain "Why Tests Matter"
