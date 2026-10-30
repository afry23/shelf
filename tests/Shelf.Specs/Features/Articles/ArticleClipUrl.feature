@F-ART-2 @F-ART-6 @M13
Feature: Clipping a public article by its URL
  As a reader
  I want shelf clip to load a public page and keep the article
  So that a blog post is in my library with one command

  Background:
    Given an empty working directory
    And I have run shelf with arguments "init library"
    And the online source "web" serves the article at "https://blog.example.com/why-tests-matter" titled "Why Tests Matter" by "Jane Doe" with one image

  Scenario: A public page becomes an article with its image
    When I run shelf with arguments "clip https://blog.example.com/why-tests-matter --library library"
    Then the exit code is 0
    And standard output contains "Clipped Why Tests Matter"
    And the work "Why Tests Matter" has the kind "article"
    And the work "Why Tests Matter" has the source URL "https://blog.example.com/why-tests-matter"
    And the article "Why Tests Matter" has 1 images in its folder
    And the online source "web" was queried

  Scenario Outline: The same article is not kept twice
    Given I have run shelf with arguments "clip https://blog.example.com/why-tests-matter --library library"
    And the online source "web" serves the same article also at "<other>"
    When I run shelf with arguments "clip <other> --library library"
    Then the exit code is 0
    And standard output contains "Already in the library: Why Tests Matter"
    And the table "works" of the index has 1 rows

    Examples:
      | other                                                                       |
      | https://blog.example.com/why-tests-matter                                   |
      | https://blog.example.com/why-tests-matter#comments                          |
      | https://blog.example.com/why-tests-matter?utm_source=newsletter&utm_medium=email |

  Scenario: A page that cannot be loaded creates nothing
    Given the online source "web" fails with "503"
    When I run shelf with arguments "clip https://blog.example.com/other --library library"
    Then the exit code is 1
    And standard error contains "503"
    And the table "works" of the index has 0 rows

  Scenario Outline: Only commands that ask for it go online
    Given I have run shelf with arguments "clip https://blog.example.com/why-tests-matter --library library"
    And the online questions are forgotten
    When I run shelf with arguments "<command> --library library"
    Then no online source was queried

    Examples:
      | command |
      | scan    |
      | reindex |
      | search  |
