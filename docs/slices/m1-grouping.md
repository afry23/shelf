# Slice m1-grouping: Grouping Cascade and Review Queue

| | |
|---|---|
| **Slice ID** | `m1-grouping` |
| **Branch** | `feat/m1-grouping` (stacked on `feat/m1-extraction`) |
| **Milestone** | M1 – Foundation |
| **Roadmap** | M1 area "Grouping"; note "the grouping thresholds (F-GRP-6) are calibrated against acceptance criterion 1" (slice `m1-calibration`) |
| **PRD references** | F-GRP-1, F-GRP-2, F-GRP-3, F-GRP-6 (Must); F-IMP-7 (Should, needed by F-GRP-3); 6.3 tables; 10.3 `review_queue` |
| **Inputs** | `Shelf.Extraction` (metadata, text), `Shelf.Index` (schema), PRD 6.3 |

---

## 1. Scope

**In scope**

1. `TitleNormalizer` (F-GRP-2): lower case, accents removed (`ß` → `ss`, `æ` → `ae`, `ø` → `o`, ...), edition notes removed (`(2nd Edition)`, `(German Edition)`, `third ed.`, `3. Auflage`), punctuation → spaces, a **leading** article (der, die, das, the, a, an) removed. A title that would become empty keeps its lower-case form.
2. `AuthorNames`: surname key and sort key from "Martin Kleppmann", "Kleppmann, Martin", "Jan van der Berg", "Martin Luther King Jr."; two author lists match if they share a surname key.
3. `SimHash`: 64 bit fingerprint over the first 5000 words, each distinct word weighted `1 + ln(count)`; fewer than 200 words → none.
4. `WorkMatcher` (F-GRP-1, F-GRP-3, F-GRP-6): the cascade ISBN → title + surname → fingerprint with the confidences of PRD 6.3; merge only if the best match of the first sure stage is unique; otherwise a new work plus review candidates (confidence ≥ `grouping.review`).
5. New setting `grouping.auto_hamming` (default 3, must not exceed `max_hamming`): the "≤ 3" boundary of the PRD table is a threshold too (F-GRP-6).
6. `ReviewQueue` (F-IMP-7) on `.shelf/review.json`, schema 1: entries of the kinds `grouping`, `extraction-error`, `weak-metadata`, with ID, time, message, file, work and candidates (work, confidence, signals). Atomic save, strict reading, never overwrites a damaged file.
7. `reindex` fills `title_norm` and `author_sort` with the real rules and mirrors the queue into `review_queue`; a damaged queue file is a problem (exit 1), the rest is still indexed.
8. `WorkCandidateStore` loads the works of the index as candidates (used by `m1-import-scan`).

**Requirements covered:** F-GRP-1, F-GRP-2, F-GRP-3, F-GRP-6, F-IMP-7 (queue; the `review` command is `m1-commands`).

**Out of scope**

- Calling the cascade for imported files and writing review entries: `m1-import-scan`.
- `shelf merge`, `split`, `review` commands: `m1-commands`.
- Calibration of the thresholds against the generated library: `m1-calibration`.

## 2. Feature Files

`Features/Grouping/`: `TitleNormalization.feature` (`@F-GRP-2`, 25 + 3 + 4 rows), `AuthorNames.feature` (`@F-GRP-1`), `SimHash.feature` (`@F-GRP-1`), `WorkMatching.feature` (`@F-GRP-1 @F-GRP-3 @F-GRP-6`: 18 table rows, ambiguity, stage order, not-a-duplicate guard, sequential files, 8 threshold rows), `ReviewQueue.feature` (`@F-IMP-7`). `Settings.feature` gains `auto_hamming`; `Reindex.feature` gains the normalized title and sort key.

## 3. Design

- **Subtitles are kept** in `title_norm`: "Python: Introduction" and "Python: Advanced Topics" must stay different. The cost: "Flatland" and "Flatland: A Romance of Many Dimensions" are different titles and need the fingerprint or the ISBN to meet.
- **Articles only at the start**, only the six of the PRD.
- **Surname rule:** the last word of the surname part (lower-case particles such as van, von, de belong to the sort key only). "Jan van der Berg" and "van der Berg, Jan" meet at `berg`. Two authors with the same surname and the same title (Frank and Brian Herbert) are not told apart; the PRD asks for surnames.
- **No guard for different ISBNs** (PRD table followed): the EPUB and the print PDF of a book usually have different ISBNs, so a mismatch must not block title + author. Revisit in `m1-calibration` if it produces wrong merges.
- **Fingerprint weighting `1 + ln(count)`** was chosen by measurement on Zipf-distributed texts (6000 words; shared function words, individual tail): unrelated texts 20-29 bits apart, 1-3 % replaced words 0-5 bits, hyphenation/header/page-number artefacts 3-8 bits. Binary presence and `sqrt(tf)` were worse (unrelated down to 13-18 bits). Uniform random words from one vocabulary are **not** a valid test text: all texts then share nearly all words and look alike. The algorithm is frozen by a golden value in `SimHashTests`; a change forces every stored fingerprint to be recomputed.
- **Real format pairs** will likely land at 4-8 bits (review band) rather than ≤ 3; `m1-calibration` decides whether `auto_hamming` rises.
- **Review queue concurrency:** read-modify-write without a lock; import and review run one at a time.
- A candidate's `notDuplicates.files` names block a merge of an incoming file of that name.

## 4. Unit Tests

`SimHashTests` (golden value, minimum, maximum, segments, folding, distance), `WorkMatcherTests` (ISBN ambiguity, signals of several stages, ordering, fingerprint after merge, empty signature), `NamesAndTitlesTests` (edition notes, idempotence, particles, apostrophes), `ReviewQueueTests` (round trip, empty-queue text, missing file, append/remove, kinds, damaged file kept, payload), `WorkCandidateStoreTests` (load including the sign of stored fingerprints).

## 5. Open Questions

None for the owner. Carried: review-queue entry creation for weak metadata (title or authors from the file name) is decided in `m1-import-scan`.

## 6. Implementation Notes

- The probe that chose the weighting was a scratch program outside the repository; its numbers are above.
- `files.simhash` is stored as a signed 64 bit integer; `WorkCandidateStore` reads it back as the same bits.
