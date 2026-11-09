# Default Output Is For A Person At A Modern Terminal

**Intent:** Free default output to use the terminal — animation, colour, Unicode, live redraw — by saying plainly that it is not a format anyone should scrape, and naming what a script reads instead.

**Behaviour:** The rule is in `CLAUDE.md` → *Product Principles*; the layout it licensed is issue #526.

**Constraints:**

- `--simple`, `--csv` and `--json` already exist and are stable, so there is somewhere for a script to go.
- The pre-1.0 stance already lets any release change an output format; this rule says default output stays changeable after 1.0.0 too, which the stance alone would not.
- Colour cannot be the only thing carrying a meaning: `NO_COLOR` and the `plain` theme must lose nothing but the colour.

**Decisions:**

- Where default output cannot animate it writes the `--simple` lines. Rejected: a cut-down table of its own — a second human-readable layout to keep in step with the first, for a destination no person is watching.
- Each destination is judged on its own, so `--file` at a terminal gets both. Rejected: one decision for the whole run — it would either put control codes in a log or take the table off the screen.
- Interactivity alone decides it. Rejected: sniffing for cursor support, which answers a question Spectre.Console already answers.

**Date:** 2026-11-09
