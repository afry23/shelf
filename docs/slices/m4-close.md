# Slice m4-close: End-to-End Check, Measurements and Close of M4

| | |
|---|---|
| **Slice ID** | `m4-close` |
| **Branch** | `feat/m4-library-core` |
| **Milestone** | M4 – Web |
| **Roadmap** | M4 slice 9 |
| **PRD references** | 9.1 (library view, idle RAM), 9.2 (Native AOT) |
| **Inputs** | [ADR 0017](../adr/0017-m4-web-measurements.md), `tests/e2e/native.sh`, `tests/perf/measure.py` |

## 1. Scope

1. `tests/e2e/native.sh`: a web section that starts `shelf serve` from the native binary and checks the API, a range request, the cover, the page and its policy, the token and the cookie, and a refused bind.
2. `tests/perf/measure.py --web` (`--web-runs`): start time, resident memory, the library view and the list variants, search, details, cover, duplicates.
3. Measurement on the reference library (10,011 works), the cost of `shelf covers`, and the view in a real browser.
4. ADR 0017 with decisions, numbers and open points; roadmap status, result and carried-over table.

**Out of scope:** the tag `v0.4.0` (owner), the first `linux-x64` run (CI).

## 2. Feature Files

None. The slice adds no behavior; the checks are the native script and the measurements.

## 3. Result

All exit criteria met (ADR 0017): library view p95 33 ms (browser: 97 ms to the first list), idle RAM 41.5 MB, acceptance criterion 5 green.
