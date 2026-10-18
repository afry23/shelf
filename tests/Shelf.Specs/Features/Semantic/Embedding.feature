@F-SEM-1 @M3
Feature: Local embeddings
  As a user who searches by meaning
  I want shelf to turn text into vectors on my machine
  So that similar meanings can be found without any network access

  Background:
    Given the model "multilingual-e5-small" is installed

  Scenario: A vector has the dimension of the model and unit length
    When the passage "Event sourcing stores every change as an event." is embedded
    Then the vector has 384 dimensions
    And the vector has a length of 1 within 0.001

  Scenario Outline: Queries and passages get their prefixes
    When the <kind> "<text>" is embedded
    Then the vector equals the embedding of the raw text "<sent>"

    Examples:
      | kind    | text         | sent                  |
      | query   | microservice | query: microservice   |
      | passage | microservice | passage: microservice |

  Scenario: The same text always gives the same vector
    When the passage "A projection builds a read model." is embedded twice
    Then both vectors are equal

  Scenario Outline: A query is closest to the passage that means the same, across languages
    When the query "<query>" is compared with these passages:
      | passage                                                                  |
      | Message brokers decouple services by passing events between them.        |
      | A sourdough starter needs flour, water and a warm kitchen.                |
      | The match ended with a late goal in the second half.                      |
    Then the closest passage is "<closest>"

    Examples:
      | query                                              | closest                                                           |
      | Bücher über Messaging zwischen Microservices       | Message brokers decouple services by passing events between them. |
      | Wie backe ich Brot mit Sauerteig                   | A sourdough starter needs flour, water and a warm kitchen.         |
      | Wer hat das Spiel in der Nachspielzeit entschieden | The match ended with a late goal in the second half.               |

  Scenario Outline: Embedding fails clearly when the model is missing
    Given the model folder is empty
    When the <kind> "hello" is embedded
    Then embedding fails with the message "model not installed"

    Examples:
      | kind    |
      | query   |
      | passage |
