@F-SRC-3 @M1
Feature: The search query language
  As a user searching my library
  I want free text, phrases, prefixes, field filters and negation in one line
  So that I can narrow a search without learning a query syntax

  Scenario Outline: A query is understood as words, filters and exclusions
    When shelf parses the search query '<query>'
    Then the parsed query is '<parsed>'

    Examples:
      | query                                        | parsed                                                       |
      | kubernetes                                   | include: kubernetes                                          |
      | kubernetes patterns                          | include: kubernetes, patterns                                |
      |   kubernetes     patterns                    | include: kubernetes, patterns                                |
      | "event sourcing"                             | include: "event sourcing"                                    |
      | kube*                                        | include: kube*                                               |
      | "kube*"                                      | include: "kube*"                                             |
      | -azure                                       | exclude: azure                                               |
      | -"event sourcing"                            | exclude: "event sourcing"                                    |
      | author:ibryam                                | filters: author:ibryam                                       |
      | AUTHOR:Ibryam                                | filters: author:Ibryam                                       |
      | author:"Martin Kleppmann"                    | filters: author:"Martin Kleppmann"                           |
      | title:kube*                                  | filters: title:kube*                                         |
      | tag:cloud/azure                              | filters: tag:cloud/azure                                     |
      | -tag:devops                                  | filters: -tag:devops                                         |
      | publisher:manning                            | filters: publisher:manning                                   |
      | PUBLISHER:Manning                            | filters: publisher:Manning                                   |
      | publisher:"Manning Publications"             | filters: publisher:"Manning Publications"                    |
      | publisher:manni*                             | filters: publisher:manni*                                    |
      | -publisher:manning                           | filters: -publisher:manning                                  |
      | lang:DE                                      | filters: lang:de                                             |
      | format:PDF                                   | filters: format:pdf                                          |
      | status:Unread                                | filters: status:unread                                       |
      | year:2015                                    | filters: year:2015                                           |
      | year:>2015                                   | filters: year:>=2016                                         |
      | year:>=2015                                  | filters: year:>=2015                                         |
      | year:<2000                                   | filters: year:<=1999                                         |
      | year:<=2000                                  | filters: year:<=2000                                         |
      | year:2010..2015                              | filters: year:2010..2015                                     |
      | -year:2015                                   | filters: -year:2015                                          |
      | kubernetes -azure tag:cloud year:>2016       | include: kubernetes; exclude: azure; filters: tag:cloud, year:>=2017 |
      | unknown:field                                | include: unknown:field                                       |
      | C++                                          | include: C++                                                 |
      | state-of-the-art                             | include: state-of-the-art                                    |
      | "an unfinished phrase                        | include: "an unfinished phrase"                              |

  Scenario Outline: A query without anything to search for is empty
    When shelf parses the search query '<query>'
    Then the parsed query is '(empty)'

    Examples:
      | query |
      |       |
      | -     |
      | *     |
      | "     |
      | ""    |
      | ...   |
      | - -   |

  Scenario Outline: A filter with an invalid value is an error that says what is expected
    When shelf parses the search query '<query>'
    Then parsing the search query fails with "<message>"

    Examples:
      | query            | message                                                          |
      | year:abc         | year: 'abc' is not a year, a comparison like >2015 or a range like 2010..2015 |
      | year:>           | year: '>' is not a year                                          |
      | year:2015..2010  | year: the range 2015..2010 is empty                              |
      | year:20150       | year: '20150' is not a year                                      |
      | status:done      | status: 'done' is not one of unread, reading, read               |
      | lang:            | lang: needs a value                                              |
      | author:          | author: needs a value                                            |
      | format:""        | format: needs a value                                            |
