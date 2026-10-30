@F-WEB-1 @F-WEB-2 @F-WEB-6 @M4
Feature: Settings of the web server
  As a user of shelf
  I want to set the port, the address and the token of the web server in the global config
  So that I do not have to repeat them on every start

  Scenario Outline: The web settings come from the global config
    Given the global config contains the line "<line>"
    When shelf resolves the settings
    Then the setting "<key>" is "<value>"

    Examples:
      | line                     | key       | value     |
      | # nothing                | web.port  | 8420      |
      | web.port = 9000          | web.port  | 9000      |
      | web.port = 0             | web.port  | 0         |
      | # nothing                | web.bind  | 127.0.0.1 |
      | web.bind = '192.168.1.5' | web.bind  | 192.168.1.5 |
      | # nothing                | web.token |           |
      | web.token = 'abc'        | web.token | abc       |
      | web.token = ''           | web.token |           |
      | # nothing                | web.page_size | 20    |
      | web.page_size = 50       | web.page_size | 50    |
      | web.page_size = 1        | web.page_size | 1     |
      | web.page_size = 100      | web.page_size | 100   |

  Scenario Outline: Invalid web settings are rejected with the key and the file
    Given the global config contains the line "<line>"
    When shelf resolves the settings
    Then resolving the settings fails with an error containing "<message>"

    Examples:
      | line             | message                                          |
      | web.port = -1    | global.toml: web.port: must be between 0 and 65535 |
      | web.port = 65536 | global.toml: web.port: must be between 0 and 65535 |
      | web.port = 'x'   | global.toml: web.port: expected integer, found string |
      | web.bind = ''    | global.toml: web.bind: must not be empty         |
      | web.bind = 3     | global.toml: web.bind: expected string, found integer |
      | web.token = 3    | global.toml: web.token: expected string, found integer |
      | web.page_size = 0   | global.toml: web.page_size: must be between 1 and 100 |
      | web.page_size = 101 | global.toml: web.page_size: must be between 1 and 100 |
      | web.page_size = 'x' | global.toml: web.page_size: expected integer, found string |

  Scenario Outline: The per-library config cannot set the web server, so that a token does not travel with a library folder
    Given the library config contains the line "<line>"
    When shelf resolves the settings
    Then there is the warning "library.toml: unknown key '<key>'"
    And the setting "<key>" is "<value>"

    Examples:
      | line              | key       | value     |
      | web.token = 'abc' | web.token |           |
      | web.port = 9000   | web.port  | 8420      |
      | web.bind = '::'   | web.bind  | 127.0.0.1 |
      | web.page_size = 50 | web.page_size | 20    |
