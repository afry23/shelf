@F-ART-2 @F-ART-3 @F-ART-4 @M13
Feature: Importing an article that a web clipper saved as Markdown
  As a reader who saves pages with a web clipper in my own browser
  I want shelf import --article to take the Markdown file or folder
  So that cookie banners, paywalls and scripts are the browser's business and never shelf's

  Background:
    Given an empty working directory
    And I have run shelf with arguments "init library"

  Scenario: A single Markdown file with front matter becomes an article
    Given a Markdown file "clips/why-tests-matter.md" with content:
      """
      ---
      title: "Why Tests Matter"
      source: "https://blog.example.com/why-tests-matter?utm_source=feed"
      author:
        - "[[Jane Doe]]"
      published: 2024-03-05
      created: 2024-04-01
      description: "Tests are documentation."
      tags:
        - "clippings"
        - "testing"
      ---
      Tests are **executable** documentation.

      ## Reasons

      - They fail loudly
      """
    When I run shelf with arguments "import --article clips/why-tests-matter.md --library library"
    Then the exit code is 0
    And standard output contains "new"
    And standard output contains "Imported 1 file: 1 new work"
    And the work "Why Tests Matter" has the kind "article"
    And the work "Why Tests Matter" has the authors "Jane Doe"
    And the work "Why Tests Matter" has the year 2024
    And the work "Why Tests Matter" has the source URL "https://blog.example.com/why-tests-matter"
    And the article "Why Tests Matter" contains "Tests are **executable** documentation."
    And the article "Why Tests Matter" contains "## Reasons"
    And the article "Why Tests Matter" does not contain "utm_source"
    And the article "Why Tests Matter" does not contain "created:"
    And the article "Why Tests Matter" has 0 images in its folder
    And the file "clips/why-tests-matter.md" exists

  Scenario Outline: The author may be written in the ways clippers write it
    Given a Markdown file "clips/post.md" with content:
      """
      ---
      title: "Authors Test"
      author: <author>
      ---
      Some text.
      """
    When I run shelf with arguments "import --article clips/post.md --library library"
    Then the exit code is 0
    And the work "Authors Test" has the authors "<authors>"

    Examples:
      | author                              | authors                    |
      | Jane Doe                            | Jane Doe                   |
      | "[[Jane Doe]]"                      | Jane Doe                   |
      | ["Jane Doe", "John Roe"]            | Jane Doe, John Roe         |
      | ["[[Jane Doe]]", "[[John Roe]]"]    | Jane Doe, John Roe         |

  Scenario: Without front matter the first heading is the title
    Given a Markdown file "clips/notes.md" with content:
      """
      # First Heading

      Text of the post.
      """
    When I run shelf with arguments "import --article clips/notes.md --library library"
    Then the exit code is 0
    And the work "First Heading" has the kind "article"
    And the article "First Heading" contains "Text of the post."

  Scenario: Without front matter and without heading the file name is the title
    Given a Markdown file "clips/a-post-name.md" with content:
      """
      Text without any heading.
      """
    When I run shelf with arguments "import --article clips/a-post-name.md --library library"
    Then the exit code is 0
    And the work "a-post-name" has the kind "article"

  Scenario: The front matter wins over the heading
    Given a Markdown file "clips/other.md" with content:
      """
      ---
      title: Front Title
      published: 2023-11-02
      ---
      # Heading

      Text
      """
    When I run shelf with arguments "import --article clips/other.md --library library"
    Then the exit code is 0
    And the work "Front Title" has the year 2023

  Scenario: The site and the source URL decide where the article is kept
    Given a Markdown file "clips/post.md" with content:
      """
      ---
      title: "Why Tests Matter"
      site: "Example Blog"
      source: "https://blog.example.com/why-tests-matter"
      ---
      Text.
      """
    When I run shelf with arguments "import --article clips/post.md --library library"
    Then the exit code is 0
    And the work "Why Tests Matter" has the site "Example Blog"
    And the article "Why Tests Matter" is found at "Example Blog/Why Tests Matter"

  Scenario Outline: The same article is not kept twice
    Given a Markdown file "clips/first.md" with content:
      """
      ---
      title: "Why Tests Matter"
      source: "https://blog.example.com/why-tests-matter"
      ---
      Text.
      """
    And I have run shelf with arguments "import --article clips/first.md --library library"
    And a Markdown file "clips/second.md" with content:
      """
      ---
      title: "Why Tests Matter (clipped again)"
      source: "<source>"
      ---
      Text.
      """
    When I run shelf with arguments "import --article clips/second.md --library library"
    Then the exit code is 0
    And standard output contains "duplicate"
    And the table "works" of the index has 1 rows

    Examples:
      | source                                                          |
      | https://blog.example.com/why-tests-matter                       |
      | https://blog.example.com/why-tests-matter#comments              |
      | https://blog.example.com/why-tests-matter?utm_campaign=x&utm_medium=y |
      | HTTPS://Blog.Example.com/why-tests-matter                       |

  Scenario: A dry run changes nothing
    Given a Markdown file "clips/post.md" with content:
      """
      # A Post

      Text.
      """
    When I run shelf with arguments "import --article clips/post.md --dry-run --library library"
    Then the exit code is 0
    And standard output contains "would create"
    And the table "works" of the index has 0 rows

  Scenario: Nothing goes online
    Given a Markdown file "clips/post.md" with content:
      """
      ---
      title: "Offline"
      source: "https://blog.example.com/offline"
      ---
      ![remote](https://cdn.example.com/photo.jpg)
      """
    When I run shelf with arguments "import --article clips/post.md --library library"
    Then the exit code is 0
    And the article "Offline" contains "![remote](https://cdn.example.com/photo.jpg)"
    And the article "Offline" has 0 images in its folder
    And no online source was queried

  Scenario: A file that is not Markdown is an error for that file
    Given a file "clips/page.html" in the working directory
    When I run shelf with arguments "import --article clips/page.html --library library"
    Then the exit code is 1
    And standard error contains "not a Markdown file"
    And the table "works" of the index has 0 rows
