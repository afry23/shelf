@F-SEM-9 @M3
Feature: Leaving out text that is not prose when embedding
  As a user searching by meaning
  I want tables of contents, prefaces, indexes and source code left out of the embeddings
  So that a passage hit is real content of the book and not a list of page numbers

  Background:
    Given an empty working directory
    And I have run shelf with arguments "init library"
    And the model catalog "multilingual-e5-small" has the files:
      | file                    | content        |
      | model.onnx              | onnx weights   |
      | sentencepiece.bpe.model | spm vocabulary |
    And the folder "offline" holds the model files
    And I have run shelf with arguments "model install offline --library library"
    And the embedder is a word-hashing fake of 16 dimensions

  Scenario Outline: A section marked by its EPUB type is not embedded
    Given a sample EPUB "library/Author/Book/book.epub" with the title "Book", the authors "Some Author" and the typed chapters:
      | title      | type   | text                                         |
      | Prologue   | <type> | <marker> belongs to a section that is no prose. |
      | Containers | chapter | The sidecar container extends the main container. |
    And the work folder "library/Author/Book" holds a sidecar for the file "book.epub"
    And I have run shelf with arguments "reindex --library library"
    When I run shelf with arguments "reindex --embeddings --library library"
    Then the exit code is 0
    And the embedder was given the text containing "sidecar container"
    And the embedder was not given the text containing "<marker>"

    Examples:
      | type            | marker       |
      | toc             | tocmarker    |
      | index           | indexmarker  |
      | preface         | prefacemarker |
      | foreword        | forewordmarker |
      | bibliography    | biblmarker   |
      | acknowledgments | thanksmarker |
      | copyright-page  | copymarker   |

  Scenario Outline: A section with a well-known title is not embedded
    Given a sample EPUB "library/Author/Book/book.epub" with the title "Book", the authors "Some Author" and the chapters:
      | title   | text                                                      |
      | <title> | titlemarker lists things that are no prose of the book.   |
      | Pods    | A pod groups containers that share a network namespace.  |
    And the work folder "library/Author/Book" holds a sidecar for the file "book.epub"
    And I have run shelf with arguments "reindex --library library"
    When I run shelf with arguments "reindex --embeddings --library library"
    Then the embedder was given the text containing "network namespace"
    And the embedder was not given the text containing "titlemarker"

    Examples:
      | title                |
      | Table of Contents    |
      | Contents             |
      | Inhaltsverzeichnis   |
      | Vorwort              |
      | Preface              |
      | Index                |
      | Stichwortverzeichnis |
      | Literaturverzeichnis |
      | Bibliography         |

  Scenario: A chapter whose title only contains a keyword is embedded
    Given a sample EPUB "library/Author/Book/book.epub" with the title "Book", the authors "Some Author" and the chapters:
      | title                | text                                              |
      | Index of Refraction  | Light bends where the medium changes its density. |
    And the work folder "library/Author/Book" holds a sidecar for the file "book.epub"
    And I have run shelf with arguments "reindex --library library"
    When I run shelf with arguments "reindex --embeddings --library library"
    Then the embedder was given the text containing "Light bends"

  Scenario: A page of a table of contents with dot leaders is not embedded
    Given a sample PDF "library/Author/Book/book.pdf" with the title "Book", the author "Some Author" and the pages:
      | text                                                                                                              |
      | Sidecars ........ 12 Adapters ........ 19 Ambassadors ........ 27 Jobs ........ 33 Operators ........ 41 Daemons ........ 58 |
      | The sidecar container extends the main container with logging and proxying.                                       |
    And the work folder "library/Author/Book" holds a sidecar for the file "book.pdf"
    And I have run shelf with arguments "reindex --library library"
    When I run shelf with arguments "reindex --embeddings --library library"
    Then the embedder was given the text containing "logging and proxying"
    And the embedder was not given the text containing "Ambassadors"

  Scenario: A page of an index with page references is not embedded
    Given a sample PDF "library/Author/Book/book.pdf" with the title "Book", the author "Some Author" and the pages:
      | text                                                                                                  |
      | adapter, 12, 45-47 ambassador, 27, 88 batch job, 33, 90-92 cluster, 4, 19, 56 daemon, 58, 61, 103 pod, 7, 14, 22 |
      | The sidecar container extends the main container with logging and proxying.                           |
    And the work folder "library/Author/Book" holds a sidecar for the file "book.pdf"
    And I have run shelf with arguments "reindex --library library"
    When I run shelf with arguments "reindex --embeddings --library library"
    Then the embedder was given the text containing "logging and proxying"
    And the embedder was not given the text containing "ambassador, 27"

  Scenario: A page of source code is not embedded
    Given a sample PDF "library/Author/Book/book.pdf" with the title "Book", the author "Some Author" and the pages:
      | text                                                                                                              |
      | static int Sum(int[] xs) { var total = 0; for (var i = 0; i < xs.Length; i++) { total += xs[i]; } return total; } |
      | The sidecar container extends the main container with logging and proxying.                                       |
    And the work folder "library/Author/Book" holds a sidecar for the file "book.pdf"
    And I have run shelf with arguments "reindex --library library"
    When I run shelf with arguments "reindex --embeddings --library library"
    Then the embedder was given the text containing "logging and proxying"
    And the embedder was not given the text containing "xs.Length"

  Scenario: Prose that mentions a function name is embedded
    Given a sample EPUB "library/Author/Book/book.epub" with the title "Book", the authors "Some Author" and the chapters:
      | title   | text                                                                                              |
      | Calling | Call the Sum function with a list of numbers and it returns the total of all numbers in the list. |
    And the work folder "library/Author/Book" holds a sidecar for the file "book.epub"
    And I have run shelf with arguments "reindex --library library"
    When I run shelf with arguments "reindex --embeddings --library library"
    Then the embedder was given the text containing "returns the total"

  Scenario: Left-out text can still be found by full-text search
    Given a sample EPUB "library/Author/Book/book.epub" with the title "Book", the authors "Some Author" and the typed chapters:
      | title    | type    | text                                         |
      | Preface  | preface | The prefacemarker explains how to read this. |
      | Pods     | chapter | A pod groups containers.                     |
    And the work folder "library/Author/Book" holds a sidecar for the file "book.epub"
    And I have run shelf with arguments "reindex --library library"
    And I have run shelf with arguments "reindex --embeddings --library library"
    When I run shelf with arguments "search prefacemarker --library library"
    Then the exit code is 0
    And standard output contains "Book"

  Scenario: Left-out chunks do not count as passages
    Given a sample EPUB "library/Author/Book/book.epub" with the title "Book", the authors "Some Author" and the typed chapters:
      | title    | type    | text                                         |
      | Contents | toc     | Pods 3 Jobs 9                                |
      | Pods     | chapter | A pod groups containers.                     |
    And the work folder "library/Author/Book" holds a sidecar for the file "book.epub"
    And I have run shelf with arguments "reindex --library library"
    When I run shelf with arguments "reindex --embeddings --library library --json"
    Then the JSON integer property "passages" of standard output is 1
