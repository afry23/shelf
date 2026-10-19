# Slice m5-completion: Shell Completion

| | |
|---|---|
| **Slice ID** | `m5-completion` |
| **Branch** | `feat/m5-completion` (stacked on `feat/m5-mcp-http`) |
| **Milestone** | M5 – Polish |
| **Roadmap** | M5 slice 8 |
| **PRD references** | 7.1 (shell completion for bash, zsh and fish) |

## 1. Scope

`shelf completion bash|zsh|fish` writes a completion script to standard output. The scripts complete commands, subcommands, options (long and short) and the shells of `completion` itself; the value of `--library` is a path.

**Out of scope**: values of other options (tags, work IDs, formats), completion by calling the binary (`dotnet-suggest` style), PowerShell, installing the script.

## 2. Feature Files

`Features/Cli/Completion.feature` (`@PRD-7.1 @M5`, 31 scenarios with the outline rows).

## 3. Design

- ADR 0002 found no generator in System.CommandLine 2.0.12. The scripts are written from the command model (`CompletionCommand`): the root is walked once, so a new command, subcommand or option is in the scripts without further work. Hidden commands and options are left out; recursive options (`--json`) are offered below the command; the framework's `-h`/`--help` are included, `-?` is not (its shape breaks shell words).
- The scripts are static: nothing is started at completion time, no AOT-sensitive code runs, and the completion is as fast as the shell.
- bash: a `case` follows the typed words down the command tree, then `compgen -W`. Written for bash 3.2 (macOS). zsh: the same walk, `_describe` with the descriptions of the model. fish: `complete -c shelf` lines with conditions on `__fish_seen_subcommand_from`.
- Tests: bash is run for real (bash 3.2 here) and asked for completions with `COMP_WORDS`; zsh and fish are checked for syntax by the shell (`zsh -n`, `fish --no-execute`).

## 4. Result

BDD and unit tests green (2730 tests; 26 skipped for the missing e5 model, 1 for fish); AOT publish 0 warnings; `native.sh` has a completion section.

## 5. Open Questions

- fish is not installed on this machine: its script is checked by the text of the scenarios only until the CI run (`linux-x64`) has fish, which the runner image may not have either (then the scenario stays skipped).
- zsh completion is checked for syntax, not run (a real run needs a pseudo terminal and `compinit`).
- Only the first level of a subcommand chain is walked by position: a value that is spelled like a command (`shelf --library tag search`) moves the completion into that command.
