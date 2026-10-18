@F-TAG-3 @M2
Feature: Tagging many works at once with a search
  As a user with many books on one topic
  I want to tag everything a search finds
  So that I do not have to tag the books one by one

  Background:
    Given an empty working directory
    And I have run shelf with arguments "init library"
    And a sample EPUB "library/Evans, Eric/Domain-Driven Design (2003)/ddd.epub" with the title "Domain-Driven Design", the authors "Eric Evans" and the text seed 1
    And the sidecar "library/Evans, Eric/Domain-Driven Design (2003)/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K1000000000000000001", "title": "Domain-Driven Design", "authors": ["Eric Evans"], "year": 2003, "tags": ["architektur/ddd"], "files": [{ "name": "ddd.epub" }] }
      """
    And a sample EPUB "library/Davis, Cornelia/Cloud Native Patterns (2019)/cnp.epub" with the title "Cloud Native Patterns", the authors "Cornelia Davis" and the text seed 2
    And the sidecar "library/Davis, Cornelia/Cloud Native Patterns (2019)/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K2000000000000000002", "title": "Cloud Native Patterns", "authors": ["Cornelia Davis"], "year": 2019, "tags": ["cloud/azure", "devops"], "files": [{ "name": "cnp.epub" }] }
      """
    And a sample EPUB "library/Ibryam, Bilgin/Kubernetes Patterns (2019)/kp.epub" with the title "Kubernetes Patterns", the authors "Bilgin Ibryam" and the text seed 3
    And the sidecar "library/Ibryam, Bilgin/Kubernetes Patterns (2019)/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K3000000000000000003", "title": "Kubernetes Patterns", "authors": ["Bilgin Ibryam"], "year": 2019, "tags": ["devops"], "files": [{ "name": "kp.epub" }] }
      """
    And I have run shelf with arguments "reindex --library library"

  Scenario: Every work that the search finds gets the tag
    When I run shelf with arguments "tag add reading --query 'title:patterns' --library library"
    Then the exit code is 0
    And standard output is "Added 'reading' to 2 works"
    And the work "01J9Z3K2000000000000000002" in the index has the tags "cloud/azure, devops, reading"
    And the work "01J9Z3K3000000000000000003" in the index has the tags "devops, reading"
    And the work "01J9Z3K1000000000000000001" in the index has the tags "architektur/ddd"

  Scenario: The search language of shelf search is understood
    When I run shelf with arguments "tag add platform --query 'tag:devops -title:kubernetes' --library library"
    Then standard output is "Added 'platform' to 1 work"
    And the work "01J9Z3K2000000000000000002" in the index has the tags "cloud/azure, devops, platform"

  Scenario: Works named on the command line and the search are combined
    When I run shelf with arguments "tag add reading 01J9Z3K1 --query 'tag:devops' --library library"
    Then standard output is "Added 'reading' to 3 works"

  Scenario: A tag can be removed from the works of a search
    When I run shelf with arguments "tag rm devops --query 'title:kubernetes' --library library"
    Then standard output is "Removed 'devops' from 1 work"
    And the work "01J9Z3K3000000000000000003" in the index has the tags ""
    And the work "01J9Z3K2000000000000000002" in the index has the tags "cloud/azure, devops"

  Scenario: A search that finds nothing is an error and changes nothing
    When I run shelf with arguments "tag add reading --query 'title:nothingfound' --library library"
    Then the exit code is 1
    And standard error contains "error: no work matches the query"
    And the file "library/Evans, Eric/Domain-Driven Design (2003)/book.json" does not contain "reading"

  Scenario: An invalid search is a usage error
    When I run shelf with arguments "tag add reading --query 'year:abc' --library library"
    Then the exit code is 2
    And standard error contains "error:"

  Scenario: The result as JSON
    When I run shelf with arguments "tag add reading --query 'title:patterns' --library library --json"
    Then the exit code is 0
    And standard output is a JSON object with the integer properties "works=2, changed=2"

  Scenario: The change can be undone
    Given I have run shelf with arguments "tag add reading --query 'title:patterns' --library library"
    When I run shelf with arguments "undo --library library"
    Then the exit code is 0
    And standard output contains "Reverted the last tag add run"
    And the work "01J9Z3K2000000000000000002" in the index has the tags "cloud/azure, devops"
    And the work "01J9Z3K3000000000000000003" in the index has the tags "devops"
    And the file "library/Ibryam, Bilgin/Kubernetes Patterns (2019)/book.json" does not contain "reading"
