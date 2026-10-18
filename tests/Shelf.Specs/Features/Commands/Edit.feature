@PRD-7.1 @F-LIB-5 @M1
Feature: Editing the metadata of a work
  As a user of the library
  I want to correct the metadata of a work from the command line
  So that titles, authors and tags are right without touching book.json by hand

  Background:
    Given an empty working directory
    And I have run shelf with arguments "init library"
    And a sample EPUB "library/Ibryam, Bilgin/Kubernetes Patterns (2019)/kp.epub" with the title "Kubernetes Patterns", the authors "Bilgin Ibryam" and the text seed 1
    And the sidecar "library/Ibryam, Bilgin/Kubernetes Patterns (2019)/book.json" is replaced by:
      """
      { "schema": 1, "id": "01J9Z3K1000000000000000001", "title": "Kubernetes Patterns", "subtitle": "Reusable Elements", "authors": ["Bilgin Ibryam"], "language": "en", "year": 2019, "tags": ["cloud/kubernetes", "devops"], "rating": 4, "x-custom": { "keep": true }, "files": [{ "name": "kp.epub" }] }
      """
    And I have run shelf with arguments "reindex --library library"

  Scenario: A changed title is written to the sidecar and to the index, and the folder stays
    When I run shelf with arguments "edit 01J9Z3K1 --title 'Kubernetes Patterns, Second Edition' --library library"
    Then the exit code is 0
    And standard output is "Updated 01J9Z3K1000000000000000001: title"
    And the sidecar "library/Ibryam, Bilgin/Kubernetes Patterns (2019)/book.json" has the title "Kubernetes Patterns, Second Edition" and the authors "Bilgin Ibryam"
    And the work "01J9Z3K1000000000000000001" in the index has the title "Kubernetes Patterns, Second Edition"
    And the file "library/Ibryam, Bilgin/Kubernetes Patterns (2019)/kp.epub" exists

  Scenario: A changed title can be searched at once
    Given I have run shelf with arguments "edit 01J9Z3K1 --title 'Container Orchestration Handbook' --library library"
    When I run shelf with arguments "search handbook --library library"
    Then the search results are in this order: "Container Orchestration Handbook"

  Scenario Outline: Each field can be set
    Given I have run shelf with arguments "edit 01J9Z3K1 <options> --library library"
    When I run shelf with arguments "show 01J9Z3K1 --library library --json"
    Then the JSON of standard output has the <type> "<path>" equal to <expected>

    Examples:
      | options                              | type   | path         | expected                |
      | --subtitle 'Second edition'        | string | subtitle     | "Second edition"        |
      | --language de                        | string | language     | "de"                    |
      | --year 2021                          | number | year         | 2021                    |
      | --publisher Manning                  | string | publisher    | "Manning"               |
      | --series Patterns --series-index 3   | number | seriesIndex  | 3                       |
      | --series Patterns                    | string | series       | "Patterns"              |
      | --edition 2nd                        | string | edition      | "2nd"                   |
      | --description 'A new description'  | string | description  | "A new description"     |
      | --rating 5                           | number | rating       | 5                       |
      | --status read                        | string | readStatus   | "read"                  |
      | --isbn 978-0-00-000001-9             | string | isbn13       | "9780000000019"         |
      | --isbn 0-306-40615-2                 | string | isbn13       | "9780306406157"         |
      | --isbn 0-306-40615-2                 | string | isbn10       | "0306406152"            |
      | --author 'Roland Huss'             | string | authors[0]   | "Roland Huss"           |

  Scenario: Several authors can be given, and they replace the old ones
    Given I have run shelf with arguments "edit 01J9Z3K1 --author 'Bilgin Ibryam' --author 'Roland Huss' --library library"
    Then the sidecar "library/Ibryam, Bilgin/Kubernetes Patterns (2019)/book.json" has the title "Kubernetes Patterns" and the authors "Bilgin Ibryam; Roland Huss"
    And the work "01J9Z3K1000000000000000001" in the index has the normalized title "kubernetes patterns" and the author sort key "Ibryam, Bilgin"

  Scenario Outline: Tags can be replaced, added to and removed
    Given I have run shelf with arguments "edit 01J9Z3K1 <options> --library library"
    Then the work "01J9Z3K1000000000000000001" in the index has the tags "<tags>"

    Examples:
      | options                                  | tags                                    |
      | --tag architecture --tag books           | architecture, books                     |
      | --add-tag books                          | books, cloud/kubernetes, devops         |
      | --add-tag devops                         | cloud/kubernetes, devops                |
      | --remove-tag devops                      | cloud/kubernetes                        |
      | --remove-tag unknown                     | cloud/kubernetes, devops                |
      | --add-tag books --remove-tag devops      | books, cloud/kubernetes                 |
      | --tag ''                              |                                         |

  Scenario Outline: An empty value removes an optional field
    Given I have run shelf with arguments "edit 01J9Z3K1 <option> '' --library library"
    When I run shelf with arguments "show 01J9Z3K1 --library library --json"
    Then the JSON of standard output has no property "<key>"

    Examples:
      | option      | key        |
      | --subtitle  | subtitle |
      | --year      | year       |
      | --rating    | rating     |
      | --language  | language |

  Scenario: Several fields change in one call and the output names them
    When I run shelf with arguments "edit 01J9Z3K1 --year 2020 --rating 5 --status read --library library"
    Then the exit code is 0
    And standard output is "Updated 01J9Z3K1000000000000000001: year, rating, status"

  Scenario: Fields shelf does not know stay in the sidecar
    Given I have run shelf with arguments "edit 01J9Z3K1 --year 2020 --library library"
    Then the file "library/Ibryam, Bilgin/Kubernetes Patterns (2019)/book.json" contains "x-custom"

  Scenario: The ID of a work stays
    Given I have run shelf with arguments "edit 01J9Z3K1 --title Changed --library library"
    Then the file "library/Ibryam, Bilgin/Kubernetes Patterns (2019)/book.json" contains "01J9Z3K1000000000000000001"

  Scenario: The JSON form names the changes
    When I run shelf with arguments "edit 01J9Z3K1 --year 2020 --status read --library library --json"
    Then the exit code is 0
    And the JSON of standard output has the string "id" equal to "01J9Z3K1000000000000000001"
    And the JSON of standard output has the string "changed[0]" equal to "year"
    And the JSON of standard output has the string "changed[1]" equal to "status"

  Scenario Outline: Values that make no sense are usage errors and nothing is changed
    When I run shelf with arguments "edit 01J9Z3K1 <options> --library library"
    Then the exit code is 2
    And standard error starts with "error: <message>"
    And the sidecar "library/Ibryam, Bilgin/Kubernetes Patterns (2019)/book.json" has the title "Kubernetes Patterns" and the authors "Bilgin Ibryam"
    And the file "library/Ibryam, Bilgin/Kubernetes Patterns (2019)/book.json" contains "2019"

    Examples:
      | options                    | message                                         |
      | --year abc                 | --year: 'abc' is not a year                     |
      | --year 99999               | --year: '99999' is not a year                   |
      | --rating 6                 | --rating: must be between 1 and 5               |
      | --rating zero              | --rating: must be between 1 and 5               |
      | --status finished          | --status: must be unread, reading or read       |
      | --isbn 123                 | --isbn: '123' is not a valid ISBN               |
      | --isbn 978-0-00-000001-0   | --isbn: '978-0-00-000001-0' is not a valid ISBN |
      | --title ''               | --title: must not be empty                      |
      | --author ''              | --author: must not be empty                     |
      | --series-index abc         | --series-index: 'abc' is not a number           |
      | --add-tag ''             | --add-tag: must not be empty                    |

  Scenario: Without anything to change the command says so
    When I run shelf with arguments "edit 01J9Z3K1 --library library"
    Then the exit code is 2
    And standard error starts with "error: nothing to change"

  Scenario: An unknown work is an error
    When I run shelf with arguments "edit 01J9Z3K9 --year 2020 --library library"
    Then the exit code is 1
    And standard error starts with "error: no work matches '01J9Z3K9'"
