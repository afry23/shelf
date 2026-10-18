@F-IMP-7 @M1
Feature: The review queue
  As a user importing files that shelf is unsure about
  I want every uncertain case to be written down in .shelf/review.json
  So that I can look at them later and nothing is decided silently

  Background:
    Given an empty working directory
    And I have run shelf with arguments "init library"

  Scenario: A new library has an empty queue
    When shelf reads the review queue of "library"
    Then the review queue has 0 entries

  Scenario Outline: Entries of every kind are stored with their details
    Given the review queue of "library" gets an entry of the kind "<kind>" about "<file>" with the message "<message>"
    When shelf reads the review queue of "library"
    Then the review queue has 1 entry
    And entry 1 has the kind "<kind>", the file "<file>" and the message "<message>"
    And the file "library/.shelf/review.json" contains '"kind": "<kind>"'

    Examples:
      | kind            | file                  | message                                      |
      | grouping        | Dune/dune.epub        | May be the same work as 01J9Z3K0000000000000000001 |
      | extraction-error | broken/cut.pdf       | bad-pdf: the file has no pages               |
      | weak-metadata   | misc/Jane Doe - Quiet Systems (2004).pdf | Title and authors come from the file name |

  Scenario: A grouping entry lists the candidates with confidence and signals
    Given the review queue of "library" gets a grouping entry with the candidate "01J9Z3K0000000000000000001" at 0.6 because "title and author match, year differs"
    When shelf reads the review queue of "library"
    Then entry 1 has the candidate "01J9Z3K0000000000000000001" at 0.6 because "title and author match, year differs"

  Scenario: Entries keep their order and can be removed by ID
    Given the review queue of "library" gets an entry of the kind "weak-metadata" about "a.pdf" with the message "first"
    And the review queue of "library" gets an entry of the kind "weak-metadata" about "b.pdf" with the message "second"
    And the review queue of "library" gets an entry of the kind "weak-metadata" about "c.pdf" with the message "third"
    When shelf removes entry 2 from the review queue of "library"
    And shelf reads the review queue of "library"
    Then the review queue has 2 entries
    And entry 1 has the message "first"
    And entry 2 has the message "third"

  Scenario: Removing an entry that does not exist is an error
    When shelf removes the review entry "01J9Z3K0000000000000000009" from "library"
    Then the review queue operation fails with an error containing "no review entry 01J9Z3K0000000000000000009"

  Scenario: Entries survive a reindex and are mirrored into the index
    Given the review queue of "library" gets an entry of the kind "weak-metadata" about "a.pdf" with the message "first"
    And the review queue of "library" gets an entry of the kind "extraction-error" about "b.pdf" with the message "second"
    When I run shelf with arguments "reindex --library library"
    Then the exit code is 0
    And the table "review_queue" of the index has 2 rows
    And the review queue of "library" still has 2 entries

  Scenario Outline: A damaged queue file is reported and never overwritten
    Given the file "library/.shelf/review.json" has the content '<content>'
    When shelf reads the review queue of "library"
    Then the review queue operation fails with an error containing "<message>"

    Examples:
      | content                                                           | message                                  |
      | not json                                                          | review.json: not valid JSON              |
      | {"schema": 2, "entries": []}                                      | schema 2 is newer than this version      |
      | {"schema": 1}                                                     | entries: missing                         |
      | {"schema": 1, "entries": [{"id": "x", "kind": "grouping"}]}       | entries[1].id: not a valid ULID          |
      | {"schema": 1, "entries": [{"id": "01J9Z3K0000000000000000001", "kind": "other", "createdAt": "2026-10-17T10:00:00Z", "message": "m"}]} | entries[1].kind: must be one of grouping, extraction-error, weak-metadata |

  Scenario: A reindex with a damaged queue still indexes the works and says so
    Given the file "library/.shelf/review.json" has the content 'not json'
    When I run shelf with arguments "reindex --library library"
    Then the exit code is 1
    And standard error contains "review.json: not valid JSON"
    And the table "review_queue" of the index has 0 rows
