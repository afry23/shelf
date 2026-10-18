@F-SEM-1 @M3
Feature: Tokenizing text for the embedding model
  As a user who searches by meaning
  I want shelf to cut text into the same tokens as the model was trained with
  So that German queries and English books land near each other in the vector space

  Background:
    Given the model files of "multilingual-e5-small" are available

  Scenario Outline: Token IDs match the reference tokenizer
    When the text <text> is tokenized
    Then the token IDs are "<ids>"

    Examples:
      | text                                                      | ids                                                                    |
      | "query: Bücher über Messaging zwischen Microservices"     | 0 41 1294 12 87128 1659 48205 66398 14568 37992 22584 7 2             |
      | "passage: Event sourcing stores every change as an event." | 0 46692 12 36943 6 176595 4343 7 11907 15549 237 142 19732 5 2         |
      | "query: kubernetes"                                       | 0 41 1294 12 228 46690 30621 2                                         |
      | "passage: Größe, Straße und Übermaß"                      | 0 46692 12 66461 4 90515 165 16089 162533 2                            |
      | "passage: 日本語のテキスト"                                | 0 46692 12 6 98449 154 193508 2                                        |
      | "query: Don't panic! 42 ways (and 3.14)"                  | 0 41 1294 12 7650 25 18 249 6402 38 4828 48322 15 2940 1031 65283 2   |
      | "query: "                                                 | 0 41 1294 12 2                                                         |

  Scenario Outline: Text longer than the model accepts is cut at 512 tokens
    When the text of <words> repeated words is tokenized
    Then the token IDs number <count>
    And the last token ID is 2

    Examples:
      | words | count |
      | 100   | 102   |
      | 510   | 512   |
      | 2000  | 512   |
