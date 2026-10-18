@F-GRP-1 @M1
Feature: Author surnames and sort keys
  As a user whose files spell author names in different ways
  I want "Martin Kleppmann" and "Kleppmann, Martin" to be recognized as the same surname
  So that title and author can be compared across files

  Scenario Outline: The surname key and the sort key of a name
    When shelf reads the author name "<name>"
    Then the surname key is "<key>"
    And the sort key is "<sort key>"

    Examples:
      | name                    | key        | sort key                    |
      | Martin Kleppmann        | kleppmann  | Kleppmann, Martin           |
      | Kleppmann, Martin       | kleppmann  | Kleppmann, Martin           |
      | Edwin A. Abbott         | abbott     | Abbott, Edwin A.            |
      | Edwin Abbott Abbott     | abbott     | Abbott, Edwin Abbott        |
      | J. R. R. Tolkien        | tolkien    | Tolkien, J. R. R.           |
      | Tolkien, J. R. R.       | tolkien    | Tolkien, J. R. R.           |
      | Jan van der Berg        | berg       | van der Berg, Jan           |
      | van der Berg, Jan       | berg       | van der Berg, Jan           |
      | Ludwig van Beethoven    | beethoven  | van Beethoven, Ludwig       |
      | Jördis Ålander          | alander    | Ålander, Jördis             |
      | Lukas Weiß              | weiss      | Weiß, Lukas                 |
      | Martin Luther King Jr.  | king       | King, Martin Luther Jr.     |
      | King, Martin Luther Jr. | king       | King, Martin Luther Jr.     |
      | Madonna                 | madonna    | Madonna                     |
      |   Jane    Doe  ,  Roe  | doe        | Jane Doe, Roe               |

  Scenario Outline: A name without letters has no surname key
    When shelf reads the author name "<name>"
    Then there is no surname key

    Examples:
      | name |
      |      |
      | ...  |
      | ,    |

  Scenario Outline: Authors match if they share a surname
    When shelf compares the authors "<first>" and "<second>"
    Then the authors <result>

    Examples:
      | first                           | second                         | result      |
      | Martin Kleppmann                | Kleppmann, Martin              | match       |
      | Lukas Weiß; Ana Núñez           | Ana Nunez                      | match       |
      | Jane Doe; John Roe              | John Roe                       | match       |
      | Jane Doe                        | John Roe                       | do not match |
      | Jane Doe                        |                                | do not match |
      |                                 |                                | do not match |
