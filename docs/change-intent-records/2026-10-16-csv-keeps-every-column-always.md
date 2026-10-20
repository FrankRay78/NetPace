# CSV Keeps Every Column, Always

**Intent:** Make a CSV log safe to append to over weeks and safe to read by position, which the old row was neither: the unit in a speed cell shifted with the automatic scale, and a skipped test dropped its columns and moved every later one.

**Behaviour:** Specified by issue #378, whose acceptance criteria this implements. The field naming, whole-number precision and failed-test rule come from [`2026-10-15-json-numbers-name-their-unit-in-the-field`](2026-10-15-json-numbers-name-their-unit-in-the-field.md), which committed CSV to matching JSON.

**Constraints:**

- CSV is a contract scripts already read, and unlike JSON it has no room for a second form of each value — one cell, one representation, so the formatted strings go rather than sit alongside the numbers.
- Users reading a header name, reading by position, or passing a unit option all break. Pre-1.0, stating that is enough (Constitution VII).

**Decisions:**

- Print every column in every row, leaving a skipped test's cells empty, rather than omitting its columns as the old row did.
  - Rejected: keeping the old shape. A header is written once and a `--loop` run appends rows under it, so dropping columns only works while the flags never change; it also means `netpace --csv --no-upload` and `netpace --csv` produce files that cannot be concatenated.
  - Chose: fixed columns. An empty cell says "not measured" where a `0` would claim a measurement, which is the same distinction the writer already drew by omitting a test's columns entirely.
- Remove `--csv-header-units` rather than keep it as a no-op or an error.
  - Rejected: an error naming the removed option (Constitution III prefers one where simple). Adding an option back to the parser solely to reject it is not simple, and the option's whole purpose — move a unit out of the cell — no longer describes anything CSV does.
  - Chose: removal. Its validation rule went with it, so `--csv` with `--count` or `--loop` is now accepted under every `--unit-scale`.

**Date:** 2026-10-16
