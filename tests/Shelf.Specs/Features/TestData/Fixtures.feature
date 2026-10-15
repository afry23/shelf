@PRD-10.1 @M0
Feature: Committed e-book fixtures
  As a developer of shelf
  I want a small, documented set of real and derived e-book files in the repository
  So that parsers can be tested against known files whose redistribution is allowed

  Background:
    Given the fixture manifest "tests/fixtures/manifest.json"

  Scenario: The manifest and the fixture directory match
    Then every file under "tests/fixtures" except "README.md" and "manifest.json" is listed in the manifest
    And every file listed in the manifest exists

  Scenario: Every fixture has the size and SHA-256 recorded in the manifest
    Then every fixture file has the size and SHA-256 recorded in the manifest

  Scenario: The fixtures stay within the size budget
    Then the total size of all fixture files is at most 5242880 bytes
    And no fixture file is larger than 1572864 bytes

  Scenario: Every upstream fixture documents its provenance and license
    Then every fixture of origin "upstream" has a source URL, a retrieval date, a license, a US public-domain basis and a life+70 basis
    And every fixture of origin "upstream" is unmodified

  Scenario: Every derived fixture names the command that produced it
    Then every fixture of origin "derived" names its generator command and has no source URL

  Scenario Outline: The fixtures cover each trait needed by later parsers
    Then at least one fixture of origin "<origin>" has the trait "<trait>"

    Examples:
      | origin   | trait           |
      | upstream | epub2           |
      | upstream | epub3           |
      | upstream | cover           |
      | upstream | language-de     |
      | upstream | pdf-text-layer  |
      | any      | non-ascii-title |
      | any      | no-cover        |
      | derived  | isbn            |
      | derived  | series          |
      | derived  | empty-metadata  |
      | derived  | malformed-opf   |
      | derived  | not-a-zip       |
      | derived  | pdf-image-only  |
      | derived  | pdf-xmp         |
      | derived  | pdf-no-metadata |

  Scenario Outline: Some works are present in more than one file
    Then the fixtures contain a work of origin "<origin>" with files of the traits "<first>" and "<second>"

    Examples:
      | origin   | first | second         |
      | upstream | epub2 | epub3          |
      | derived  | epub3 | pdf-text-layer |

  Scenario: Derived fixtures are reproducible
    When the derived fixtures are regenerated into a temporary directory
    Then every regenerated file is byte-identical to the committed derived fixture with the same name
    And no committed derived fixture is missing from the regenerated set
