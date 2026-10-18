# Slice m2-related: Related Editions (Should)

| | |
|---|---|
| **Slice ID** | `m2-related` |
| **Branch** | `feat/m2-related` (stacked on `feat/m2-dupes`) |
| **Milestone** | M2 – Order (Should item) |
| **PRD references** | F-GRP-5, 10.4 |
| **Inputs** | `WorkMatcher`, `LibraryImporter.CreateWork`, `LibraryEditor.MarkNotDuplicates`, [m1-grouping](m1-grouping.md) |

---

## 1. Scope

**In scope**

1. `related` in `book.json`: the IDs of the works that are another edition of the work (F-GRP-5). Optional, left out when empty, `schema` stays 1.
2. Import: a file that grouping puts into a work of its own, and whose match is another edition (year or ISBN differs), marks both works as related.
3. `shelf show`: a `Related` line per related work (text) and a `related` array (JSON).
4. `shelf edit <work> --add-related <other>` and `--remove-related <other>`: mark or unmark by hand, on both works.
5. `merge` drops the relation between the two merged works and unites their other relations.

**Requirements covered:** F-GRP-5.

**Out of scope**

- A `related` table in the index and search by relation: nothing needs it yet; `show` reads the sidecars. No index version bump.
- Re-pointing the `related` entries of third works that name a work removed by `merge`: such an ID is ignored when shown.
- Showing related works in the web UI (M4).

## 2. Feature Files

`Features/Grouping/RelatedEditions.feature` (`@F-GRP-5 @M2`): another ISBN becomes a work of its own, both sides name each other, `show` and its JSON, two files of one book stay one work, no related line without a relation, merge removes the relation. `Features/Grouping/RelatedEdit.feature`: mark by hand, `show` names the year, marking twice, unmarking from either side, a work cannot be related to itself, an unknown work, the mark survives `reindex`.

## 3. Design

- **Signal.** `GroupingMatch.IsOtherEdition` is true if the signals contain "title and author match, year differs" or "ISBN differs" (the two guards of the matcher). The strings are constants of `WorkMatcher`.
- **Review entries stay.** The related mark does not replace the grouping review entry: a person may still merge the two, and the merge removes the mark.
- **Both sides.** The import writes the new work's ID into the sidecar of the existing work (not journaled; the import is not either, see [m2-journal](m2-journal.md)); `edit` writes both through `SidecarStore`.
- **PRD.** Section 10.4 shows `related`. Decision without ADR.

## 4. Unit Tests

`SidecarFormatTests` (round trip, left out when empty and not unknown, own ID refused, key named for an invalid entry), `WorkMatcherTests` (year or ISBN differs gives another edition).

## 5. Open Questions

None for the owner.
