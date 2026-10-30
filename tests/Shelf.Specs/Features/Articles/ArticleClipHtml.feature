@F-ART-3 @F-ART-4 @F-ART-5 @M13
Feature: Keeping a saved web page as a Markdown article
  As a reader with a subscription
  I want shelf clip to turn a page I saved in my browser into an article with its images
  So that paywalled articles are kept without shelf ever seeing my login

  Background:
    Given an empty working directory
    And I have run shelf with arguments "init library"

  Scenario: The article text becomes Markdown and the page furniture is dropped
    Given a saved web page "post.html" with content:
      """
      <html><head>
        <title>Why Tests Matter | Example Blog</title>
        <meta property="og:title" content="Why Tests Matter">
        <meta property="og:site_name" content="Example Blog">
        <meta name="author" content="Jane Doe">
        <meta property="article:published_time" content="2024-03-05T09:00:00Z">
        <link rel="canonical" href="https://blog.example.com/why-tests-matter?utm_source=feed">
      </head><body>
        <nav>Home About Subscribe</nav>
        <article>
          <h1>Why Tests Matter</h1>
          <p>Tests are <strong>executable</strong> documentation.</p>
          <h2>Reasons</h2>
          <ul><li>They fail loudly</li><li>They document intent</li></ul>
          <blockquote>Code without tests is broken by design.</blockquote>
          <pre><code>dotnet test</code></pre>
        </article>
        <aside>Related: ten more posts</aside>
        <div id="comments">First! Great post.</div>
        <script>trackEverything()</script>
      </body></html>
      """
    When I run shelf with arguments "clip --html post.html --library library"
    Then the exit code is 0
    And standard output contains "Clipped Why Tests Matter"
    And the work "Why Tests Matter" has the kind "article"
    And the work "Why Tests Matter" has the authors "Jane Doe"
    And the work "Why Tests Matter" has the year 2024
    And the work "Why Tests Matter" has the site "Example Blog"
    And the work "Why Tests Matter" has the source URL "https://blog.example.com/why-tests-matter"
    And the article "Why Tests Matter" contains "Tests are **executable** documentation."
    And the article "Why Tests Matter" contains "## Reasons"
    And the article "Why Tests Matter" contains "- They fail loudly"
    And the article "Why Tests Matter" contains "> Code without tests is broken by design."
    And the article "Why Tests Matter" does not contain "Subscribe"
    And the article "Why Tests Matter" does not contain "Great post"
    And the article "Why Tests Matter" does not contain "trackEverything"

  Scenario Outline: Images that belong to the article are saved next to it and linked relative
    Given a saved web page "post.html" with the article "Photo Essay" and the image markup <markup>
    And the image "<file>" of the saved page "post.html" is a <width> by <height> picture
    When I run shelf with arguments "clip --html post.html --library library"
    Then the exit code is 0
    And the article "Photo Essay" has <count> images in its folder
    And the article "Photo Essay" contains "![<alt>](images/01-"

    Examples:
      | markup                                                                          | file      | width | height | alt     | count |
      | <img src="a.jpg" alt="Harbour">                                                 | a.jpg     | 1200  | 800    | Harbour | 1     |
      | <img src="s.jpg" srcset="s.jpg 400w, l.jpg 1600w" alt="Harbour">               | l.jpg     | 1600  | 900    | Harbour | 1     |
      | <img src="p.gif" data-src="real.jpg" alt="Harbour">                             | real.jpg  | 1000  | 700    | Harbour | 1     |
      | <figure><img src="f.jpg" alt="Harbour"><figcaption>Low tide</figcaption></figure> | f.jpg     | 900   | 600    | Low tide | 1    |

  Scenario Outline: Images that do not belong to the article are skipped
    Given a saved web page "post.html" with the article "Photo Essay" and the image markup <markup>
    And the image "<file>" of the saved page "post.html" is a <width> by <height> picture
    When I run shelf with arguments "clip --html post.html --library library"
    Then the exit code is 0
    And the article "Photo Essay" has 0 images in its folder

    Examples:
      | markup                                              | file      | width | height |
      | <img src="pixel.gif" alt="">                        | pixel.gif | 1     | 1      |
      | <img src="icon.png" alt="icon">                     | icon.png  | 32    | 32     |
      | <img src="t.gif" width="1" height="1" alt="">       | t.gif     | 1     | 1      |

  Scenario: The same image used twice is saved once
    Given a saved web page "post.html" with the article "Photo Essay" and the image markup <img src="a.jpg" alt="One"><img src="copy.jpg" alt="Two">
    And the image "a.jpg" of the saved page "post.html" is a 1200 by 800 picture
    And the image "copy.jpg" of the saved page "post.html" is the same picture as "a.jpg"
    When I run shelf with arguments "clip --html post.html --library library"
    Then the article "Photo Essay" has 1 images in its folder
    And the article "Photo Essay" links the same image twice

  Scenario: An MHTML page carries its images inside
    Given a saved MHTML page "post.mhtml" with the article "Photo Essay" and one 1200 by 800 image
    When I run shelf with arguments "clip --html post.mhtml --library library"
    Then the exit code is 0
    And the article "Photo Essay" has 1 images in its folder

  Scenario: An image that is missing stays a link to its source and is reported
    Given a saved web page "post.html" with the article "Photo Essay" and the image markup <img src="https://cdn.example.com/gone.jpg" alt="Gone">
    When I run shelf with arguments "clip --html post.html --library library"
    Then the exit code is 0
    And standard error contains "image not saved: https://cdn.example.com/gone.jpg"
    And the article "Photo Essay" contains "![Gone](https://cdn.example.com/gone.jpg)"
    And the online source "web" was not queried

  Scenario: A page without an article is refused
    Given a saved web page "empty.html" with content:
      """
      <html><body><nav>Menu</nav><footer>Imprint</footer></body></html>
      """
    When I run shelf with arguments "clip --html empty.html --library library"
    Then the exit code is 1
    And standard error contains "no article found in empty.html"
    And the table "works" of the index has 0 rows
