@F-GRP-2 @M1
Feature: Title normalization
  As a user with several files of the same book
  I want titles to be compared without case, accents, punctuation, leading articles and edition notes
  So that "The Hobbit (2nd Edition)" and "Hobbit" are recognized as the same title

  Scenario Outline: A title is reduced to its comparable form
    When shelf normalizes the title "<title>"
    Then the normalized title is "<normalized>"

    Examples:
      | title                                                | normalized                                        |
      | The Hobbit                                           | hobbit                                            |
      | the hobbit                                           | hobbit                                            |
      | A Tale of Two Cities                                 | tale of two cities                                |
      | An Introduction to Algorithms                        | introduction to algorithms                        |
      | Der Prozess                                          | prozess                                           |
      | Die Verwandlung                                      | verwandlung                                       |
      | Das Schloss                                          | schloss                                           |
      | Größenordnungen verteilter Systeme                   | grossenordnungen verteilter systeme               |
      | Straße der Erinnerung                                | strasse der erinnerung                            |
      | Ångström & Æther                                     | angstrom aether                                   |
      | L'Étranger                                           | l etranger                                        |
      | Café – Menü!                                         | cafe menu                                         |
      |   Spaced    Out  ,,  Title ;                         | spaced out title                                  |
      | Designing Data-Intensive Applications                | designing data intensive applications             |
      | Designing Data-Intensive Applications (2nd Edition)  | designing data intensive applications             |
      | Clean Code (German Edition)                          | clean code                                        |
      | Dune (Second Edition)                                | dune                                              |
      | A Tale of Two Cities, 3rd ed.                        | tale of two cities                                |
      | Python Crash Course, 3. Auflage                      | python crash course                               |
      | Praxishandbuch Verteilung (Deutsche Ausgabe)         | praxishandbuch verteilung                         |
      | Praxishandbuch Verteilung, Neuausgabe                | praxishandbuch verteilung                         |
      | Praxishandbuch Verteilung (3. überarbeitete Auflage) | praxishandbuch verteilung                         |
      | Praxishandbuch Verteilung, zweite Auflage            | praxishandbuch verteilung                         |
      | Praxishandbuch Verteilung, Neuauflage                | praxishandbuch verteilung                         |
      | Das Gesamtausgabe Projekt                            | gesamtausgabe projekt                             |
      | Clean Code: A Handbook of Agile Software Craftsmanship | clean code a handbook of agile software craftsmanship |
      | Flatland: A Romance of Many Dimensions               | flatland a romance of many dimensions             |
      | 1984                                                 | 1984                                              |
      | Catch-22                                             | catch 22                                          |
      | Edition 4                                            | edition 4                                         |
      | Theory of Everything                                 | theory of everything                              |
      | Another Day                                          | another day                                       |

  Scenario Outline: A title that would become empty keeps its lower-case form
    When shelf normalizes the title "<title>"
    Then the normalized title is "<normalized>"

    Examples:
      | title | normalized |
      | The   | the        |
      | !!!   | !!!        |
      | A     | a          |

  Scenario Outline: Different books stay different
    When shelf normalizes the titles "<first>" and "<second>"
    Then the normalized titles are different

    Examples:
      | first                       | second                       |
      | Python: Introduction        | Python: Advanced Topics      |
      | Volume 1                    | Volume 2                     |
      | Dune                        | Dune Messiah                 |
      | The Hobbit                  | The Hobbit Companion         |
