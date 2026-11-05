# The review-cost table lives in the codebase, as an exception to issue numbers living in records

**Intent:** Keep one readable, cumulative table of what each pull request's automated review cost, beside its size and first-review risk, so the cost of a review rule change can be judged across pull requests rather than one at a time.

**Behaviour:** `scripts/pr-review-costs.sh` adds a row to `docs/metrics/pr-review-costs.md` for each closed pull request carrying a cost footer, never rewriting a row already there, and writes the same rows to `pr-review-costs.csv` beside it for a spreadsheet. The maintainer runs it by hand and commits the result. The Markdown opens with a note telling agents it is not guidance; both files are marked `linguist-generated` so diffs collapse them, and are left out of the audit's scope.

**Decisions:**

- *Not a wiki page.* The wiki's sidebar narrows a thirteen-column table until it is hard to read.
- *Not a gist.* It would sit outside both the repository and the wiki, with nothing linking the data to the code it measures.
- *The CSV is derived from the Markdown, not the other way round.* The Markdown rows are what a hand correction edits, so the CSV is rewritten from them on every run and cannot drift.
- *An exception to the issue-number rule rather than a table without numbers.* A row's pull request number is its key and its link; without it the table cannot stay additive.

**Date:** 2026-11-05
