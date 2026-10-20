# A review rating is raised, never lowered, and old-code bugs are reported, not fixed

**Intent:** Close the ways `/verify` could report a branch verified when it should not, or stop for the wrong reason, that #346 found in how a finding's severity decides the verdict.

**Behaviour:** [`.claude/commands/verify.md`](../../.claude/commands/verify.md) — the reviewer brief, and the rules table with its definitions, in step 2.

**Constraints:** The reviewer agents come from a plugin, so their own prompts cannot be edited; the scale can only be asked for in the brief `/verify` gives them.

**Decisions:**

- *Rejected: a translation table from each reviewer's own scale.* Mapping "HIGH" / "MEDIUM" and the numeric bands onto this scale was tried on #346's first attempt and became its own source of review findings — letter case, band boundaries — so a finding its reviewer did not rate on the scale is reported as unrated and placed by whether it is a defect.
- *Rejected: treating every unrated finding as Important.* The first choice, softened: the plugin reviewers have output shapes of their own, so a true but trivial point reported unrated would have become a mandatory fix.

**Date:** 2026-10-09
