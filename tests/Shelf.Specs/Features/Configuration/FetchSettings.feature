@F-PAP-8 @PRD-9.2 @M9
Feature: The e-mail address for Unpaywall in the config
  As a user of shelf fetch
  I want to give Unpaywall the e-mail address it asks for
  So that shelf can look for a free PDF of a paper that arXiv does not have

  Scenario: Without a config file Unpaywall is not used
    When shelf resolves the settings
    Then the setting "fetch.unpaywall_email" is ""
    And there are no warnings

  Scenario Outline: The library config overrides the global config
    Given the global config contains the line "<global line>"
    And the library config contains the line "<library line>"
    When shelf resolves the settings
    Then the setting "fetch.unpaywall_email" is "<expected>"
    And there are no warnings

    Examples:
      | global line                                  | library line                                 | expected        |
      | # nothing                                    | # nothing                                    |                 |
      | fetch.unpaywall_email = 'me@example.org'     | # nothing                                    | me@example.org  |
      | # nothing                                    | fetch.unpaywall_email = 'lab@example.org'    | lab@example.org |
      | fetch.unpaywall_email = 'me@example.org'     | fetch.unpaywall_email = 'lab@example.org'    | lab@example.org |

  Scenario Outline: An unusable value is rejected with the key and the file
    Given the library config contains the line "<line>"
    When shelf resolves the settings
    Then resolving the settings fails with an error containing "<message>"

    Examples:
      | line                                   | message                                                           |
      | fetch.unpaywall_email = ''             | library.toml: fetch.unpaywall_email: must not be empty            |
      | fetch.unpaywall_email = 'not an email' | library.toml: fetch.unpaywall_email: must be an e-mail address    |
      | fetch.unpaywall_email = '@example.org' | library.toml: fetch.unpaywall_email: must be an e-mail address    |
      | fetch.unpaywall_email = 5              | library.toml: fetch.unpaywall_email: expected string, found integer |
