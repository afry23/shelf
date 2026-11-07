@F-ART-5 @F-ART-9 @M13
Feature: The images of an imported article stay with it
  As a reader
  I want the images that the clipper saved next to the Markdown file to move into the article folder
  So that the article reads in any Markdown editor and in the web reader without the source folder

  Background:
    Given an empty working directory
    And I have run shelf with arguments "init library"

  Scenario Outline: Linked images are copied to images/ and the links are rewritten
    Given a Markdown file "clips/post/post.md" with content:
      """
      ---
      title: "Photo Essay"
      ---
      Intro.

      <markup>
      """
    And an image "clips/post/<file>" that is a 1200 by 800 picture
    When I run shelf with arguments "import --article clips/post --library library"
    Then the exit code is 0
    And the article "Photo Essay" has 1 images in its folder
    And the article "Photo Essay" contains "![<alt>](images/01-"
    And the article "Photo Essay" does not contain "](<file>)"

    Examples:
      | markup                          | file                 | alt    |
      | ![Harbour](harbour.jpg)         | harbour.jpg          | Harbour |
      | ![Harbour](attachments/h.png)   | attachments/h.png    | Harbour |
      | ![Harbour](./img/h%20x.jpg)     | img/h x.jpg          | Harbour |
      | ![Harbour](h.jpg "A title")     | h.jpg                | Harbour |
      | ![[h.png]]                      | h.png                | h.png  |
      | ![[h.png\|Harbour]]             | h.png                | Harbour |

  Scenario: Identical images are saved once
    Given a Markdown file "clips/post/post.md" with content:
      """
      ---
      title: "Twice"
      ---
      ![One](a.jpg)

      ![Two](b.jpg)
      """
    And an image "clips/post/a.jpg" that is a 800 by 600 picture
    And the image "clips/post/b.jpg" is the same picture as "clips/post/a.jpg"
    When I run shelf with arguments "import --article clips/post --library library"
    Then the exit code is 0
    And the article "Twice" has 1 images in its folder
    And the article "Twice" links the same image twice

  Scenario Outline: An image that cannot be taken stays as it is and is reported
    Given a Markdown file "clips/post/post.md" with content:
      """
      ---
      title: "Broken"
      ---
      ![Gone](<target>)
      """
    And a file "clips/secret.txt" in the working directory
    When I run shelf with arguments "import --article clips/post --library library"
    Then the exit code is 0
    And standard error contains "<warning>"
    And the article "Broken" contains "![Gone](<target>)"
    And the article "Broken" has 0 images in its folder

    Examples:
      | target              | warning                    |
      | missing.jpg         | missing.jpg: no such file  |
      | ../secret.txt       | ../secret.txt: outside     |
      | ../../secret.txt    | ../../secret.txt: outside  |

  Scenario: An image that is a file but no picture is not copied
    Given a Markdown file "clips/post/post.md" with content:
      """
      ---
      title: "Fake"
      ---
      ![Fake](fake.jpg)
      """
    And the file "clips/post/fake.jpg" has the content "not a picture"
    When I run shelf with arguments "import --article clips/post --library library"
    Then the exit code is 0
    And standard error contains "fake.jpg: not an image"
    And the article "Fake" has 0 images in its folder

  Scenario: A folder with several Markdown files makes one article per file
    Given a Markdown file "clips/batch/one.md" with content:
      """
      ---
      title: "One"
      ---
      ![Shared](shared.jpg)
      """
    And a Markdown file "clips/batch/two.md" with content:
      """
      ---
      title: "Two"
      ---
      ![Shared](shared.jpg)
      """
    And an image "clips/batch/shared.jpg" that is a 800 by 600 picture
    When I run shelf with arguments "import --article clips/batch --library library"
    Then the exit code is 0
    And standard output contains "Imported 2 files: 2 new works"
    And the article "One" has 1 images in its folder
    And the article "Two" has 1 images in its folder

  Scenario: An imported article is found by its text and read in the web reader
    Given a Markdown file "clips/post.md" with content:
      """
      ---
      title: "Harbour Notes"
      ---
      The harbour wakes before the town does.
      """
    When I run shelf with arguments "import --article clips/post.md --library library"
    And I run shelf with arguments "search type:article harbour --library library"
    Then standard output contains "Harbour Notes"

  Scenario: The article folder survives organize and reindex
    Given a Markdown file "clips/post/post.md" with content:
      """
      ---
      title: "Kept Together"
      ---
      ![Photo](p.jpg)
      """
    And an image "clips/post/p.jpg" that is a 800 by 600 picture
    And I have run shelf with arguments "import --article clips/post --library library"
    When I run shelf with arguments "organize --apply --library library"
    And I delete the file "library/.shelf/index.db"
    And I run shelf with arguments "reindex --library library"
    Then the exit code is 0
    And the article "Kept Together" has 1 images in its folder
    And the table "works" of the index has 1 rows
