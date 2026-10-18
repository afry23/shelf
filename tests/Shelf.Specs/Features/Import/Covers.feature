@F-IMP-5 @M4
Feature: Cover images
  As a user who looks at the library as a grid
  I want every work to have a cover when its files can give one
  So that I can find a book by its picture

  Background:
    Given an empty working directory
    And I have run shelf with arguments "init library"

  # ---- EPUB ----

  Scenario: The cover of an EPUB is stored as it was embedded
    Given the folder "incoming" contains the fixtures "derived/epub2-isbn-cover.epub"
    When I run shelf with arguments "import incoming --library library"
    Then the exit code is 0
    And the work folder of "Größenordnungen verteilter Systeme" has the cover "cover.png"
    And the cover "cover.png" of that work is byte for byte the image "OEBPS/images/cover.png" of the EPUB "derived/epub2-isbn-cover.epub"
    And the cover "cover.png" of that work is an image of 64 x 96 pixels

  Scenario: The sidecar names the cover and the index knows it
    Given the folder "incoming" contains the fixtures "derived/epub2-isbn-cover.epub"
    And I have run shelf with arguments "import incoming --library library"
    And I have run shelf with arguments "reindex --library library"
    Then the work folder of "Größenordnungen" has the cover "cover.png"
    And the index lists a cover for "Größenordnungen"

  Scenario: An EPUB without a cover gives no cover and no failure
    Given the folder "incoming" contains the fixtures "derived/Jane Doe - Quiet Systems (2004).epub"
    When I run shelf with arguments "import incoming --library library"
    Then the exit code is 0
    And the work "Quiet Systems" has no cover
    And standard error does not contain "cover"

  # ---- PDF ----

  Scenario Outline: A PDF gets a JPEG of its first page, 600 pixels on the longest edge
    Given the folder "incoming" contains the fixtures "<fixture>"
    When I run shelf with arguments "import incoming --library library"
    Then the exit code is 0
    And the work folder of "<title>" has the cover "cover.jpg"
    And the cover "cover.jpg" of that work is a JPEG
    And the cover "cover.jpg" of that work is an image of <width> x <height> pixels
    And the cover "cover.jpg" of that work is not blank

    Examples:
      | fixture                                 | title                  | width | height |
      | derived/Event Streams in Practice.pdf   | Event Streams          | 463   | 600    |
      | derived/scanned-page.pdf                | scanned                | 463   | 600    |
      | upstream/pdf/arxiv-2403.02174v1.pdf     | arXiv                  | 423   | 600    |

  Scenario: A PDF without metadata still gets a cover
    Given the folder "incoming" contains the fixtures "derived/Jane Doe - Quiet Systems (2004).pdf"
    When I run shelf with arguments "import incoming --library library"
    Then the work "Quiet Systems" has a cover

  # ---- several files ----

  Scenario: The cover of an EPUB is preferred to a rendering of a PDF
    Given a work folder "library/Weiss/Verteilte Systeme" with the fixture files "Event Streams in Practice.pdf=derived/Event Streams in Practice.pdf, epub2-isbn-cover.epub=derived/epub2-isbn-cover.epub" and the sidecar:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000001", "title": "Verteilte Systeme", "authors": ["Lukas Weiß"], "files": [{ "name": "Event Streams in Practice.pdf" }, { "name": "epub2-isbn-cover.epub" }] }
      """
    When I run shelf with arguments "reindex --library library"
    Then the file "library/Weiss/Verteilte Systeme/cover.png" exists
    And the file "library/Weiss/Verteilte Systeme/cover.jpg" does not exist

  Scenario: A work with a cover keeps it when a file is added
    Given the folder "incoming" contains the fixtures "derived/epub2-isbn-cover.epub"
    And I have run shelf with arguments "import incoming --library library"
    And the fixture "derived/Event Streams in Practice.pdf" is added to the work folder of "Größenordnungen"
    When I run shelf with arguments "scan --library library"
    Then the work folder of "Größenordnungen" has the cover "cover.png"
    And the work folder of "Größenordnungen" has no file "cover.jpg"

  # ---- the rule for scans ----

  Scenario: A file that is read by a scan gets a cover for its work
    Given a work folder "library/Alander/Event Streams" with the fixture files "Event Streams in Practice.pdf=derived/Event Streams in Practice.pdf" and the sidecar:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000002", "title": "Event Streams", "authors": ["Jördis Ålander"], "files": [{ "name": "Event Streams in Practice.pdf" }] }
      """
    When I run shelf with arguments "scan --library library"
    Then the file "library/Alander/Event Streams/cover.jpg" exists

  Scenario: A scan that reads no file makes no cover, shelf covers does
    Given a work folder "library/Alander/Event Streams" with the fixture files "Event Streams in Practice.pdf=derived/Event Streams in Practice.pdf" and the sidecar:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000002", "title": "Event Streams", "authors": ["Jördis Ålander"], "files": [{ "name": "Event Streams in Practice.pdf" }] }
      """
    And I have run shelf with arguments "reindex --library library"
    And I delete the file "library/Alander/Event Streams/cover.jpg"
    And the sidecar of "Event Streams" has no cover entry
    When I run shelf with arguments "scan --library library"
    Then the work folder of "Event Streams" has no cover
    When I run shelf with arguments "covers --library library"
    Then the exit code is 0
    And the work folder of "Event Streams" has the cover "cover.jpg"

  # ---- shelf covers ----

  Scenario: shelf covers fills the works that lack a cover and says what it did
    Given the folder "incoming" contains the fixtures "derived/epub2-isbn-cover.epub, derived/Jane Doe - Quiet Systems (2004).epub, derived/Event Streams in Practice.pdf"
    And I have run shelf with arguments "import incoming --library library"
    And the cover of "Größenordnungen" is removed
    And the cover of "Event Streams" is removed
    When I run shelf with arguments "covers --library library"
    Then the exit code is 0
    And standard output contains "Created 2 covers"
    And standard output contains "1 work has none"
    And the work folder of "Größenordnungen" has the cover "cover.png"
    And the work folder of "Event Streams" has the cover "cover.jpg"

  Scenario: shelf covers has nothing to do when every work that can have a cover has one
    Given the folder "incoming" contains the fixtures "derived/epub2-isbn-cover.epub, derived/Event Streams in Practice.pdf"
    And I have run shelf with arguments "import incoming --library library"
    When I run shelf with arguments "covers --library library"
    Then the exit code is 0
    And standard output contains "Created 0 covers"

  Scenario: shelf covers as JSON
    Given the folder "incoming" contains the fixtures "derived/epub2-isbn-cover.epub, derived/Jane Doe - Quiet Systems (2004).epub"
    And I have run shelf with arguments "import incoming --library library"
    When I run shelf with arguments "covers --json --library library"
    Then standard output is a JSON object with the integer properties "created=0, existing=1, without=1"

  Scenario: shelf covers adopts a cover file that the sidecar does not name
    Given the folder "incoming" contains the fixtures "derived/epub2-isbn-cover.epub"
    And I have run shelf with arguments "import incoming --library library"
    And the sidecar of "Größenordnungen" has no cover entry
    When I run shelf with arguments "covers --library library"
    Then standard output contains "Created 0 covers"
    And the work folder of "Größenordnungen" has the cover "cover.png"

  Scenario: shelf covers reports a file that cannot be rendered and goes on
    Given a work folder "library/Broken/Broken" with the files "broken.pdf" and the sidecar:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000003", "title": "Broken", "authors": ["Anna Bruch"], "files": [{ "name": "broken.pdf" }] }
      """
    And I have tried shelf with arguments "reindex --library library"
    When I run shelf with arguments "covers --library library"
    Then the exit code is 0
    And standard error contains "broken.pdf"
    And standard output contains "1 work has none"

  # ---- the cover goes along ----

  Scenario: The cover moves with the work when it is organized
    Given the folder "incoming" contains the fixtures "derived/epub2-isbn-cover.epub"
    And I have run shelf with arguments "import incoming --library library"
    When I run shelf with arguments "organize --apply --pattern '{title}/{title}.{ext}' --library library"
    Then the file "library/Größenordnungen verteilter Systeme/cover.png" exists

  Scenario: A merge keeps the cover of the target and sends the cover of the source to the trash
    Given a work folder "library/Weiss/Verteilte Systeme" with the fixture files "epub2-isbn-cover.epub=derived/epub2-isbn-cover.epub" and the sidecar:
      """
      { "schema": 1, "id": "01J9Z3K1000000000000000001", "title": "Verteilte Systeme", "authors": ["Lukas Weiß"], "files": [{ "name": "epub2-isbn-cover.epub" }] }
      """
    And a work folder "library/Alander/Event Streams" with the fixture files "Event Streams in Practice.pdf=derived/Event Streams in Practice.pdf" and the sidecar:
      """
      { "schema": 1, "id": "01J9Z3K2000000000000000002", "title": "Event Streams", "authors": ["Jördis Ålander"], "files": [{ "name": "Event Streams in Practice.pdf" }] }
      """
    And I have run shelf with arguments "reindex --library library"
    When I run shelf with arguments "merge 01J9Z3K1 01J9Z3K2 --library library"
    Then the exit code is 0
    And the work folder of "Verteilte Systeme" has the cover "cover.png"
    And the work folder of "Verteilte Systeme" has no file "cover.jpg"
    And the directory "library/Alander/Event Streams" does not exist
    And the directory "library/.shelf/trash" exists

  Scenario: A merge gives the cover of the source to a target that has none
    Given a work folder "library/Weiss/Verteilte Systeme" with the fixture files "epub2-isbn-cover.epub=derived/epub2-isbn-cover.epub" and the sidecar:
      """
      { "schema": 1, "id": "01J9Z3K1000000000000000001", "title": "Verteilte Systeme", "authors": ["Lukas Weiß"], "files": [{ "name": "epub2-isbn-cover.epub" }] }
      """
    And a work folder "library/Alander/Event Streams" with the fixture files "Event Streams in Practice.pdf=derived/Event Streams in Practice.pdf" and the sidecar:
      """
      { "schema": 1, "id": "01J9Z3K2000000000000000002", "title": "Event Streams", "authors": ["Jördis Ålander"], "files": [{ "name": "Event Streams in Practice.pdf" }] }
      """
    And I have run shelf with arguments "reindex --library library"
    And the cover of "Event Streams" is removed
    And I have run shelf with arguments "scan --library library"
    When I run shelf with arguments "merge 01J9Z3K2 01J9Z3K1 --library library"
    Then the exit code is 0
    And the work folder of "Event Streams" has the cover "cover.png"
    And the directory "library/Weiss/Verteilte Systeme" does not exist

  Scenario: A work that is split off gets its own cover
    Given a work folder "library/Weiss/Verteilte Systeme" with the fixture files "epub2-isbn-cover.epub=derived/epub2-isbn-cover.epub, Event Streams in Practice.pdf=derived/Event Streams in Practice.pdf" and the sidecar:
      """
      { "schema": 1, "id": "01J9Z3K1000000000000000001", "title": "Verteilte Systeme", "authors": ["Lukas Weiß"], "files": [{ "name": "epub2-isbn-cover.epub" }, { "name": "Event Streams in Practice.pdf" }] }
      """
    And I have run shelf with arguments "reindex --library library"
    When I run shelf with arguments "split 01J9Z3K1 'Event Streams in Practice.pdf' --library library"
    Then the exit code is 0
    And the work folder of "Event Streams in Practice" has the cover "cover.jpg"
    And the work folder of "Verteilte Systeme" has the cover "cover.png"
