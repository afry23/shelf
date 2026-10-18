@F-SEM-4 @M3
Feature: Storing and finding vectors in the index
  As a user who searches by meaning
  I want the vectors of works and passages kept in the index database
  So that the nearest ones can be found without any other database

  Background:
    Given an index for vectors of 3 dimensions and the model "test-model"

  Scenario Outline: The nearest vectors come back closest first, with their cosine distance
    Given the <backend> backend
    And these work vectors are stored:
      | id | vector     |
      | 1  | 1 0 0      |
      | 2  | 0 1 0      |
      | 3  | -1 0 0     |
      | 4  | 1 1 0      |
    When the 4 nearest work vectors to "1 0 0" are requested
    Then the hits are:
      | id | distance |
      | 1  | 0.0      |
      | 4  | 0.2929   |
      | 2  | 1.0      |
      | 3  | 2.0      |

    Examples:
      | backend    |
      | sqlite-vec |
      | plain      |

  Scenario Outline: Works and passages are kept apart
    Given the <backend> backend
    And these work vectors are stored:
      | id | vector |
      | 7  | 1 0 0  |
    And these chunk vectors are stored:
      | id | vector |
      | 7  | 0 1 0  |
      | 8  | 0 0 1  |
    When the 5 nearest chunk vectors to "0 1 0" are requested
    Then the hits are:
      | id | distance |
      | 7  | 0.0      |
      | 8  | 1.0      |
    And there are 1 work vectors and 2 chunk vectors

    Examples:
      | backend    |
      | sqlite-vec |
      | plain      |

  Scenario Outline: Storing a vector for the same id again replaces it
    Given the <backend> backend
    And these work vectors are stored:
      | id | vector |
      | 1  | 1 0 0  |
      | 2  | 0 1 0  |
    And these work vectors are stored:
      | id | vector |
      | 1  | 0 1 0  |
    When the 1 nearest work vectors to "0 1 0" are requested
    Then there are 2 work vectors and 0 chunk vectors
    And the best hit has a distance of 0.0

    Examples:
      | backend    |
      | sqlite-vec |
      | plain      |

  Scenario Outline: A deleted vector is not found any more
    Given the <backend> backend
    And these chunk vectors are stored:
      | id | vector |
      | 1  | 1 0 0  |
      | 2  | 1 1 0  |
    When the chunk vector 1 is deleted
    And the 5 nearest chunk vectors to "1 0 0" are requested
    Then the hits are:
      | id | distance |
      | 2  | 0.2929   |

    Examples:
      | backend    |
      | sqlite-vec |
      | plain      |

  Scenario Outline: Asking for more than there is returns what there is
    Given the <backend> backend
    And these work vectors are stored:
      | id | vector |
      | 1  | 1 0 0  |
    When the <k> nearest work vectors to "1 0 0" are requested
    Then there are <found> hits

    Examples:
      | backend    | k   | found |
      | sqlite-vec | 10  | 1     |
      | plain      | 10  | 1     |
      | sqlite-vec | 1   | 1     |

  Scenario Outline: An empty store has no hits
    Given the <backend> backend
    When the 3 nearest work vectors to "1 0 0" are requested
    Then there are 0 hits

    Examples:
      | backend    |
      | sqlite-vec |
      | plain      |

  Scenario Outline: A vector of the wrong dimension is rejected
    Given the <backend> backend
    When a work vector with the values "<values>" is stored
    Then storing fails with the message "<message>"

    Examples:
      | backend    | values  | message                                  |
      | sqlite-vec | 1 0     | expected 3 dimensions but got 2          |
      | plain      | 1 0 0 0 | expected 3 dimensions but got 4          |
      | plain      |         | expected 3 dimensions but got 0          |

  Scenario Outline: Vectors survive closing and opening the index
    Given the <backend> backend
    And these work vectors are stored:
      | id | vector |
      | 1  | 1 0 0  |
    When the index is closed and opened again
    And the 1 nearest work vectors to "1 0 0" are requested
    Then the backend is <backend>
    And the best hit has a distance of 0.0

    Examples:
      | backend    |
      | sqlite-vec |
      | plain      |

  Scenario: Without the sqlite-vec library the plain backend is used
    Given the sqlite-vec library cannot be loaded
    When the index is prepared
    Then the backend is plain

  Scenario: An index made with sqlite-vec cannot be searched without the library
    Given the sqlite-vec backend
    And the sqlite-vec library cannot be loaded
    When the index is opened again
    Then opening fails with the message "sqlite-vec"

  Scenario Outline: The index remembers the model and notices another one
    Given the <backend> backend
    When the index is opened again for the model "<model>"
    Then the stored model is "test-model"
    And the model is <verdict>

    Examples:
      | backend    | model      | verdict   |
      | plain      | test-model | the same  |
      | plain      | other-model | different |
      | sqlite-vec | other-model | different |

  Scenario Outline: Rebuilding the index with reindex removes the vectors
    Given the <backend> backend
    And these work vectors are stored:
      | id | vector |
      | 1  | 1 0 0  |
    When the index of the library is rebuilt with reindex
    Then the index has no vector tables

    Examples:
      | backend    |
      | sqlite-vec |
      | plain      |

  Scenario: Rebuilding an index with sqlite-vec tables works without the library
    Given the sqlite-vec backend
    And the sqlite-vec library cannot be loaded
    When the index of the library is rebuilt with reindex
    Then the index has no vector tables
