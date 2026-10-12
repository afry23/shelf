---
name: slice
description: Run the full plan, implement, format, review workflow for one vertical slice of shelf using the slice-planner, slice-implementer, and slice-reviewer subagents.
argument-hint: <slice-id> <short description, e.g. "m1-grouping-exact F-GRP-1 exact grouping by identifier">
disable-model-invocation: true
---

Run the slice workflow for: $ARGUMENTS

Subagents cannot spawn subagents, so you orchestrate every step yourself. Run the steps strictly in order.

## 1. Plan

Spawn the `slice-planner` agent with the slice ID and description. It writes `docs/slices/<slice-id>.md`.

Read the plan. If its "Open questions" section holds items that change behavior, ask the user before continuing. Otherwise continue.

## 2. Implement

Spawn the `slice-implementer` agent. Point it at `docs/slices/<slice-id>.md`.

## 3. Quality gate (run yourself, not via subagent)

Always run these, in this order, from the repo root:

1. `dotnet format` (applies formatting and style fixes)
2. `dotnet build` (warnings are errors via `Directory.Build.props`; never bypass with `-p:TreatWarningsAsErrors=false`)
3. `dotnet test` (the full BDD suite plus unit tests)

If build or tests fail, send the exact output back to `slice-implementer` to fix, then repeat step 3. After 2 failed rounds, stop and report to the user.

## 4. Review

Spawn the `slice-reviewer` agent for the working-tree diff and the plan.

- `VERDICT: APPROVE`: continue to step 5.
- `VERDICT: CHANGES_REQUESTED`: pass the blocking findings to `slice-implementer`, then repeat step 3 and step 4. At most 2 review rounds. If still not approved, stop and report the open findings to the user.

## 5. Report

Do not commit, push, or open a PR unless the user asks. Report briefly:

- slice ID and requirement IDs covered
- files changed
- result of `dotnet format`, `dotnet build`, `dotnet test` (counts)
- review verdict and any remaining optional suggestions
- deviations from the plan and open questions
