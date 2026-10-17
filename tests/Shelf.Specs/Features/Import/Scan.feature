@F-LIB-7 @M1
Feature: Incremental scan
  As a user who adds, changes and removes files in the library folders
  I want shelf scan to look only at what changed
  So that the index follows the folders without reading every book again

  Background:
    Given an empty working directory
    And I have run shelf with arguments "init library"
    And a work folder "library/Ålander, Jördis/Event Streams in Practice" with the fixture files "es.epub=derived/epub3-isbn-series.epub, es.pdf=derived/Event Streams in Practice.pdf" and the sidecar:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000001", "title": "Event Streams in Practice", "authors": ["Jördis Ålander"], "tags": ["streaming"], "files": [{ "name": "es.epub" }, { "name": "es.pdf" }] }
      """
    And I have run shelf with arguments "reindex --library library"

  Scenario: A scan of an unchanged library reads nothing
    When I run shelf with arguments "scan --library library"
    Then the exit code is 0
    And standard output contains "Scanned 2 files in 1 work: 0 new, 0 changed, 0 removed, 2 unchanged"
    And standard error is empty

  Scenario: Reindex has already read the content
    Then the table "chunks" of the index has at least 2 rows
    And the file "es.pdf" in the index has the text status "done"
    And the file "es.pdf" in the index has a hash and a fingerprint

  Scenario: A file with other content is read again
    Given the file "library/Ålander, Jördis/Event Streams in Practice/es.pdf" is replaced by the fixture "derived/scanned-page.pdf"
    When I run shelf with arguments "scan --library library"
    Then the exit code is 0
    And standard output contains "Scanned 2 files in 1 work: 0 new, 1 changed, 0 removed, 1 unchanged"
    And the file "es.pdf" in the index has the text status "none"

  Scenario: A file that was only touched is checked by its hash and not read again
    Given the file "library/Ålander, Jördis/Event Streams in Practice/es.pdf" has a new modification time
    When I run shelf with arguments "scan --library library"
    Then standard output contains "Scanned 2 files in 1 work: 0 new, 0 changed, 0 removed, 2 unchanged"
    And standard output contains "1 touched"
    And the file "es.pdf" in the index has the text status "done"

  Scenario: A deleted file leaves the index but not the sidecar
    Given I delete the file "library/Ålander, Jördis/Event Streams in Practice/es.pdf"
    When I run shelf with arguments "scan --library library"
    Then the exit code is 0
    And standard output contains "0 new, 0 changed, 1 removed, 1 unchanged"
    And the table "files" of the index has 1 rows
    And the sidecar "library/Ålander, Jördis/Event Streams in Practice/book.json" lists the files "es.epub, es.pdf"

  Scenario: A new file in a work folder joins that work
    Given the fixture "derived/epub2-isbn-cover.epub" copied as "library/Ålander, Jördis/Event Streams in Practice/extra.epub"
    When I run shelf with arguments "scan --library library"
    Then the exit code is 0
    And standard output contains "1 new"
    And the sidecar "library/Ålander, Jördis/Event Streams in Practice/book.json" lists the files "es.epub, es.pdf, extra.epub"
    And the table "files" of the index has 3 rows
    And the table "works" of the index has 1 rows

  Scenario: A new work folder is indexed
    Given a work folder "library/Weiß, Lukas/Größenordnungen" with the fixture files "g.epub=derived/epub2-isbn-cover.epub" and the sidecar:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000002", "title": "Größenordnungen verteilter Systeme", "authors": ["Lukas Weiß"], "files": [{ "name": "g.epub" }] }
      """
    When I run shelf with arguments "scan --library library"
    Then the exit code is 0
    And standard output contains "Scanned 3 files in 2 works: 1 new, 0 changed, 0 removed, 2 unchanged"
    And the table "works" of the index has 2 rows
    And the table "chunks" of the index has at least 3 rows

  Scenario: A removed work folder leaves the index
    Given I delete the folder "library/Ålander, Jördis"
    When I run shelf with arguments "scan --library library"
    Then standard output contains "Scanned 0 files in 0 works"
    And standard output contains "2 removed"
    And the table "works" of the index has 0 rows
    And the table "files" of the index has 0 rows
    And the table "chunks" of the index has 0 rows

  Scenario: A changed sidecar updates the work without reading its files again
    Given the sidecar "library/Ålander, Jördis/Event Streams in Practice/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000001", "title": "Event Streams in Practice, Revised", "authors": ["Jördis Ålander"], "tags": ["streaming", "kafka"], "files": [{ "name": "es.epub" }, { "name": "es.pdf" }] }
      """
    When I run shelf with arguments "scan --library library"
    Then the exit code is 0
    And standard output contains "1 work updated"
    And the work "01J9Z3K0000000000000000001" in the index has the title "Event Streams in Practice, Revised"
    And the work "01J9Z3K0000000000000000001" in the index has the tags "kafka, streaming"
    And the full-text index finds the work "01J9Z3K0000000000000000001" for the query "revised"
    And the table "chunks" of the index has at least 2 rows

  Scenario: A work folder that moved keeps its content
    Given I move the folder "library/Ålander, Jördis" to "library/Alander"
    When I run shelf with arguments "scan --library library"
    Then the exit code is 0
    And standard output contains "0 new, 0 changed, 0 removed, 2 unchanged"
    And the file "es.pdf" in the index has the path "Alander/Event Streams in Practice/es.pdf"
    And the table "chunks" of the index has at least 2 rows

  Scenario: A loose file outside any work folder is reported, not touched
    Given the fixture "derived/epub2-isbn-cover.epub" copied as "library/loose/loose.epub"
    When I run shelf with arguments "scan --library library"
    Then the exit code is 0
    And standard output contains "untracked loose/loose.epub"
    And standard output contains "1 untracked"
    And the file "library/loose/loose.epub" exists
    And the directory "library/loose/.shelf" does not exist

  Scenario: A file that cannot be read is reported and queued
    Given the fixture "derived/not-a-zip.epub" copied as "library/Ålander, Jördis/Event Streams in Practice/bad.epub"
    When I run shelf with arguments "scan --library library"
    Then the exit code is 1
    And standard error contains "bad.epub"
    And standard error contains "not-a-container"
    And the review queue of "library" has 1 entry
    And the file "bad.epub" in the index has the text status "failed"

  Scenario: A repaired file removes its entry from the queue
    Given the fixture "derived/not-a-zip.epub" copied as "library/Ålander, Jördis/Event Streams in Practice/bad.epub"
    And I have tried shelf with arguments "scan --library library"
    And the file "library/Ålander, Jördis/Event Streams in Practice/bad.epub" is replaced by the fixture "derived/epub2-isbn-cover.epub"
    When I run shelf with arguments "scan --library library"
    Then the exit code is 0
    And the review queue of "library" has 0 entries
    And the file "bad.epub" in the index has the text status "done"

  Scenario: The JSON output counts what happened
    When I run shelf with arguments "scan --json --library library"
    Then the exit code is 0
    And standard output is a JSON object with the integer properties "works=1, files=2, newWorks=0, updatedWorks=0, removedWorks=0, newFiles=0, changedFiles=0, touchedFiles=0, removedFiles=0, unchangedFiles=2, untracked=0, problems=0"
