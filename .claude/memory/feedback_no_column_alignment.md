---
name: Don't introduce column-aligned whitespace in code
description: Avoid vertical-aligned if/return/=>/comment chains in C# (and any language). Use single-space tokens; only end-of-line trailing comments on tabular data are safe to align.
type: feedback
---

Don't introduce column-aligned whitespace in code: vertical-aligned `if`/`return`/`=>` chains, padded identifiers to a column, columns of equals signs in field initialisers. Standard single-space C# style only. End-of-line trailing comments on `[InlineData]` rows or similar tabular test data are fine to align — those sit at end-of-line and don't shift on row edits.

**Why:** During feature 001-linux-aot-release, the rewritten `TimeSpanFormatter.Humanize` shipped a column-aligned ladder of `if (totalDays >= DaysInAYear)  return Format((int)(totalDays / DaysInAYear),  "year",        "years");`-style lines (extra spaces between columns to vertically align identifiers and string literals). User flagged: *"this is non-standard spacing - the code layout shouldn't be like this. remove the multiple white spaces, we also need to make a note for this in learnings."* Reasons aligned blocks are bad: (1) any rename that changes the longest identifier's width forces a re-format of every row → spurious diff churn, (2) the alignment breaks under variable-width fonts and outside the editor that produced it, (3) inconsistent with the rest of the codebase which uses single-space tokens.

**How to apply:** When writing or editing C# (or any language) — including refactors and new code — use single-space token separation between identifiers, operators, and method arguments. Don't pad consecutive `if`/`return`/`=>`/`=` lines to align columns with neighbouring lines. The only acceptable alignment is end-of-line trailing comments on genuinely tabular data (e.g. `[InlineData]` test rows where the comment annotates each row's specific value); even there, prefer not to align if the comment widths vary much.
