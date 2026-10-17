@F-LIB-5 @M1
Feature: Work IDs are ULIDs
  As a user whose library lives in plain folders
  I want every work to carry a stable, sortable ID
  So that references to a work survive renames, moves and a rebuilt index

  Scenario Outline: A ULID is the timestamp in milliseconds followed by random bytes in Crockford base32
    When shelf creates a ULID for the timestamp <milliseconds> and the random bytes "<random>"
    Then the ULID text is "<text>"
    And the ULID timestamp is <milliseconds>

    Examples:
      | milliseconds    | random               | text                       |
      | 0               | 00000000000000000000 | 00000000000000000000000000 |
      | 281474976710655 | ffffffffffffffffffff | 7ZZZZZZZZZZZZZZZZZZZZZZZZZ |
      | 1               | 00000000000000000000 | 00000000010000000000000000 |
      | 0               | 00000000000000000001 | 00000000000000000000000001 |
      | 1469922850259   | 0123456789abcdef0123 | 01ARZ3NDEK04HMASW9NF6YY093 |
      | 1760642640000   | ffeeddccbbaa99887766 | 01K7Q7NA40ZZQDVK5VNACRGXV6 |

  Scenario: ULIDs created later sort after earlier ones
    When shelf creates ULIDs for the timestamps 1000, 1001 and 70000
    Then the ULID texts are in ascending order

  Scenario: New ULIDs are valid and unique
    When shelf creates 1000 new ULIDs
    Then all ULIDs are valid and different from each other

  Scenario Outline: Valid ULID text is accepted in either case
    When shelf parses the ULID text "<text>"
    Then the ULID text is "<normalized>"

    Examples:
      | text                       | normalized                 |
      | 01ARZ3NDEK04HMASW9NF6YY093 | 01ARZ3NDEK04HMASW9NF6YY093 |
      | 01arz3ndek04hmasw9nf6yy093 | 01ARZ3NDEK04HMASW9NF6YY093 |
      | 7ZZZZZZZZZZZZZZZZZZZZZZZZZ | 7ZZZZZZZZZZZZZZZZZZZZZZZZZ |

  Scenario Outline: Text that is not a ULID is rejected
    When shelf parses the ULID text "<text>"
    Then parsing the ULID fails

    Examples:
      | text                        | reason                      |
      |                             | empty                       |
      | 01ARZ3NDEK04HMASW9NF6YY09   | 25 characters               |
      | 01ARZ3NDEK04HMASW9NF6YY0933 | 27 characters               |
      | 01ARZ3NDEK04HMASW9NF6YY09I  | I is not in the alphabet    |
      | 01ARZ3NDEK04HMASW9NF6YY09L  | L is not in the alphabet    |
      | 01ARZ3NDEK04HMASW9NF6YY09O  | O is not in the alphabet    |
      | 01ARZ3NDEK04HMASW9NF6YY09U  | U is not in the alphabet    |
      | 8ZZZZZZZZZZZZZZZZZZZZZZZZZ  | larger than 48 bit time     |
      | 01ARZ3NDEK04HMASW9NF6YY0-3  | punctuation                 |
