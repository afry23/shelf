@F-PAP-6 @M9
Feature: Importing a BibTeX file
  As a researcher who comes from Zotero or Mendeley
  I want shelf import-bib to match the entries of my .bib file to the works I already have
  So that my cite keys, DOIs and venues carry over without typing them again

  Background:
    Given an empty working directory
    And I have run shelf with arguments "init library"
    And a paper "attention" with the arXiv ID "1706.03762" and the title "Attention Is All You Need" and no authors
    And a paper "Deep Residual Learning for Image Recognition" by "Kaiming He" from 2016 with the cite key "he2016deep"
    And a book "Kubernetes Patterns" by "Bilgin Ibryam" from 2019

  Scenario: The preview shows what would change and writes nothing
    Given a BibTeX file "refs.bib" with content:
      """
      @inproceedings{vaswani2017,
        title     = {Attention Is All You Need},
        author    = {Vaswani, Ashish and Shazeer, Noam and Parmar, Niki},
        booktitle = {Advances in Neural Information Processing Systems},
        year      = {2017},
        eprint    = {1706.03762},
        archiveprefix = {arXiv},
        doi       = {10.5555/3295222.3295349}
      }
      """
    When I run shelf with arguments "import-bib refs.bib --library library"
    Then the exit code is 0
    And standard output contains "vaswani2017 -> "
    And standard output contains "matched by arXiv ID 1706.03762"
    And standard output contains "+ authors: Ashish Vaswani, Noam Shazeer, Niki Parmar"
    And standard output contains "+ doi: 10.5555/3295222.3295349"
    And standard output contains "+ venue: Advances in Neural Information Processing Systems"
    And standard output contains "+ year: 2017"
    And standard output contains "Nothing written; run again with --apply"
    And the work "Attention Is All You Need" has no authors in the index

  Scenario: Applying fills the empty fields and keeps the others
    Given a BibTeX file "refs.bib" with content:
      """
      @inproceedings{vaswani2017,
        title     = {Attention Is All You Need},
        author    = {Vaswani, Ashish and Shazeer, Noam and Parmar, Niki},
        booktitle = {NeurIPS},
        year      = {2017},
        eprint    = {1706.03762v5},
        archiveprefix = {arXiv}
      }
      @article{he-resnet,
        title   = {Deep Residual Learning for Image Recognition},
        author  = {He, Kaiming and Zhang, Xiangyu},
        year    = {2015},
        journal = {CoRR},
        doi     = {10.1109/CVPR.2016.90}
      }
      """
    When I run shelf with arguments "import-bib refs.bib --apply --library library"
    Then the exit code is 0
    And the work "Attention Is All You Need" has the authors "Ashish Vaswani, Noam Shazeer, Niki Parmar"
    And the work "Attention Is All You Need" has the year 2017
    And the work "Attention Is All You Need" has the venue "NeurIPS"
    And the work "Deep Residual Learning for Image Recognition" has the year 2016
    And the work "Deep Residual Learning for Image Recognition" has the venue "CoRR"
    And the work "Deep Residual Learning for Image Recognition" has the DOI "10.1109/cvpr.2016.90"
    And the work "Deep Residual Learning for Image Recognition" has the cite key "he2016deep"
    And standard output contains "= year: 2016 (kept, bib: 2015)"

  Scenario: An empty cite key is filled, an existing one is kept unless --overwrite
    Given a BibTeX file "refs.bib" with content:
      """
      @misc{attention-key,
        title = {Attention Is All You Need},
        author = {Vaswani, Ashish},
        eprint = {1706.03762}
      }
      @article{resnet-key,
        title = {Deep Residual Learning for Image Recognition},
        author = {He, Kaiming}
      }
      """
    When I run shelf with arguments "import-bib refs.bib --apply --library library"
    Then the work "Attention Is All You Need" has the cite key "attention-key"
    And the work "Deep Residual Learning for Image Recognition" has the cite key "he2016deep"
    When I run shelf with arguments "import-bib refs.bib --apply --overwrite --library library"
    Then the work "Deep Residual Learning for Image Recognition" has the cite key "resnet-key"

  Scenario Outline: An entry is found by DOI, arXiv ID or title with a shared author
    Given a paper "Dropout" by "Nitish Srivastava" from 2014 with the DOI "10.5555/2627435.2670313" and the venue "JMLR"
    And a BibTeX file "refs.bib" with content:
      """
      @article{k1,
        <fields>
      }
      """
    When I run shelf with arguments "import-bib refs.bib --library library"
    Then standard output contains "k1 -> "
    And standard output contains "<how>"

    Examples:
      | fields                                                                        | how                                        |
      | title = {Unrelated}, doi = {https://doi.org/10.5555/2627435.2670313}          | matched by DOI 10.5555/2627435.2670313     |
      | title = {Deep Residual Learning for Image Recognition}, author = {Kaiming He} | matched by title and author                |
      | title = {Other}, eprint = {arXiv:1706.03762v2}                                | matched by arXiv ID 1706.03762             |

  Scenario: Entries that match no work are listed and change nothing
    Given a BibTeX file "refs.bib" with content:
      """
      @book{unknown1, title = {A Book I Do Not Have}, author = {Doe, Jane}, year = {2001}}
      @article{unknown2, title = {Attention Is All You Need}, author = {Somebody Else}}
      """
    When I run shelf with arguments "import-bib refs.bib --apply --library library"
    Then the exit code is 0
    And standard output contains "no work: unknown1 (A Book I Do Not Have)"
    And standard output contains "no work: unknown2 (Attention Is All You Need)"
    And standard output contains "0 of 2 entries matched"
    And the work "Attention Is All You Need" has no authors in the index

  Scenario: BibTeX syntax is read the way LaTeX writes it
    Given a BibTeX file "refs.bib" with content:
      """
      @comment{ignored entirely @article{x, title={Nope}} }
      @string{nips = "Advances in Neural Information Processing Systems"}
      @InProceedings{ vaswani,
        TITLE = "Attention {I}s All {Y}ou Need",
        author = {Vaswani, Ashish and M{\"u}ller, Jörg and others},
        booktitle = nips # { 30},
        year = 2017,
        eprint = {1706.03762}
      }
      """
    When I run shelf with arguments "import-bib refs.bib --library library"
    Then standard output contains "+ venue: Advances in Neural Information Processing Systems 30"
    And standard output contains "+ authors: Ashish Vaswani, Jörg Müller"
    And standard output does not contain "Nope"

  Scenario Outline: A file that cannot be used is refused
    Given a BibTeX file "refs.bib" with content:
      """
      <content>
      """
    When I run shelf with arguments "import-bib <argument> --library library"
    Then the exit code is 1
    And standard error contains "<message>"

    Examples:
      | content           | argument | message                    |
      | just some text    | refs.bib | no BibTeX entries found    |
      | @article{k1,      | refs.bib | no BibTeX entries found    |
      | @article{k1, title = {X}} | missing.bib | cannot read      |

  Scenario: Nothing is asked online
    Given a BibTeX file "refs.bib" with content:
      """
      @article{k1, title = {Deep Residual Learning for Image Recognition}, author = {He, Kaiming}}
      """
    When I run shelf with arguments "import-bib refs.bib --apply --library library"
    Then no online source was queried
