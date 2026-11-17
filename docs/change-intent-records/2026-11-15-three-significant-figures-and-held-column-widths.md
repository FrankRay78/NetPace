# Three Significant Figures, And Column Widths Held Across Draws

**Intent:** Stop the live table's figures moving sideways as they change, and stop a speed claiming precision a speed test cannot measure.

**Behaviour:** See issue #558's confirmed decisions.

**Constraints:**

- The rule is part of `NetPace.Core`'s published surface: every format showing a speed to a person reads it from the same extensions. CSV, JSON and `GetBitsPerSecond` stay outside it.

**Decisions:**

- Re-banding after rounding. Rejected: rounding once writes `9.996` as `10.00`, four figures.
- `"G3"` formatting. Rejected: it drops trailing zeros and uses exponents, losing digits before the point.
- Fixing `TableColumn.Width`. Rejected: Spectre.Console then wraps an outsized figure instead of growing the column; padding the cell text raises the floor instead.
- Listing unit names in the console project to reserve the Speed column. Rejected: they would drift from the formatter's own, so the width is read back from it.

**Date:** 2026-11-15
