@F-SEM-1 @PRD-9.2 @M3
Feature: Installing the embedding model
  As a user who wants to search by meaning
  I want to put the embedding model into my library, online or offline
  So that semantic search works without any network access afterwards

  Background:
    Given an empty working directory
    And I have run shelf with arguments "init library"
    And the model catalog "multilingual-e5-small" has the files:
      | file                    | content          |
      | model.onnx              | onnx weights     |
      | sentencepiece.bpe.model | spm vocabulary   |

  Scenario Outline: A model is installed from a folder, offline
    Given the folder "<folder>" holds the model files
    When I run shelf with arguments "model install <folder> --library library"
    Then the exit code is 0
    And standard output contains "Installed multilingual-e5-small"
    And the file "library/.shelf/models/multilingual-e5-small/model.onnx" has the content "onnx weights"
    And the file "library/.shelf/models/multilingual-e5-small/sentencepiece.bpe.model" has the content "spm vocabulary"
    And no download was started

    Examples:
      | folder          |
      | offline         |
      | offline/onnx    |
      | my-models/e5    |

  Scenario Outline: A folder without a complete model is rejected
    Given the folder "offline" holds the model files except "<missing>"
    When I run shelf with arguments "model install offline --library library"
    Then the exit code is 1
    And standard error contains "<missing> not found in"
    And the directory "library/.shelf/models/multilingual-e5-small" does not exist

    Examples:
      | missing                 |
      | model.onnx              |
      | sentencepiece.bpe.model |

  Scenario: A path that is no folder is rejected
    When I run shelf with arguments "model install nowhere --library library"
    Then the exit code is 1
    And standard error contains "nowhere: not a folder"

  Scenario Outline: A file that does not match the catalog is rejected and nothing is installed
    Given the folder "offline" holds the model files
    And the file "offline/<file>" has the content "tampered"
    When I run shelf with arguments "model install offline --library library"
    Then the exit code is 1
    And standard error contains "checksum mismatch for <file>"
    And the directory "library/.shelf/models/multilingual-e5-small" does not exist

    Examples:
      | file                    |
      | model.onnx              |
      | sentencepiece.bpe.model |

  Scenario: Installing twice changes nothing
    Given the folder "offline" holds the model files
    And I have run shelf with arguments "model install offline --library library"
    When I run shelf with arguments "model install --library library"
    Then the exit code is 0
    And standard output contains "multilingual-e5-small is already installed"
    And no download was started

  Scenario: Without a path the model is downloaded
    Given the download serves the model files
    When I run shelf with arguments "model install --library library"
    Then the exit code is 0
    And standard output contains "Downloaded multilingual-e5-small"
    And the file "library/.shelf/models/multilingual-e5-small/model.onnx" has the content "onnx weights"
    And the download requested "https://models.example/multilingual-e5-small/model.onnx"

  Scenario: A failed download names the offline way and leaves nothing behind
    Given the download fails with "connection refused"
    When I run shelf with arguments "model install --library library"
    Then the exit code is 1
    And standard error contains "download failed: connection refused"
    And standard error contains "shelf model install <folder>"
    And the directory "library/.shelf/models/multilingual-e5-small" does not exist

  Scenario Outline: A download that does not match the catalog is rejected
    Given the download serves "tampered" for "<file>"
    When I run shelf with arguments "model install --library library"
    Then the exit code is 1
    And standard error contains "checksum mismatch for <file>"
    And the directory "library/.shelf/models/multilingual-e5-small" does not exist

    Examples:
      | file                    |
      | model.onnx              |
      | sentencepiece.bpe.model |

  Scenario: The library config names another model
    Given the config file of the library "library" contains the line "semantic.model = 'bge-m3'"
    When I run shelf with arguments "model install --library library"
    Then the exit code is 1
    And standard error contains "unknown model 'bge-m3'; known models: multilingual-e5-small"

  Scenario: JSON output
    Given the folder "offline" holds the model files
    When I run shelf with arguments "model install offline --library library --json"
    Then the exit code is 0
    And the JSON string property "model" of standard output matches "^multilingual-e5-small$"
    And the JSON string property "status" of standard output matches "^installed$"
    And the JSON string property "path" of standard output matches "multilingual-e5-small$"
