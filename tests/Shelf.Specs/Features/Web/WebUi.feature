@F-WEB-3 @M4
Feature: The web interface is part of the binary
  As a user of shelf
  I want `shelf serve` to open the interface without anything else installed
  So that there is no Node.js, no web server and no folder of files to look after

  Background:
    Given an empty working directory
    And I have run shelf with arguments "init library"
    And the web server is running on the library "library"

  Scenario: The root is the page of the build
    When I request "GET /"
    Then the response status is 200
    And the response header "Content-Type" starts with "text/html"
    And the response body is the built UI file "index.html"
    And the response body contains "<title>shelf</title>"

  Scenario: The script and the style of the page are served from the binary
    Given I note the script and the style of the page
    When I request "GET {script}"
    Then the response status is 200
    And the response header "Content-Type" starts with "text/javascript"
    And the response body is the built UI file of the noted script
    When I request "GET {style}"
    Then the response status is 200
    And the response header "Content-Type" starts with "text/css"
    And the response body is the built UI file of the noted style

  Scenario: Files with a content hash in their name are cached for good, the page is checked every time
    Given I note the script and the style of the page
    When I request "GET {script}"
    Then the response header "Cache-Control" is "public, max-age=31536000, immutable"
    When I request "GET /"
    Then the response header "Cache-Control" is "no-cache"

  Scenario Outline: An address without a file name gives the page, so that the browser can run its own routes
    When I request "GET <path>"
    Then the response status is 200
    And the response body is the built UI file "index.html"

    Examples:
      | path                              |
      | /library                          |
      | /library/abc                      |
      | /search?q=event                   |
      | /works/01J9Z3K0000000000000000001 |

  Scenario Outline: An address with a dot gives the page when a browser asks for a page, so that the reader can be reloaded
    When I request "GET <path>" with the header "Accept: text/html,application/xhtml+xml"
    Then the response status is 200
    And the response body is the built UI file "index.html"

    Examples:
      | path                                              |
      | /read/01J9Z3K0000000000000000001~kp.epub          |
      | /read/01J9Z3K0000000000000000001~Kubernetes%20Patterns%20(2nd).pdf |

  Scenario Outline: A file that does not exist is a 404, never the page
    When I request "GET <path>"
    Then the response status is 404
    And the response header "Content-Type" is "application/problem+json"

    Examples:
      | path                  |
      | /missing.js           |
      | /assets/missing.css   |
      | /favicon.ico          |

  Scenario Outline: An address under /api that is no endpoint is a 404 in JSON, never the page
    When I request "GET <path>"
    Then the response status is 404
    And the response header "Content-Type" is "application/problem+json"

    Examples:
      | path                  |
      | /api                  |
      | /api/v1               |
      | /api/v1/unknown       |
      | /api/v2/status        |
      | /api/v1/works/x/y/z   |

  Scenario Outline: The page takes no other method than GET and HEAD
    When I request "<method> /library"
    Then the response status is 405
    And the response header "Allow" contains "GET"

    Examples:
      | method |
      | POST   |
      | PUT    |
      | DELETE |

  Scenario: An endpoint of the API still says which methods it takes
    When I request "POST /api/v1/status"
    Then the response status is 405
    And the response header "Allow" contains "GET"

  Scenario: HEAD gives the headers of the page and no body
    When I request "HEAD /"
    Then the response status is 200
    And the response header "Content-Type" starts with "text/html"
    And the response body is empty

  Scenario: A page that has not changed is not sent again
    Given I request "GET /"
    When I request "GET /" again with the ETag
    Then the response status is 304

  Scenario Outline: Every answer carries the security headers
    When I request "GET <path>"
    Then the response header "X-Content-Type-Options" is "nosniff"
    And the response header "X-Frame-Options" is "SAMEORIGIN"
    And the response header "Referrer-Policy" is "same-origin"

    Examples:
      | path             |
      | /                |
      | /api/v1/status   |
      | /api/v1/unknown  |
      | /missing.js      |

  Scenario: With a token the page and its files are public and the API is not
    Given the web server is running on the library "library" with the arguments "--token s3cret"
    And I note the script and the style of the page
    When I request "GET /"
    Then the response status is 200
    When I request "GET {script}"
    Then the response status is 200
    When I request "GET /library/abc"
    Then the response status is 200
    When I request "GET /api/v1/status"
    Then the response status is 401

  Scenario: The page shows the API only after the token was accepted
    Given the web server is running on the library "library" with the arguments "--token s3cret"
    When I request "GET /?token=s3cret"
    Then the response status is 303
    And the response header "Location" is "/"

  Scenario Outline: Every answer carries a content security policy that blocks scripts but the program's own
    When I request "GET <path>"
    Then the response header "Content-Security-Policy" contains "script-src 'self'"
    And the response header "Content-Security-Policy" contains "object-src 'none'"
    And the response header "Content-Security-Policy" contains "frame-ancestors 'self'"
    And the response header "Content-Security-Policy" contains "base-uri 'none'"

    Examples:
      | path             |
      | /                |
      | /library         |
      | /api/v1/status   |
      | /api/v1/unknown  |
      | /missing.js      |

  Scenario: The policy lets the reader open books in frames and run its worker
    When I request "GET /"
    Then the response header "Content-Security-Policy" contains "frame-src 'self' blob:"
    And the response header "Content-Security-Policy" contains "worker-src 'self' blob:"
    And the response header "Content-Security-Policy" contains "img-src 'self' data: blob:"

  Scenario: The policy does not allow inline scripts or remote sources
    When I request "GET /"
    Then the response header "Content-Security-Policy" does not contain "unsafe-eval"
    And the response header "Content-Security-Policy" does not contain "script-src 'self' 'unsafe-inline'"
    And the response header "Content-Security-Policy" does not contain "http:"
    And the response header "Content-Security-Policy" does not contain "https:"

  Scenario Outline: The files that the PDF reader loads are served with a usable type
    When I request "GET <path>"
    Then the response status is 200
    And the response header "Content-Type" starts with "<type>"

    Examples:
      | path                                         | type                     |
      | /pdfjs/wasm/openjpeg.wasm                    | application/wasm         |
      | /pdfjs/standard_fonts/FoxitSerif.pfb          | application/octet-stream |
      | /pdfjs/cmaps/Adobe-Japan1-UCS2.bcmap         | application/octet-stream |
