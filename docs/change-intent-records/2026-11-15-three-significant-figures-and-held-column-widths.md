# Three Significant Figures, And Column Widths Held Across Draws

**Intent:** Stop the live table's figures moving sideways as they change, and stop a speed claiming precision a speed test cannot measure.

**Behaviour:** See issue #558, whose confirmed decisions settle the per-run width lifetime and the one rounding rule both unit systems follow.

**Constraints:**

- The three-significant-figure rule is now part of `NetPace.Core`'s published surface: every format that shows a speed to a person reads it from the same extensions, so a new one cannot format a speed of its own.
- CSV and JSON stay outside it, and `GetBitsPerSecond` stays outside it too, reporting bits per second to the nearest whole bit.

**Decisions:**

- Re-banding after rounding. Rejected: rounding once, which writes `9.996` as `10.00` — four significant figures, the thing the change exists to stop.
- `"G3"` or another general significant-figure format. Rejected: it switches to exponent notation and drops trailing zeros, so `8.00` would read `8` and a speed pinned to a small unit would lose digits before the point.
- Fixing `TableColumn.Width` to the running maximum. Rejected: an explicit width stops Spectre.Console sizing the column at all, so a figure too wide for it wraps inside a column that could have grown. Padding the cell text raises the floor instead, leaving a window too narrow for the table free to wrap it rather than lose a figure.
- Reserving the Speed column from unit names listed in the console project. Rejected: it duplicates the formatter's own names, so an added or renamed unit would leave the reserve silently too narrow; the width is read back out of the formatter.

**Date:** 2026-11-15
