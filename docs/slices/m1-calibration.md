# Slice m1-calibration: Grouping quality against the ground truth

| | |
|---|---|
| **Slice ID** | `m1-calibration` |
| **Branch** | `feat/m1-calibration` (stacked on `feat/m1-commands`) |
| **Milestone** | M1 – Foundation |
| **PRD references** | F-GRP-6, F-GRP-1 to F-GRP-3; acceptance criterion 1 (12) |
| **Inputs** | `Shelf.TestData` generator and `ground-truth.json` (M0), grouping and import (`m1-grouping`, `m1-import-scan`) |

## 1. Scope

Turn acceptance criterion 1 into a permanent scenario: 500 mixed files with 100 works as EPUB + PDF are imported; at least 95 % of the pairs end in one work, and no work holds files of different books. The start values of F-GRP-6 were tuned in `m1-grouping` and `m1-import-scan`; this slice fixes them with a test and records the result.

## 2. Feature File

`Features/Grouping/Calibration.feature`: four seeds (1, 2, 3, 42) of `--works 389 --files 500` (the generator's formulas give exactly 100 works with two or three files); a rebuild of the index keeps the grouping; with `auto_merge = 1.0` still no wrong merge.

## 3. Evaluation

`CalibrationSteps` matches files of the imported library to the ground truth by SHA-256 (renamed files and skipped exact copies need no special care). Pair: a work with an EPUB and a PDF that is not an exact copy. Wrong merge: a library work whose files belong to more than one generated work.

## 4. Result (start values unchanged)

| Seed | Pairs merged | Wrong merges | Works (truth 389) |
|---|---|---|---|
| 1 | 100 / 100 | 0 | 389 |
| 2 | 100 / 100 | 0 | 389 |
| 3 | 99 / 100 | 0 | 390 |

No value changed; PRD 6.3 records the check.

## 5. Open Questions

None for the owner.
