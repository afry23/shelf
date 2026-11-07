@F-GRP-10 @M7
Feature: Choosing the kind of a work made by hand
  As a user who creates a work with shelf create
  I want to say whether the file is a book, a paper or an article
  So that the work has the right kind from the start

  Background:
    Given an empty working directory
    And I have run shelf with arguments "init library"
    And a sample EPUB "incoming/draft.epub" with the title "Some Book", the authors "Jane Doe" and the text seed 7
    And a sample PDF "incoming/note.pdf" with the title "A Note on Quantities", the author "Jane Roe" and the text seed 3
    And a Markdown file "incoming/post.md" with content:
      """
      ---
      title: "Why Tests Matter"
      source: "https://blog.example.com/why-tests-matter"
      author: "Jane Doe"
      published: 2024-03-05
      ---
      Tests are **executable** documentation.
      """

  Scenario Outline: The kind that is asked for is the kind of the work
    When I run shelf with arguments "create <source> --kind <kind> --library library"
    Then the exit code is 0
    And the work "<title>" has the kind "<kind>"

    Examples:
      | source               | kind    | title                |
      | incoming/draft.epub  | book    | Some Book            |
      | incoming/note.pdf    | paper   | A Note on Quantities |
      | incoming/post.md     | article | Why Tests Matter     |

  Scenario Outline: A kind that does not fit the file is an error and nothing is created
    When I run shelf with arguments "create <source> --kind <kind> --library library"
    Then the exit code is 1
    And standard error starts with "error: <message>"
    And the table "works" of the index has 0 rows

    Examples:
      | source              | kind    | message                                   |
      | incoming/draft.epub | paper   | incoming/draft.epub: not a PDF file       |
      | incoming/draft.epub | article | incoming/draft.epub: not a Markdown file  |
      | incoming/post.md    | book    | incoming/post.md: a Markdown file is no book (use --kind article) |
      | incoming/draft.epub | thesis  | kind: 'thesis' is not one of book, paper, article |
