@F-ART-1 @F-ART-9 @M13
Feature: An article folder is one unit
  As a reader who organizes the library
  I want the Markdown file, the images and the sidecar to stay together
  So that no operation leaves an article without its pictures

  Background:
    Given an empty working directory
    And I have run shelf with arguments "init library"
    And an article "Why Tests Matter" from the site "Example Blog" with the text "Tests document intent." and 2 images

  Scenario: The sidecar keeps the article fields and reindex restores them
    Then the sidecar of the work "Why Tests Matter" has the kind "article"
    And the sidecar of the work "Why Tests Matter" has the schema 1
    When I delete the file "library/.shelf/index.db"
    And I run shelf with arguments "reindex --library library"
    Then the work "Why Tests Matter" has the kind "article"
    And the work "Why Tests Matter" has the site "Example Blog"

  Scenario: Scan does not take images for works
    When I run shelf with arguments "scan --library library"
    Then the exit code is 0
    And the table "works" of the index has 1 rows

  Scenario: Organize and undo move the whole folder
    Given the folder of the article "Why Tests Matter" is renamed to "Misc/Old Stuff"
    When I run shelf with arguments "organize --apply --library library"
    Then the exit code is 0
    And the article "Why Tests Matter" has 2 images in its folder
    And the article "Why Tests Matter" is found at "Example Blog/Why Tests Matter"
    And the directory "library/Misc" does not exist
    When I run shelf with arguments "undo --library library"
    Then the article "Why Tests Matter" has 2 images in its folder
    And the article "Why Tests Matter" is found at "Misc/Old Stuff"

  Scenario: Trash takes the images along
    When I run shelf with arguments "delete 'library/Example Blog/Why Tests Matter' --yes --library library"
    Then the exit code is 0
    And the trash holds the article "Why Tests Matter" with 2 images
    And the table "works" of the index has 0 rows
