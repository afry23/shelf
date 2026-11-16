# Turning a PDF into a reflowable EPUB (for a Kindle)

A PDF has a fixed page layout. On a small screen, such as a Kindle, you zoom and scroll. A reflowable EPUB has no pages: the text fills the screen at the font size you choose. `shelf convert` makes one from the PDF of a work and keeps it as another file of the **same work**, next to the PDF.

shelf brings no converter. It runs one that **you** install, the way `shelf import --from rclone:` runs your own `rclone`. You tell shelf which command to run in the config. Nothing is converted unless you ask, and shelf itself never goes online for this.

PRD: section 6.16 (F-CNV-1 to F-CNV-4). Measurements and the reasons for the choice: [ADR 0022](adr/0022-pdf-to-epub-converter.md).

## 1. Install the tools

The two recipes in `tools/` need a layout tool and `pandoc`.

| Tool | Install | Notes |
|---|---|---|
| `pandoc` | `brew install pandoc` (or your package manager) | Writes the EPUB. Small. |
| `marker` | `uv tool install marker-pdf` | Default recipe. Reads the layout of the pages (columns, tables, footnotes). |
| `docling` | `uv tool install docling` | Alternative recipe. |

Install **one** of marker or docling; you only need both if you want to compare them.

Both layout tools load machine-learning models (several GB, with PyTorch). They download them **on the first run** from the internet, into their own cache. That is the tool, not shelf, so allow for a long first run (see `convert.timeout` below).

`epubcheck` (`brew install epubcheck`, needs Java) is optional. It checks an EPUB for errors; shelf does not need it.

## 2. Configure the converter

Add this to the global config (`~/.config/shelf/config.toml`) or to the config of one library (`<root>/.shelf/config.toml`). The library config wins.

```toml
[convert]
command = "/path/to/shelf/tools/pdf2epub.sh {input} {output}"
timeout = 1800
```

| Key | Default | Meaning |
|---|---|---|
| `convert.command` | none | The command to run. It **must** contain `{input}` (the PDF) and `{output}` (the EPUB the command has to write). Without it `shelf convert` fails and names this key. |
| `convert.timeout` | `600` | Seconds the command may run (1 to 86400). Longer runs are stopped. The first run downloads models, so `1800` is a good value to start with. |

How the command is run:

- It is **not** run by a shell. shelf splits it at spaces, replaces `{input}` and `{output}` in each part, and starts the first part as the program. A path with a space in it, quotes and `|` or `&&` do not work. Put what you need into a script, as the recipes in `tools/` do.
- Use the absolute path of the script, or a program that is on your `PATH`.
- The input is the PDF in your library. The output is a file in a temporary folder that shelf removes afterwards.
- A command that exits with a code other than 0 is an error and its message (standard error) is shown.

## 3. Choose a recipe

| Recipe | Layout tool | Use it |
|---|---|---|
| `tools/pdf2epub.sh` | marker | **Default.** Faster (17 s for 143 pages against 33 s), finds more tables, puts footnotes after the main text. |
| `tools/pdf2epub-docling.sh` | docling | When marker reads words as formulas. In a 143-page German law guide marker read 150 words as formulas (34 of them with a space between the letters), docling none. |

What both do well, measured on test PDFs and one real book: text in **one or two columns** comes out in reading order, and the running head or footer is mostly removed. The EPUB passes `epubcheck`. They differ in one place: in the two-column test page marker put the footnote after the main text, docling put it between the two columns.

Try the second recipe when the first one gives you strange words: change `command` and run `shelf convert <work> --replace`.

Calibre's `ebook-convert` and MinerU were not tried (Calibre was left out as too large an installation).

## 4. Convert

```bash
shelf convert <work>                 # the work has exactly one PDF
shelf convert <work> --file Book.pdf # it has several: say which
shelf convert <work> --replace       # convert again; the old EPUB goes to the trash
shelf undo                           # take the last conversion back
```

- `<work>` is the work ID (or the start of it), as for the other commands. `--json` gives the result as JSON.
- The EPUB gets the name of the PDF with the ending `.epub`, and the metadata and the cover of the work stay as they are.
- The new file is indexed like any other: the full text search finds it (`format:epub`) and the web reader opens it.
- A work that **has an EPUB already** is refused, unless you give `--replace`. If the converter makes exactly the same EPUB again, nothing changes and you are told so.

### In the web interface

On the work page, the tab **Files** has a block **Make an EPUB** with the button **Convert to EPUB** (**Convert to EPUB again** if the work has an EPUB). It is shown only for a work with a PDF. The REST call is `POST /api/v1/works/{id}/convert` with the optional body `{"file": "<file ID>", "replace": true}`.

The converter runs **on the machine of `shelf serve`**. That machine needs the tools and the config, not the browser's.

## 5. Read it on the Kindle

Amazon's Send to Kindle takes EPUB files. Send the EPUB from the work folder (`shelf show <work>` lists the files) by e-mail, with the app or on the website. This was not tried on a real Kindle while this was written, so open the book once and look at it before you convert a whole shelf.

## 6. Limits

- **No OCR.** A PDF that is only a scan (no text layer) is refused with a message that says so. Run it through an OCR tool first, then import the result.
- **Formulas stay weak.** Mathematical text is written as TeX or MathML; a Kindle shows it poorly.
- **Headings have one level.** Both tools write every heading at the same level, so the table of contents of the EPUB is flat.
- **Tables and images** are converted but were not judged on a real Kindle. docling puts images into the EPUB, marker did not in the tested book.
- **Page numbers and print layout are gone.** That is the point of a reflowable EPUB, but a quote can no longer be given by page.
- **Check the result.** A converter reads the layout, it does not understand the book. Skim the chapters you care about.

## 7. Rights

The tools are machine-learning models that run **on your computer**; the PDF is not sent anywhere. Some publishers still forbid the use of their files "in connection with AI systems or language models" in a notice in the file, for example in the footer. Read the notice of a licensed book before you convert it. This is a question of your license, not of shelf.

The weights of the marker models have their own license (free for personal use, limits for commercial use). It has not been checked in detail; read it before you use marker at work.

## 8. When something goes wrong

| Message | Cause and what to do |
|---|---|
| `no converter is configured; set convert.command …` | The key is missing. Add it (section 2). |
| `<tool> was not found; install it or change convert.command` | The program of the command is not on your `PATH`, or the path is wrong. For `shelf serve`, check the `PATH` of the server. |
| `<tool> failed (exit code N): …` | The converter itself failed. The text after the colon is its own message. Run the command by hand on the PDF to see more. |
| `<tool> timed out after N s` | Too slow, often the first run (model download). Raise `convert.timeout`, run once by hand, try again. |
| `<tool> wrote no output file` | The command exited with 0 but did not write `{output}`. Check that the script uses the second argument. |
| `the result of <tool> is not a valid EPUB` / `has no chapter text` | The output is no EPUB or has no text. Nothing is added. |
| `<file> has no text layer; shelf has no OCR` | A scanned PDF. See the limits. |
| `<work> already has an EPUB (…); use --replace` | Say `--replace` if you want to convert again. |
| `<work> has N PDF files; name one with --file` | Give `--file`. |
| `the converter made an EPUB that is identical to …` | Same result as before; nothing changed. |

The recipes print what marker or docling say on standard error, which shelf shows when a command fails.
