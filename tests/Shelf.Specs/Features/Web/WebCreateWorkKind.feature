@F-GRP-10 @M7
Feature: Choosing the kind of a work made by hand over HTTP
  As the library view of the web interface
  I want to say whether an uploaded file is a book, a paper or an article
  So that the work has the right kind from the start

  Background:
    Given an empty working directory
    And I have run shelf with arguments "init library"
    And a sample EPUB "upload/draft.epub" with the title "Some Book", the authors "Jane Doe" and the text seed 7
    And a sample PDF "upload/note.pdf" with the title "A Note on Quantities", the author "Jane Roe" and the text seed 3
    And a Markdown file "upload/post.md" with content:
      """
      ---
      title: "Why Tests Matter"
      source: "https://blog.example.com/why-tests-matter"
      author: "Jane Doe"
      published: 2024-03-05
      ---
      Tests are **executable** documentation.
      """
    And I have run shelf with arguments "reindex --library library"
    And the web server is running on the library "library"

  Scenario Outline: The kind that is asked for is the kind of the work
    When I upload the file "<file>" with "POST /api/v1/works?name=<name>&kind=<kind>"
    Then the response status is 200
    And the work "<title>" has the kind "<kind>"

    Examples:
      | file              | name       | kind    | title                |
      | upload/draft.epub | draft.epub | book    | Some Book            |
      | upload/note.pdf   | note.pdf   | paper   | A Note on Quantities |
      | upload/post.md    | post.md    | article | Why Tests Matter     |

  Scenario: Without a kind a PDF without DOI or arXiv ID is a book
    When I upload the file "upload/note.pdf" with "POST /api/v1/works?name=note.pdf"
    Then the response status is 200
    And the work "A Note on Quantities" has the kind "book"

  Scenario: The values that are given replace the front matter of an article
    When I upload the file "upload/post.md" with "POST /api/v1/works?name=post.md&kind=article&title=Real%20Title&author=John%20Roe&year=2025"
    Then the response status is 200
    And the work "Real Title" has the kind "article"
    And the work "Real Title" has the authors "John Roe"
    And the work "Real Title" has the year 2025
    And the work "Real Title" has the source URL "https://blog.example.com/why-tests-matter"

  Scenario: An article whose URL is in the library already is not made again
    Given I upload the file "upload/post.md" with "POST /api/v1/works?name=post.md&kind=article"
    When I upload the file "upload/post.md" with "POST /api/v1/works?name=post.md&kind=article"
    Then the response status is 409
    And the table "works" of the index has 1 rows

  Scenario Outline: A kind that does not fit the file is refused and nothing is created
    When I upload the file "<file>" with "POST /api/v1/works?name=<name>&kind=<kind>"
    Then the response status is 400
    And the table "works" of the index has 0 rows

    Examples:
      | file              | name       | kind    |
      | upload/draft.epub | draft.epub | paper   |
      | upload/draft.epub | draft.epub | article |
      | upload/post.md    | post.md    | book    |
      | upload/post.md    | post.md    | paper   |
      | upload/note.pdf   | note.pdf   | article |
      | upload/draft.epub | draft.epub | thesis  |
