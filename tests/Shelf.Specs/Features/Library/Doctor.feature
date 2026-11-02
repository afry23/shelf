@F-DOC-1 @F-DOC-2 @M21
Feature: A health check of the library
  As a person with a grown library
  I want one command that finds what is broken or incomplete
  So that I know what to fix and with which command

  Background:
    Given an empty working directory
    And I have run shelf with arguments "init library"
    And a sample PDF "library/Ibryam/Kubernetes Patterns/kp.pdf" with the title "Kubernetes Patterns", the author "Bilgin Ibryam" and the text seed 1
    And the sidecar "library/Ibryam/Kubernetes Patterns/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000001", "title": "Kubernetes Patterns", "authors": ["Bilgin Ibryam"], "language": "en", "year": 2019, "files": [{ "name": "kp.pdf" }] }
      """
    And a sample PDF "library/Mueller/Cloud Native/cn.pdf" with the title "Cloud Native", the author "Anna Müller" and the text seed 2
    And the sidecar "library/Mueller/Cloud Native/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000004", "title": "Cloud Native", "authors": ["Anna Müller"], "language": "en", "year": 2017, "files": [{ "name": "cn.pdf" }] }
      """
    And a sample PDF "library/Weiss/Verteilte Systeme/vs.pdf" with the title "Verteilte Systeme verstehen", the author "Lukas Weiß" and the text seed 3
    And the sidecar "library/Weiss/Verteilte Systeme/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000003", "title": "Verteilte Systeme verstehen", "authors": ["Lukas Weiß"], "language": "de", "year": 2015, "files": [{ "name": "vs.pdf" }] }
      """
    And I have run shelf with arguments "reindex --library library"

  Scenario: A healthy library has no errors and no warnings
    When I run shelf with arguments "doctor --library library"
    Then the exit code is 0
    And standard output contains "3 works"
    And standard output contains "[ok] index"
    And standard output contains "[ok] sidecars"
    And standard output contains "[ok] files"
    And standard output contains "[ok] index-sync"
    And standard output contains "[ok] metadata"
    And standard output contains "0 errors, 0 warnings"
    And standard output does not contain "embeddings"

  Scenario: Nothing is changed
    Given I take a snapshot of the folder "library"
    When I run shelf with arguments "doctor --library library"
    Then the folder "library" equals the snapshot

  Scenario: A sidecar that cannot be read is an error and names the folder
    Given the file "library/Mueller/Cloud Native/book.json" has the content "{ this is not json"
    When I run shelf with arguments "doctor --library library"
    Then the exit code is 1
    And standard output contains "[error] sidecars"
    And standard output contains "Mueller/Cloud Native"
    And standard output contains "1 error"

  Scenario: An annotations file that is not valid is an error
    Given the sidecar "library/Mueller/Cloud Native/annotations.json" is replaced by:
      """
      { "schema": 1, "annotations": [ { "id": "nope" } ] }
      """
    When I run shelf with arguments "doctor --library library"
    Then the exit code is 1
    And standard output contains "[error] sidecars"
    And standard output contains "annotations.json"

  Scenario: A file that a sidecar names but the folder no longer has is an error
    Given I delete the file "library/Ibryam/Kubernetes Patterns/kp.pdf"
    When I run shelf with arguments "doctor --library library"
    Then the exit code is 1
    And standard output contains "[error] files"
    And standard output contains "Kubernetes Patterns"
    And standard output contains "kp.pdf"

  Scenario: Missing metadata is a warning with a way to fix it
    Given the sidecar "library/Mueller/Cloud Native/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000004", "title": "Cloud Native", "authors": ["Anna Müller"], "files": [{ "name": "cn.pdf" }] }
      """
    And I have run shelf with arguments "reindex --library library"
    When I run shelf with arguments "doctor --library library"
    Then the exit code is 0
    And standard output contains "[warn] metadata"
    And standard output contains "1 work has no language"
    And standard output contains "1 work has no year"
    And standard output contains "Cloud Native"
    And standard output contains "shelf edit"
    And standard output contains "1 warning"

  Scenario: With --strict a warning fails the check
    Given the sidecar "library/Mueller/Cloud Native/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000004", "title": "Cloud Native", "authors": ["Anna Müller"], "files": [{ "name": "cn.pdf" }] }
      """
    And I have run shelf with arguments "reindex --library library"
    When I run shelf with arguments "doctor --strict --library library"
    Then the exit code is 1
    And standard output contains "[warn] metadata"

  Scenario: A work that the index does not know yet is a warning
    Given a sample PDF "library/Neu/Fresh/fr.pdf" with the title "Fresh", the author "N. Eu" and the text seed 9
    And the sidecar "library/Neu/Fresh/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000009", "title": "Fresh", "authors": ["N. Eu"], "language": "en", "year": 2024, "files": [{ "name": "fr.pdf" }] }
      """
    When I run shelf with arguments "doctor --library library"
    Then the exit code is 0
    And standard output contains "[warn] index-sync"
    And standard output contains "Fresh"
    And standard output contains "shelf scan"

  Scenario: A work whose folder is gone is a warning
    Given I delete the folder "library/Weiss"
    When I run shelf with arguments "doctor --library library"
    Then standard output contains "[warn] index-sync"
    And standard output contains "Verteilte Systeme verstehen"

  Scenario: The same file in two works is a warning
    Given a sample PDF "library/Copy/Kubernetes Again/kp.pdf" with the title "Kubernetes Patterns", the author "Bilgin Ibryam" and the text seed 1
    And the sidecar "library/Copy/Kubernetes Again/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000008", "title": "Kubernetes Again", "authors": ["Bilgin Ibryam"], "language": "en", "year": 2019, "files": [{ "name": "kp.pdf" }] }
      """
    And I have run shelf with arguments "reindex --library library"
    When I run shelf with arguments "doctor --library library"
    Then standard output contains "[warn] duplicates"
    And standard output contains "shelf dupes"

  Scenario: A saved search that is not valid any more is a warning
    Given the file "library/.shelf/searches.json" has the content '{ "schema": 1, "searches": [ { "name": "old", "query": "status:nothing" } ] }'
    When I run shelf with arguments "doctor --library library"
    Then the exit code is 0
    And standard output contains "[warn] searches"
    And standard output contains "old"

  Scenario: A file that cannot be read is a warning
    Given the file "library/Broken/Bad/bad.pdf" has the content "this is not a pdf"
    And the sidecar "library/Broken/Bad/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000007", "title": "Bad", "authors": ["A. Uthor"], "language": "en", "year": 2000, "files": [{ "name": "bad.pdf" }] }
      """
    And I run shelf with arguments "reindex --library library"
    When I run shelf with arguments "doctor --library library"
    Then standard output contains "[warn] text"
    And standard output contains "bad.pdf"

  Scenario: Without an index the other checks still run
    Given I delete the file "library/.shelf/index.db"
    When I run shelf with arguments "doctor --library library"
    Then the exit code is 1
    And standard output contains "[error] index"
    And standard output contains "shelf reindex"
    And standard output contains "[ok] sidecars"

  Scenario: The report is available as JSON
    Given the sidecar "library/Mueller/Cloud Native/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K0000000000000000004", "title": "Cloud Native", "authors": ["Anna Müller"], "files": [{ "name": "cn.pdf" }] }
      """
    And I have run shelf with arguments "reindex --library library"
    When I run shelf with arguments "doctor --json --library library"
    Then the exit code is 0
    And the JSON integer property "works" of standard output is 3
    And the JSON integer property "errors" of standard output is 0
    And the JSON integer property "warnings" of standard output is 1
    And standard output contains '"id":"metadata"'
    And standard output contains '"status":"warning"'
    And standard output contains '"title":"Cloud Native"'
    And standard output contains '"hint"'

  Scenario: Every check is named in the report
    When I run shelf with arguments "doctor --json --library library"
    Then standard output contains '"id":"index"'
    And standard output contains '"id":"sidecars"'
    And standard output contains '"id":"files"'
    And standard output contains '"id":"index-sync"'
    And standard output contains '"id":"metadata"'
    And standard output contains '"id":"covers"'
    And standard output contains '"id":"cite-keys"'
    And standard output contains '"id":"text"'
    And standard output contains '"id":"review"'
    And standard output contains '"id":"duplicates"'
    And standard output contains '"id":"searches"'
