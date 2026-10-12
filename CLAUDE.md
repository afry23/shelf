# shelf

E-book manager in C# / .NET 10. Requirements: `docs/prd.md`. Plan and milestones: `docs/roadmap.md`.

## Development Process: BDD with Reqnroll

Every feature is developed behavior-driven with Reqnroll. These rules are mandatory:

1. **Feature files first.** Before writing any implementation code, specify the behavior in Gherkin feature files in `tests/Shelf.Specs/`.
2. **Concrete examples.** Back every scenario with concrete examples. Express variations as `Scenario Outline` with `Examples` tables instead of copying scenarios.
3. **English only.** Feature files, scenario names, steps, and examples are written in English.
4. **Traceability.** Tag every feature or scenario with the PRD requirement ID it covers (e.g. `@F-GRP-1`) and its milestone (e.g. `@M1`).
5. **Implementation second.** Start implementing only once the feature files exist. Write step definitions alongside the implementation.
6. **Always green.** After implementing, run the full BDD suite. A task is not done while any scenario is red.

Unit tests complement the BDD scenarios for internal details (e.g. title normalization, SimHash, pattern engine) but do not replace them.
